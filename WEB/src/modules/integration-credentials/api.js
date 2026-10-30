/*
 * Integrations hub APIs — three resources behind one module:
 *
 * 1. integrationCredentialApi -> per-integration credential tables (Credentials_*):
 *      GET/POST   /api/integration-credentials/{provider}
 *      GET/PUT/DELETE /api/integration-credentials/{provider}/{id}
 *    Body shape is provider-specific (driven by providers.js `fields`).
 *
 * 2. smtpAccountApi -> TenantSmtpAccountsController (/api/tenant/smtp-accounts).
 *      Passwords are write-only; a blank password on update keeps the stored one.
 *
 * 3. storageApi -> Storage-category platform settings + connectivity probe
 *      (/api/admin/settings, /api/admin/storage/test-connection). The Azure Blob
 *      connection string is a credential (Credentials_AzureBlob), managed via #1.
 */
import { api, unwrap, qsSerializer } from 'services/api'

// ---- 1. Integration credentials --------------------------------------------
const CRED_BASE = '/api/integration-credentials'

export const integrationCredentialApi = {
  list (provider) {
    return api.get(`${CRED_BASE}/${provider}`).then(unwrap)
  },
  get (provider, id) {
    return api.get(`${CRED_BASE}/${provider}/${encodeURIComponent(id)}`).then(unwrap)
  },
  create (provider, payload) {
    return api.post(`${CRED_BASE}/${provider}`, payload).then(unwrap)
  },
  update (provider, id, payload) {
    return api.put(`${CRED_BASE}/${provider}/${encodeURIComponent(id)}`, payload).then(unwrap)
  },
  remove (provider, id) {
    return api.delete(`${CRED_BASE}/${provider}/${encodeURIComponent(id)}`).then(unwrap)
  },
  // Quick active/inactive toggle from the grid (activating deactivates the integration's other rows).
  setActive (provider, id, active) {
    return api.post(`${CRED_BASE}/${provider}/${encodeURIComponent(id)}/active`, { active }).then(unwrap)
  }
}

// ---- 2. SMTP accounts -------------------------------------------------------
const SMTP_BASE = '/api/tenant/smtp-accounts'

export const smtpAccountApi = {
  list (params = {}) {
    return api.get(SMTP_BASE, { params, paramsSerializer: qsSerializer }).then(unwrap)
  },
  get (id) {
    return api.get(`${SMTP_BASE}/${id}`).then(unwrap)
  },
  create (payload) {
    return api.post(SMTP_BASE, payload).then(unwrap)
  },
  update (id, payload) {
    return api.put(`${SMTP_BASE}/${id}`, payload).then(unwrap)
  },
  remove (id) {
    return api.delete(`${SMTP_BASE}/${id}`).then(unwrap)
  },
  testSend (id, toEmail) {
    return api.post(`${SMTP_BASE}/${id}/test-send`, { toEmail }).then(unwrap)
  }
}

// ---- 3. File-storage settings ----------------------------------------------
// Provider identifiers must match the storage adapter `Name` values on the API.
export const PROVIDERS = Object.freeze({
  local: 'LocalFilesystem',
  azure: 'AzureBlobStorage'
})

const STORAGE_KEYS = Object.freeze({
  provider: 'storage.provider',
  azureContainer: 'storage.azure.container',
  cdnBaseUrl: 'storage.cdn.base-url'
})

export const storageApi = {
  async getConfig () {
    const settings = await api.get('/api/admin/settings', { params: { category: 'Storage' } }).then(unwrap)
    const map = {}
    for (const s of Array.isArray(settings) ? settings : []) map[s.key] = s.value
    return {
      provider: map[STORAGE_KEYS.provider] || PROVIDERS.local,
      azureContainer: map[STORAGE_KEYS.azureContainer] || '',
      cdnBaseUrl: map[STORAGE_KEYS.cdnBaseUrl] || ''
    }
  },
  async updateConfig ({ provider, azureContainer, cdnBaseUrl }) {
    const settings = { [STORAGE_KEYS.provider]: provider }
    if (provider === PROVIDERS.azure) {
      settings[STORAGE_KEYS.azureContainer] = azureContainer || ''
      settings[STORAGE_KEYS.cdnBaseUrl] = cdnBaseUrl || ''
    }
    await api.put('/api/admin/settings', { settings }).then(unwrap)
    return storageApi.getConfig()
  },
  testConnection () {
    return api.post('/api/admin/storage/test-connection').then(unwrap)
  }
}

// ---- 4. reCAPTCHA (singleton config) ---------------------------------------
const RECAPTCHA_BASE = '/api/tenant/recaptcha'

export const recaptchaApi = {
  get () {
    return api.get(RECAPTCHA_BASE).then(unwrap)
  },
  update (payload) {
    return api.put(RECAPTCHA_BASE, payload).then(unwrap)
  }
}

export default integrationCredentialApi
