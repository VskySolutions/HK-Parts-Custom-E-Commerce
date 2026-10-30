/*
 * JWT helpers (WO-94 Step 5).
 *
 * Pure, dependency-free base64url decoding of a JWT payload. Never throws —
 * malformed tokens yield null / []. The current backend issues tokens that
 * carry only a "role" claim (no granular permissions), so decodeJwtPermissions
 * MUST tolerate their absence and return an empty array.
 */

/** Decode a base64url string into a UTF-8 JS string. */
function base64UrlDecode (input) {
  if (typeof input !== 'string' || input.length === 0) return ''
  // base64url -> base64
  let base64 = input.replace(/-/g, '+').replace(/_/g, '/')
  // pad to a multiple of 4
  const pad = base64.length % 4
  if (pad === 2) base64 += '=='
  else if (pad === 3) base64 += '='
  else if (pad === 1) base64 += '===' // defensive; technically invalid

  let binary
  if (typeof atob === 'function') {
    binary = atob(base64)
  } else if (typeof Buffer !== 'undefined') {
    return Buffer.from(base64, 'base64').toString('utf-8')
  } else {
    return ''
  }

  // Decode the binary string as UTF-8.
  try {
    const bytes = Uint8Array.from(binary, (c) => c.charCodeAt(0))
    return new TextDecoder('utf-8').decode(bytes)
  } catch (e) {
    return binary
  }
}

/**
 * Decode and JSON.parse the payload (second) segment of a JWT.
 * @returns {object|null} the claims object, or null if the token is malformed.
 */
export function decodeJwtPayload (token) {
  if (typeof token !== 'string') return null
  const parts = token.split('.')
  if (parts.length < 2) return null
  try {
    const json = base64UrlDecode(parts[1])
    if (!json) return null
    return JSON.parse(json)
  } catch (e) {
    return null
  }
}

// Claim keys under which permissions may live, including common .NET namespaced
// forms. Checked in order.
const PERMISSION_CLAIM_KEYS = [
  'permissions',
  'permission',
  'perms',
  'scope',
  'scopes',
  'http://schemas.vsky.com/claims/permissions'
]

function coerceToArray (value) {
  if (value == null) return []
  if (Array.isArray(value)) return value.map(String).filter(Boolean)
  if (typeof value === 'string') {
    const trimmed = value.trim()
    if (!trimmed) return []
    // Support JSON-encoded arrays as well as space/comma separated scopes.
    if (trimmed.startsWith('[')) {
      try {
        const parsed = JSON.parse(trimmed)
        return Array.isArray(parsed) ? parsed.map(String).filter(Boolean) : []
      } catch (e) {
        /* fall through to delimiter split */
      }
    }
    return trimmed.split(/[\s,]+/).map((s) => s.trim()).filter(Boolean)
  }
  return []
}

/**
 * Extract the permissions array from a JWT.
 *
 * Tolerates tokens that carry ONLY a "role" claim (returns []). Callers apply
 * the graceful-degrade rule (role-based full access) in the auth store.
 *
 * @returns {string[]} de-duplicated permission strings (possibly empty).
 */
export function decodeJwtPermissions (token) {
  const payload = decodeJwtPayload(token)
  if (!payload) return []

  const collected = []
  for (const key of PERMISSION_CLAIM_KEYS) {
    if (key in payload) {
      collected.push(...coerceToArray(payload[key]))
    }
  }

  // De-duplicate while preserving order.
  return Array.from(new Set(collected))
}

/**
 * Convenience: read the role claim (string) from a JWT, if present.
 * Handles the common ".NET" role claim URI as well as plain "role"/"roles".
 */
export function decodeJwtRole (token) {
  const payload = decodeJwtPayload(token)
  if (!payload) return null
  const role =
    payload.role ??
    payload.roles ??
    payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role']
  if (Array.isArray(role)) return role[0] ?? null
  return role ?? null
}

/** Returns true when the JWT `exp` claim is in the past. */
export function isJwtExpired (token, skewSeconds = 0) {
  const payload = decodeJwtPayload(token)
  if (!payload || typeof payload.exp !== 'number') return false
  const nowSec = Math.floor(Date.now() / 1000)
  return payload.exp <= nowSec + skewSeconds
}

export default {
  decodeJwtPayload,
  decodeJwtPermissions,
  decodeJwtRole,
  isJwtExpired
}
