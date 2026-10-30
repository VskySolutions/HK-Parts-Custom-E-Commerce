/*
 * API service layer (WO-94 Step 5).
 *
 * - Re-exports the two axios instances as `api` (authenticated) and `anonApi`
 *   (anonymous), plus a default aggregate.
 * - ApiErrorCodes: a frozen enum + getApiErrorCode()/getApiErrorMessage()
 *   helpers that normalise axios failures.
 * - unwrap()/envelope(): tolerate both bare payloads and { success, data }
 *   response envelopes.
 * - Resource groups: authApi (login/refresh/logout/logoutAll/profile) and
 *   widgetApi (the CRUD template other modules copy).
 */
import qs from 'qs'
import { http, http2, http3 } from 'src/boot/axios'
import { decodeJwtPayload, decodeJwtRole, decodeJwtPermissions } from 'src/services/jwt'

export const api = http
export const anonApi = http2
// Authenticated STOREFRONT customer instance (isolated token slot; see boot/axios.js).
export const customerApi = http3

// Consistent query serialization (repeated keys for arrays, drop null/undefined).
export function qsSerializer (params) {
  return qs.stringify(params, { arrayFormat: 'repeat', skipNulls: true })
}

// ---- Media URL resolution ---------------------------------------------------
// Locally-stored files are saved as domain-less relative paths (e.g. "/uploads/x.png"), so an
// <img src> would resolve them against the SPA origin instead of the API. Prefix those with the
// API origin (API_BASE_URL). Absolute (http/https or protocol-relative, i.e. blob/CDN), data: and
// blob: URLs are already complete and returned unchanged. Registered globally as `$media`.
export function mediaUrl (url) {
  if (!url || typeof url !== 'string') return url
  if (/^(https?:)?\/\//i.test(url) || url.startsWith('data:') || url.startsWith('blob:')) return url
  const base = (process.env.API_BASE_URL || '').replace(/\/$/, '')
  if (!base) return url
  return url.startsWith('/') ? base + url : base + '/' + url
}

// ---- Error normalisation ----------------------------------------------------
export const ApiErrorCodes = Object.freeze({
  UNKNOWN: 'UNKNOWN',
  NETWORK: 'NETWORK',
  TIMEOUT: 'TIMEOUT',
  BAD_REQUEST: 'BAD_REQUEST',
  VALIDATION: 'VALIDATION',
  UNAUTHORIZED: 'UNAUTHORIZED',
  FORBIDDEN: 'FORBIDDEN',
  NOT_FOUND: 'NOT_FOUND',
  CONFLICT: 'CONFLICT',
  SETUP_REQUIRED: 'SETUP_REQUIRED',
  SERVER: 'SERVER'
})

const DEFAULT_MESSAGES = Object.freeze({
  [ApiErrorCodes.UNKNOWN]: 'Something went wrong. Please try again.',
  [ApiErrorCodes.NETWORK]: 'Cannot reach the server. Check your connection.',
  [ApiErrorCodes.TIMEOUT]: 'The request timed out. Please try again.',
  [ApiErrorCodes.BAD_REQUEST]: 'The request was invalid.',
  [ApiErrorCodes.VALIDATION]: 'Please correct the highlighted fields.',
  [ApiErrorCodes.UNAUTHORIZED]: 'Your session has expired. Please sign in again.',
  [ApiErrorCodes.FORBIDDEN]: 'You do not have permission to perform this action.',
  [ApiErrorCodes.NOT_FOUND]: 'The requested item was not found.',
  [ApiErrorCodes.CONFLICT]: 'This action conflicts with the current state.',
  [ApiErrorCodes.SETUP_REQUIRED]: 'Initial setup must be completed first.',
  [ApiErrorCodes.SERVER]: 'A server error occurred. Please try again later.'
})

/** Map an axios error to a stable ApiErrorCodes value. */
export function getApiErrorCode (error) {
  if (!error) return ApiErrorCodes.UNKNOWN
  if (error.code === 'ECONNABORTED') return ApiErrorCodes.TIMEOUT
  if (!error.response) return ApiErrorCodes.NETWORK

  const { status, data } = error.response
  // Honour an explicit backend code when present.
  const backendCode = data && (data.code || data.errorCode)
  if (typeof backendCode === 'string' && ApiErrorCodes[backendCode]) {
    return ApiErrorCodes[backendCode]
  }

  switch (status) {
    case 400: return ApiErrorCodes.BAD_REQUEST
    case 401: return ApiErrorCodes.UNAUTHORIZED
    case 403: return ApiErrorCodes.FORBIDDEN
    case 404: return ApiErrorCodes.NOT_FOUND
    case 409: return ApiErrorCodes.CONFLICT
    case 422: return ApiErrorCodes.VALIDATION
    case 423: return ApiErrorCodes.SETUP_REQUIRED
    default:
      if (status >= 500) return ApiErrorCodes.SERVER
      return ApiErrorCodes.UNKNOWN
  }
}

/** Best-effort human-readable message for an axios error. */
export function getApiErrorMessage (error) {
  const data = error && error.response && error.response.data
  if (data) {
    if (typeof data === 'string' && data.trim()) return data
    const msg = data.message || data.error || data.title || data.detail
    if (msg) return msg
    // ASP.NET ProblemDetails / ModelState validation errors.
    if (data.errors && typeof data.errors === 'object') {
      const first = Object.values(data.errors)[0]
      if (Array.isArray(first) && first.length) return first[0]
      if (typeof first === 'string') return first
    }
  }
  return DEFAULT_MESSAGES[getApiErrorCode(error)] || DEFAULT_MESSAGES[ApiErrorCodes.UNKNOWN]
}

// ---- Envelope helpers -------------------------------------------------------
const ENVELOPE_FLAGS = ['success', 'succeeded', 'isSuccess']

/** Return the inner payload, unwrapping a { success, data } envelope if present. */
export function unwrap (response) {
  const body = response && 'data' in response ? response.data : response
  if (
    body &&
    typeof body === 'object' &&
    !Array.isArray(body) &&
    'data' in body &&
    ENVELOPE_FLAGS.some((f) => f in body)
  ) {
    return body.data
  }
  return body
}

/** Wrap a payload in the canonical response envelope (useful for tests/mocks). */
export function envelope (data, success = true, message = null) {
  return { success, message, data }
}

// ---- auth resource group ----------------------------------------------------
export const authApi = {
  async login (credentials) {
    const res = await anonApi.post('/api/auth/login', credentials)
    return unwrap(res)
  },

  async refresh (refreshToken) {
    const res = await anonApi.post('/api/auth/refresh', { refreshToken })
    return unwrap(res)
  },

  async logout (refreshToken) {
    const res = await api.post('/api/auth/logout', { refreshToken })
    return unwrap(res)
  },

  // Change the signed-in user's own password (verifies the current one).
  async changePassword ({ currentPassword, newPassword }) {
    const res = await api.post('/api/auth/change-password', { currentPassword, newPassword })
    return unwrap(res)
  },

  // Start the reset flow from the sign-in page (always succeeds, even for unknown emails).
  async requestPasswordReset (email) {
    const res = await anonApi.post('/api/auth/request-password-reset', { email })
    return unwrap(res)
  },

  // Set a new password using a valid reset token from the emailed link.
  async resetPassword (token, newPassword) {
    const res = await anonApi.post('/api/auth/reset-password', { token, newPassword })
    return unwrap(res)
  },

  // No dedicated "logout everywhere" endpoint yet — same call as logout.
  async logoutAll (refreshToken) {
    return authApi.logout(refreshToken)
  },

  // No /me endpoint yet: reconstruct the profile from the JWT claims.
  profile (token) {
    const payload = decodeJwtPayload(token)
    if (!payload) return null
    return {
      id: payload.sub ?? payload.nameid ?? payload.userId ?? payload.id ?? null,
      email:
        payload.email ??
        payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress'] ??
        null,
      fullName: payload.fullName ?? payload.name ?? payload.given_name ?? null,
      role: decodeJwtRole(token),
      permissions: decodeJwtPermissions(token)
    }
  }
}

// ---- media resource group (SEO/metadata editor) -----------------------------
export const mediaApi = {
  // GET /api/admin/media/{id} -> MediaDto (id, seoFileName, altText, title, caption, description, publicUrl, …).
  get (id) {
    return api.get(`/api/admin/media/${id}`).then(unwrap)
  },
  // PUT /api/admin/media/{id} — update SEO / accessibility metadata (no re-upload).
  update (id, payload) {
    return api.put(`/api/admin/media/${id}`, payload).then(unwrap)
  }
}

// ---- widget resource group (CRUD template, WO-94 Step 12) -------------------
export const widgetApi = {
  list (params = {}) {
    return api
      .get('/api/widgets', { params, paramsSerializer: qsSerializer })
      .then(unwrap)
  },
  get (id) {
    return api.get(`/api/widgets/${id}`).then(unwrap)
  },
  create (payload) {
    return api.post('/api/widgets', payload).then(unwrap)
  },
  update (id, payload) {
    return api.put(`/api/widgets/${id}`, payload).then(unwrap)
  },
  remove (id) {
    return api.delete(`/api/widgets/${id}`).then(unwrap)
  }
}

export default {
  api,
  anonApi,
  customerApi,
  authApi,
  widgetApi,
  mediaApi,
  ApiErrorCodes,
  getApiErrorCode,
  getApiErrorMessage,
  unwrap,
  envelope,
  mediaUrl
}
