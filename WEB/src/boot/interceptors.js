/*
 * Response interceptor boot file (WO-94 Step 4).
 *
 * Attaches the RESPONSE interceptor to the authenticated `http` instance:
 *   - 423 { setupRequired: true }  -> route to /setup.
 *   - 401 (non auth-endpoint, has refresh token) -> single-flight silent
 *     refresh, then retry the original request once. If refresh fails, wipe the
 *     session and redirect to /auth/login (preserving the attempted URL).
 *
 * Kept separate from boot/axios.js so the auth-store / router coupling lives in
 * exactly one place and axios.js stays importable anywhere.
 */
import { http, http3 } from 'src/boot/axios'
import { useAuthStore } from 'stores/auth'
import { useCustomerAuthStore } from 'stores/customerAuth'
import { parseUtc } from 'src/utils/datetime'

function isAuthEndpoint (url = '') {
  return (
    url.includes('/api/auth/login') ||
    url.includes('/api/auth/refresh') ||
    url.includes('/api/auth/logout')
  )
}

// Auth/refresh endpoints that must NOT themselves trigger a customer refresh loop.
function isCustomerAuthEndpoint (url = '') {
  return url.includes('/api/customer/auth/') || url.includes('/api/auth/refresh')
}

export default ({ router }) => {
  // Ensures parallel 401s trigger only ONE refresh call.
  let refreshPromise = null
  let proactiveRefreshPromise = null

  // Proactive refresh (checkout fix): authenticated calls to [AllowAnonymous] endpoints (e.g.
  // /api/checkout/quote|place) never receive a 401 when the access token is expired — the backend just
  // treats the caller as a guest (CurrentUserService.UserId = null), so the reactive 401-refresh below
  // never fires. Refresh a stale access token BEFORE sending, so a logged-in shopper is recognised at
  // checkout. Registered after axios.js's token-attach interceptor, so it runs first (axios request
  // interceptors execute LIFO) and the freshly-stored token is the one attached.
  async function ensureFreshToken (config) {
    const url = config.url || ''
    if (isAuthEndpoint(url) || isCustomerAuthEndpoint(url)) return config
    const auth = useAuthStore()
    const exp = parseUtc(auth.expiresAtUtc)
    const expired = !!exp && exp.getTime() <= Date.now()
    if (auth.token && auth.refreshToken && expired) {
      try {
        if (!proactiveRefreshPromise) {
          proactiveRefreshPromise = auth.refresh().finally(() => { proactiveRefreshPromise = null })
        }
        await proactiveRefreshPromise
      } catch (e) {
        // Session truly expired: proceed unauthenticated; a protected endpoint 401s into the reactive path.
      }
    }
    return config
  }
  http.interceptors.request.use(ensureFreshToken)
  http3.interceptors.request.use(ensureFreshToken)

  http.interceptors.response.use(
    (response) => response,
    async (error) => {
      const original = error.config || {}
      const status = error.response ? error.response.status : null
      const data = error.response ? error.response.data : null

      // First-time setup gate.
      if (status === 423 && data && data.setupRequired) {
        if (router.currentRoute.value.path !== '/setup') {
          router.push('/setup')
        }
        return Promise.reject(error)
      }

      const auth = useAuthStore()

      const shouldRefresh =
        status === 401 &&
        !original._retry &&
        !isAuthEndpoint(original.url || '') &&
        !!auth.refreshToken

      if (shouldRefresh) {
        original._retry = true
        try {
          if (!refreshPromise) {
            refreshPromise = auth.refresh().finally(() => {
              refreshPromise = null
            })
          }
          await refreshPromise
          // Re-issue: the request interceptor re-reads the fresh token from
          // storage, so no manual header patching is required.
          return http(original)
        } catch (refreshError) {
          auth.clearSession()
          const redirect = router.currentRoute.value.fullPath
          router.push({
            path: '/auth/login',
            query: redirect && redirect !== '/' ? { redirect } : undefined
          })
          return Promise.reject(refreshError)
        }
      }

      return Promise.reject(error)
    }
  )

  // ---- Storefront customer instance: 401 -> refresh -> retry, else sign out --
  let customerRefreshPromise = null

  http3.interceptors.response.use(
    (response) => response,
    async (error) => {
      const original = error.config || {}
      const status = error.response ? error.response.status : null
      const customer = useCustomerAuthStore()

      const shouldRefresh =
        status === 401 &&
        !original._retry &&
        !isCustomerAuthEndpoint(original.url || '') &&
        !!customer.refreshToken

      if (shouldRefresh) {
        original._retry = true
        try {
          if (!customerRefreshPromise) {
            customerRefreshPromise = customer.refresh().finally(() => {
              customerRefreshPromise = null
            })
          }
          await customerRefreshPromise
          return http3(original)
        } catch (refreshError) {
          customer.clearSession()
          // Only bounce to login from the PROTECTED customer area (account pages). Browsing the public
          // storefront with an expired session must silently drop to guest — never redirect to login
          // (otherwise a stale session makes opening the shop land on /auth/login). WO-112 unified login.
          const current = router.currentRoute.value
          if (current.path.startsWith('/shop/account')) {
            const redirect = current.fullPath
            router.push({
              path: '/auth/login',
              query: redirect && redirect !== '/' ? { redirect } : undefined
            })
          }
          return Promise.reject(refreshError)
        }
      }

      return Promise.reject(error)
    }
  )
}
