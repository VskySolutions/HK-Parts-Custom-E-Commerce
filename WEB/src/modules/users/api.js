/*
 * Users module API resource group (WO-62 / REQ-ADM-004).
 *
 * Mirrors the widgetApi style from services/api.js. Admin user CRUD under
 * /api/admin/users, plus assignRoles() (PUT .../roles) which replaces a user's
 * role set, and roles() — a thin read of /api/admin/roles used to populate the
 * user's role selector.
 */
import { api, unwrap, qsSerializer } from 'services/api'

export const userApi = {
  // GET /api/admin/users -> PaginatedList<AdminUserDto> ({ items, totalCount, ... }).
  list (params = {}) {
    return api
      .get('/api/admin/users', { params, paramsSerializer: qsSerializer })
      .then(unwrap)
  },
  get (id) {
    return api.get(`/api/admin/users/${id}`).then(unwrap)
  },
  create (payload) {
    return api.post('/api/admin/users', payload).then(unwrap)
  },
  update (id, payload) {
    return api.put(`/api/admin/users/${id}`, payload).then(unwrap)
  },
  remove (id) {
    return api.delete(`/api/admin/users/${id}`).then(unwrap)
  },
  // PUT /api/admin/users/{id}/roles — replace the user's assignments with exactly
  // roleIds (an array of role GUIDs).
  assignRoles (id, roleIds) {
    return api.put(`/api/admin/users/${id}/roles`, { roleIds }).then(unwrap)
  },
  // Role options for the user's role selector (the roles list endpoint returns all).
  roles () {
    return api.get('/api/admin/roles').then(unwrap)
  },
  // PUT /api/admin/users/{id}/password — admin overwrites the user's password directly.
  setPassword (id, newPassword) {
    return api.put(`/api/admin/users/${id}/password`, { newPassword }).then(unwrap)
  },
  // POST /api/admin/users/{id}/send-password-reset — email the user a reset link.
  sendPasswordReset (id) {
    return api.post(`/api/admin/users/${id}/send-password-reset`).then(unwrap)
  }
}

export default userApi
