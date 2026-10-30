/*
 * Authentication store (Pinia, setup style) — WO-94 Step 6.
 *
 * State is SEEDED FROM LocalStorage so a page reload keeps the session. Tokens,
 * user, permissions and the must-change-password flag are all persisted.
 *
 * GRACEFUL DEGRADE (important): the current backend issues role-only JWTs with
 * NO granular permissions. When the permissions array is empty but the user's
 * role is SuperAdmin/TenantAdmin, we treat them as having ALL permissions so
 * permission-gated routes/menus keep working against the role-only backend.
 */
import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { authApi } from 'services/api'
import { decodeJwtPermissions } from 'services/jwt'
import { STORAGE_KEYS, getItem, setItem, removeItem } from 'services/storage'

// Roles that implicitly hold every permission while the backend has none.
export const FULL_ACCESS_ROLES = Object.freeze(['SuperAdmin', 'TenantAdmin'])

// The storefront-customer system role. A "staff" (admin) session is any authenticated user carrying at
// least one role OTHER than this. Customers carry only the Customer role — and sessions issued before the
// role existed carry none — so both resolve to isStaff === false.
export const CUSTOMER_ROLE = 'Customer'

export const useAuthStore = defineStore('auth', () => {
  // ---- State (seeded from LocalStorage) ------------------------------------
  const token = ref(getItem(STORAGE_KEYS.TOKEN, null))
  const refreshToken = ref(getItem(STORAGE_KEYS.REFRESH_TOKEN, null))
  const expiresAtUtc = ref(getItem(STORAGE_KEYS.EXPIRES_AT, null))
  const user = ref(getItem(STORAGE_KEYS.USER, null)) // { id, username, email, fullName, roles[], ...site }
  const mustChangePassword = ref(getItem(STORAGE_KEYS.MUST_CHANGE_PASSWORD, false) === true)
  const permissions = ref(getItem(STORAGE_KEYS.PERMISSIONS, []) || [])

  // ---- Getters -------------------------------------------------------------
  const isAuthenticated = computed(() => !!token.value)
  // Backend returns a roles[] array (multi-role User/Role/UserRole model).
  const roles = computed(() => (Array.isArray(user.value?.roles) ? user.value.roles : []))
  const role = computed(() => roles.value[0] ?? null)
  // Staff (admin) session = authenticated with any role other than the storefront Customer role.
  const isStaff = computed(() => roles.value.some((r) => r !== CUSTOMER_ROLE))

  // True when we should fall back to role-based full access.
  const hasFullAccess = computed(
    () =>
      permissions.value.length === 0 &&
      roles.value.some((r) => FULL_ACCESS_ROLES.includes(r))
  )

  function hasPermission (permission) {
    if (!permission) return true
    if (hasFullAccess.value) return true
    return permissions.value.includes(permission)
  }

  function hasAnyPermission (list) {
    if (!Array.isArray(list) || list.length === 0) return true
    if (hasFullAccess.value) return true
    return list.some((p) => permissions.value.includes(p))
  }

  // ---- Internal persistence helpers ----------------------------------------
  function _setTokens ({ accessToken, refreshToken: rt, expiresAtUtc: exp } = {}) {
    if (accessToken !== undefined) {
      token.value = accessToken || null
      if (accessToken) setItem(STORAGE_KEYS.TOKEN, accessToken)
      else removeItem(STORAGE_KEYS.TOKEN)

      // Derive permissions straight from the freshly-issued token.
      const perms = accessToken ? decodeJwtPermissions(accessToken) : []
      permissions.value = perms
      setItem(STORAGE_KEYS.PERMISSIONS, perms)
    }

    if (rt !== undefined) {
      refreshToken.value = rt || null
      if (rt) setItem(STORAGE_KEYS.REFRESH_TOKEN, rt)
      else removeItem(STORAGE_KEYS.REFRESH_TOKEN)
    }

    if (exp !== undefined) {
      expiresAtUtc.value = exp || null
      if (exp) setItem(STORAGE_KEYS.EXPIRES_AT, exp)
      else removeItem(STORAGE_KEYS.EXPIRES_AT)
    }
  }

  function _setUser (u) {
    user.value = u || null
    if (u) setItem(STORAGE_KEYS.USER, u)
    else removeItem(STORAGE_KEYS.USER)
  }

  function setMustChangePassword (value) {
    mustChangePassword.value = !!value
    setItem(STORAGE_KEYS.MUST_CHANGE_PASSWORD, !!value)
  }

  // Apply a full auth response ({ accessToken, expiresAtUtc, refreshToken, user }).
  function _applySession (data) {
    if (!data) return
    _setTokens({
      accessToken: data.accessToken ?? null,
      refreshToken: data.refreshToken ?? null,
      expiresAtUtc: data.expiresAtUtc ?? null
    })
    if (data.user !== undefined) _setUser(data.user)
    if (data.mustChangePassword !== undefined) {
      setMustChangePassword(!!data.mustChangePassword)
    }
  }

  // ---- Actions -------------------------------------------------------------
  async function login (credentials) {
    const data = await authApi.login(credentials)
    _applySession(data)
    // If the token carried permissions, they are already applied via _setTokens.
    return data
  }

  // No /me endpoint yet: reconstruct the profile from the JWT and merge it into
  // the persisted user (keeping any site/tenant fields already present).
  async function loadProfile () {
    if (!token.value) return null
    const profile = authApi.profile(token.value)
    if (profile) {
      _setUser({ ...(user.value || {}), ...profile })
      if (Array.isArray(profile.permissions) && profile.permissions.length) {
        permissions.value = profile.permissions
        setItem(STORAGE_KEYS.PERMISSIONS, profile.permissions)
      }
    }
    return profile
  }

  async function refresh () {
    if (!refreshToken.value) {
      throw new Error('No refresh token available')
    }
    const data = await authApi.refresh(refreshToken.value)
    _applySession(data)
    return data
  }

  async function logout () {
    try {
      if (refreshToken.value) await authApi.logout(refreshToken.value)
    } catch (e) {
      /* best-effort server logout */
    } finally {
      clearSession()
    }
  }

  async function logoutAll () {
    try {
      if (refreshToken.value) await authApi.logoutAll(refreshToken.value)
    } catch (e) {
      /* best-effort */
    } finally {
      clearSession()
    }
  }

  // Wipe the session from both memory and LocalStorage.
  function clearSession () {
    token.value = null
    refreshToken.value = null
    expiresAtUtc.value = null
    user.value = null
    permissions.value = []
    mustChangePassword.value = false

    removeItem(STORAGE_KEYS.TOKEN)
    removeItem(STORAGE_KEYS.REFRESH_TOKEN)
    removeItem(STORAGE_KEYS.EXPIRES_AT)
    removeItem(STORAGE_KEYS.USER)
    removeItem(STORAGE_KEYS.PERMISSIONS)
    removeItem(STORAGE_KEYS.MUST_CHANGE_PASSWORD)
  }

  // Called once on app mount (App.vue). Re-hydrates derived state that may be
  // missing (e.g. permissions decoded from an existing token).
  function initialize () {
    if (token.value && permissions.value.length === 0) {
      const perms = decodeJwtPermissions(token.value)
      if (perms.length) {
        permissions.value = perms
        setItem(STORAGE_KEYS.PERMISSIONS, perms)
      }
    }
    return isAuthenticated.value
  }

  return {
    // state
    token,
    refreshToken,
    expiresAtUtc,
    user,
    mustChangePassword,
    permissions,
    // getters
    isAuthenticated,
    role,
    roles,
    isStaff,
    hasFullAccess,
    hasPermission,
    hasAnyPermission,
    // actions
    login,
    loadProfile,
    refresh,
    logout,
    logoutAll,
    initialize,
    clearSession,
    setMustChangePassword,
    _setTokens,
    _setUser,
    _applySession
  }
})
