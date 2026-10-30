/*
 * Storefront customer session (Pinia) — WO-112 unified authentication.
 *
 * Originally (WO-21) this was an ISOLATED session with its own token slot + axios
 * instance. WO-112 unified all roles into a single session, so this store is now a
 * thin FACADE over the admin `useAuthStore`: login/refresh/logout/token all delegate
 * to the one shared session, while customer self-service (register / verify-email /
 * password reset) keeps the customer-specific endpoints. Storefront components can
 * keep using `useCustomerAuthStore()` unchanged.
 */
import { defineStore } from 'pinia'
import { computed } from 'vue'
import { useAuthStore } from 'stores/auth'
import { customerAuthApi } from 'modules/storefront/account-api'

export const useCustomerAuthStore = defineStore('customerAuth', () => {
  const auth = useAuthStore()

  const isAuthenticated = computed(() => auth.isAuthenticated)
  // Expose the shared refresh token so the storefront's 401-refresh interceptor (boot/interceptors.js)
  // can silently refresh the unified session — same as the admin instance. Without this the storefront
  // never refreshed an expired access token and surfaced "session expired" while the user was still in.
  const refreshToken = computed(() => auth.refreshToken)
  const customer = computed(() => auth.user)
  const displayName = computed(() => {
    const u = auth.user
    if (!u) return ''
    return u.fullName || [u.firstName, u.lastName].filter(Boolean).join(' ') || u.email || ''
  })

  // Login through the unified endpoint (/api/auth/login) — one session for all roles.
  async function login (credentials) {
    return auth.login(credentials)
  }

  // Customer self-service still uses the customer-specific endpoints.
  async function register (payload) { return customerAuthApi.register(payload) }
  async function verifyEmail (token) { return customerAuthApi.verifyEmail(token) }
  async function requestPasswordReset (payload) { return customerAuthApi.requestPasswordReset(payload) }
  async function resetPassword (token, newPassword) { return customerAuthApi.resetPassword(token, newPassword) }

  async function refresh () { return auth.refresh() }

  // Merge fresh profile fields into the shared user (keeps the header name current).
  function setProfile (profile) {
    if (!profile) return
    auth._setUser({ ...(auth.user || {}), ...profile })
  }

  function logout () { return auth.logout() }
  function clearSession () { auth.clearSession() }

  return {
    isAuthenticated,
    refreshToken,
    customer,
    displayName,
    login,
    register,
    verifyEmail,
    requestPasswordReset,
    resetPassword,
    refresh,
    setProfile,
    logout,
    clearSession
  }
})
