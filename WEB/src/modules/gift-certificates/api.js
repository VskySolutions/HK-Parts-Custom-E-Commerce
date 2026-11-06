/*
 * Gift Certificates admin API (AdminGiftCertificatesController). Enums travel as names:
 * status = Active | Inactive, source = Admin | Purchased,
 * ledger action = Created | AdminAdjusted | Redeemed | Refunded | StatusChanged | Voided.
 */
import { api, unwrap, qsSerializer } from 'services/api'

const BASE = '/api/admin/gift-certificates'

export const giftCertificateApi = {
  list (params = {}) { return api.get(BASE, { params, paramsSerializer: qsSerializer }).then(unwrap) },
  get (id) { return api.get(`${BASE}/${id}`).then(unwrap) },
  create (payload) { return api.post(BASE, payload).then(unwrap) },
  update (id, payload) { return api.put(`${BASE}/${id}`, payload).then(unwrap) },
  remove (id) { return api.delete(`${BASE}/${id}`).then(unwrap) },
  resendEmail (id) { return api.post(`${BASE}/${id}/resend-email`).then(unwrap) },
  getSettings () { return api.get(`${BASE}/settings`).then(unwrap) },
  saveSettings (payload) { return api.put(`${BASE}/settings`, payload).then(unwrap) },
  async exportCsv (params = {}) {
    const res = await api.get(`${BASE}/export`, { params, paramsSerializer: qsSerializer, responseType: 'blob' })
    const url = URL.createObjectURL(res.data)
    const a = document.createElement('a')
    a.href = url
    a.download = `gift-certificates-${new Date().toISOString().slice(0, 10)}.csv`
    document.body.appendChild(a)
    a.click()
    a.remove()
    URL.revokeObjectURL(url)
  }
}

export const ACTION_LABELS = {
  Created: 'Created',
  AdminAdjusted: 'Admin adjusted',
  Redeemed: 'Redeemed',
  Refunded: 'Refunded / restored',
  StatusChanged: 'Status changed',
  Voided: 'Voided'
}

export function money (value) {
  return Number(value || 0).toLocaleString(undefined, { style: 'currency', currency: 'USD' })
}

export function signedMoney (value) {
  const n = Number(value || 0)
  return (n > 0 ? '+' : n < 0 ? '−' : '') + money(Math.abs(n))
}

export async function copyText (text, notify) {
  try {
    await navigator.clipboard.writeText(text)
    notify?.success('Code copied')
  } catch (e) {
    notify?.warning('Copy failed. Select the code and copy it manually.')
  }
}
