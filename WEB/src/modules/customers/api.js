/*
 * Customers admin module API layer (WO-117): customer list + detail, create + profile edit, single Customer Group
 * assignment, activate/deactivate, and store credit. Wraps AdminCustomersController.
 * Tax-exemption review lives in its own module (WO-126) — here it is read-only status only.
 */
import { api, unwrap, qsSerializer } from 'services/api'

const CUSTOMERS = '/api/admin/customers'

export const customerAdminApi = {
  list (params = {}) { return api.get(CUSTOMERS, { params, paramsSerializer: qsSerializer }).then(unwrap) },
  get (id) { return api.get(`${CUSTOMERS}/${id}`).then(unwrap) },

  // Admin creates a customer. payload: { firstName, lastName, email, phoneNumber, password, isActive, emailVerified,
  //   longText, allowRestrictedCapacityPurchases }.
  // Returns the new customer id (Guid string).
  create (payload) { return api.post(CUSTOMERS, payload).then(unwrap) },

  // Admin edits a customer's profile. payload: { firstName, lastName, phoneNumber, longText, allowRestrictedCapacityPurchases }.
  // Email is read-only (not sent). Requires Users.Write server-side. Returns the refreshed detail.
  update (id, payload) {
    return api.put(`${CUSTOMERS}/${id}`, { customerId: id, ...payload }).then(unwrap)
  },

  // Admin password reset (separate from the profile update). payload: { newPassword, confirmPassword }.
  // Requires Users.Write server-side. 204 on success; also signs the customer out of all sessions.
  resetPassword (id, payload) {
    return api.put(`${CUSTOMERS}/${id}/password`, { customerId: id, ...payload }).then(unwrap)
  },

  // Soft-delete: the record is kept but hidden from lists, and the customer's login is deactivated.
  // Requires Users.Write server-side. 204 on success; 404 unknown; 409 already deleted / staff account.
  remove (id) { return api.delete(`${CUSTOMERS}/${id}`).then(unwrap) },

  // Single pricing group (AC-CUS-003.2). getGroup → brief|null; setGroup(null) clears it (base pricing).
  getGroup (id) { return api.get(`${CUSTOMERS}/${id}/group`).then(unwrap) },
  setGroup (id, customerGroupId) {
    return api.put(`${CUSTOMERS}/${id}/group`, { customerId: id, customerGroupId }).then(unwrap)
  },

  // Activate / deactivate the customer's login (User.IsActive). Returns the refreshed detail.
  setActive (id, isActive) {
    return api.put(`${CUSTOMERS}/${id}/active`, { customerId: id, isActive }).then(unwrap)
  },

  // Exempt from the restricted-state shipping rule (SR-92). Returns the refreshed detail.
  setShippingRestrictionExempt (id, isExempt) {
    return api.put(`${CUSTOMERS}/${id}/shipping-restriction-exempt`, { customerId: id, isExempt }).then(unwrap)
  },

  getStoreCredit (id) { return api.get(`${CUSTOMERS}/${id}/store-credit`).then(unwrap) },
  issueStoreCredit (id, payload) { return api.post(`${CUSTOMERS}/${id}/store-credit`, { customerId: id, ...payload }).then(unwrap) }
}

/**
 * Customer → Documents tab (AdminCustomerDocumentsController). Files live in private storage: they are fetched
 * as an authenticated blob (never a public URL). Writes require Users.Write server-side.
 */
export const customerDocumentsApi = {
  // [{ value: 'MilitaryId', label: 'Military ID' }, ...] — the backend owns the list.
  types () { return api.get(`${CUSTOMERS}/document-types`).then(unwrap) },
  // { items: [...newest first, active + deleted], activeCount }
  list (customerId) { return api.get(`${CUSTOMERS}/${customerId}/documents`).then(unwrap) },
  // rows: [{ documentType, note, file }] → multipart items[i].*; all or nothing. Returns the refreshed list.
  upload (customerId, rows) {
    const fd = new FormData()
    rows.forEach((r, i) => {
      fd.append(`items[${i}].documentType`, r.documentType || '')
      fd.append(`items[${i}].note`, r.note || '')
      if (r.file) fd.append(`items[${i}].file`, r.file)
    })
    return api.post(`${CUSTOMERS}/${customerId}/documents`, fd).then(unwrap)
  },
  // { documentType, note, file? } — file optional (replaces the stored file).
  update (customerId, documentId, { documentType, note, file }) {
    const fd = new FormData()
    fd.append('documentType', documentType || '')
    fd.append('note', note || '')
    if (file) fd.append('file', file)
    return api.put(`${CUSTOMERS}/${customerId}/documents/${documentId}`, fd).then(unwrap)
  },
  remove (customerId, documentId) { return api.delete(`${CUSTOMERS}/${customerId}/documents/${documentId}`).then(unwrap) },
  // Authenticated blob of the stored file (the raw storage location is never exposed).
  file (customerId, documentId) {
    return api.get(`${CUSTOMERS}/${customerId}/documents/${documentId}/file`, { responseType: 'blob' }).then((r) => r.data)
  }
}

/** Client-side upload rules — mirror CustomerDocumentFiles on the API (which re-checks the real content type). */
export const customerDocumentRules = Object.freeze({
  extensions: ['.pdf', '.doc', '.docx', '.jpg', '.jpeg', '.png'],
  accept: '.pdf,.doc,.docx,.jpg,.jpeg,.png',
  maxBytes: 10 * 1024 * 1024,
  maxRows: 10,
  noteMax: 2000,
  note: 'Only PDF, DOC, DOCX, JPG, PNG files are allowed. Maximum size 10 MB per file.'
})

/** Active pricing groups, for the customer-detail assignment single-select and the list filter. */
export const customerGroupOptionsApi = {
  listActive () {
    return api.get('/api/admin/customer-groups', { params: { activeOnly: true }, paramsSerializer: qsSerializer }).then(unwrap)
  }
}

export default { customerAdminApi, customerGroupOptionsApi }
