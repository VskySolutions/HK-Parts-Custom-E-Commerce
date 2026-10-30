/*
 * Vue Router 4 factory (WO-94 Step 7).
 *
 * Assembles routes from each feature module (modules/<name>/routes) and applies
 * a 4-step navigation guard:
 *   (1) requiresAuth + no token          -> /auth/login (with ?redirect)
 *   (2) authenticated on an /auth/* route -> /dashboard (staff) or the storefront (customers)
 *   (3) mustChangePassword               -> /change-password
 *   (4) deepest meta.permissions unmet    -> notify + redirect home for the role
 *
 * "/" is the public storefront landing page; the admin front door is /dashboard.
 *
 * (423 setupRequired -> /setup is handled by the axios response interceptor.)
 */
import { createRouter, createWebHistory, createWebHashHistory } from 'vue-router'
import { useAuthStore } from 'stores/auth'
import { notifyNegative } from 'composables/useNotify'
import { appShellRoute, notAuthorizedRoute, catchAllRoute } from './routes'

// Feature modules
import authRoutes from 'modules/auth/routes'
import dashboardRoutes from 'modules/dashboard/routes'
import widgetRoutes from 'modules/widget/routes'
import storesRoutes from 'modules/stores/routes'
import currenciesRoutes from 'modules/currencies/routes'
import emailTemplatesRoutes from 'modules/email-templates/routes'
import brandingRoutes from 'modules/branding/routes'
import settingsRoutes from 'modules/settings/routes'
import integrationCredentialsRoutes from 'modules/integration-credentials/routes'
import usersRoutes from 'modules/users/routes'
import rolesRoutes from 'modules/roles/routes'
import catalogRoutes from 'modules/catalog/routes'
import ordersRoutes from 'modules/orders/routes'
import inquiriesRoutes from 'modules/inquiries/routes'
import pricingRoutes from 'modules/pricing/routes'
import shippingRoutes from 'modules/shipping/routes'
import reportsRoutes from 'modules/reports/routes'
import taxRoutes from 'modules/tax/routes'
import customersRoutes from 'modules/customers/routes'
import customerGroupsRoutes from 'modules/customer-groups/routes'
import taxExemptionRoutes from 'modules/tax-exemption/routes'
import storefrontRoutes from 'modules/storefront/routes'
import emailLogRoutes from 'modules/email-log/routes'
import adminAlertsRoutes from 'modules/admin-alerts/routes'
import webhooksRoutes from 'modules/webhooks/routes'
// Phase 5 admin modules
import reviewsRoutes from 'modules/reviews/routes'
import productQaRoutes from 'modules/product-qa/routes'
import loyaltyRoutes from 'modules/loyalty/routes'
import subscriptionsRoutes from 'modules/subscriptions/routes'
import cmsRoutes from 'modules/cms/routes'
import cmsContentRoutes from 'modules/cms-content/routes'
import cmsDynamicRoutes from 'modules/cms-dynamic/routes'
import cmsSeoRoutes from 'modules/cms-seo/routes'
import analyticsRoutes from 'modules/analytics/routes'
import logViewerRoutes from 'modules/log-viewer/routes'
import auditTrailRoutes from 'modules/audit-trail/routes'
import operationalReportsRoutes from 'modules/reports/operational-routes'

// Append every app-module child route under the authenticated shell.
const moduleChildren = [
  ...dashboardRoutes,
  ...widgetRoutes,
  ...storesRoutes,
  ...currenciesRoutes,
  ...emailTemplatesRoutes,
  ...brandingRoutes,
  ...settingsRoutes,
  ...integrationCredentialsRoutes,
  ...usersRoutes,
  ...rolesRoutes,
  ...catalogRoutes,
  ...ordersRoutes,
  ...inquiriesRoutes,
  ...pricingRoutes,
  ...shippingRoutes,
  ...reportsRoutes,
  ...taxRoutes,
  ...customersRoutes,
  ...customerGroupsRoutes,
  ...taxExemptionRoutes,
  ...emailLogRoutes,
  ...adminAlertsRoutes,
  ...webhooksRoutes,
  ...reviewsRoutes,
  ...productQaRoutes,
  ...loyaltyRoutes,
  ...subscriptionsRoutes,
  ...cmsRoutes,
  ...cmsContentRoutes,
  ...cmsDynamicRoutes,
  ...cmsSeoRoutes,
  ...analyticsRoutes,
  ...logViewerRoutes,
  ...auditTrailRoutes,
  ...operationalReportsRoutes
]
appShellRoute.children.push(...moduleChildren)

const routes = [
  ...authRoutes, // /auth/*, /setup, /change-password (own layout)
  // The public customer storefront (own layout, no auth) owns the landing page at "/" and the
  // /shop/* pages. It MUST precede appShellRoute: both are mounted at "/", so whichever is
  // registered first wins the bare root.
  ...storefrontRoutes,
  appShellRoute,
  notAuthorizedRoute,
  catchAllRoute
]

function deepestPermissions (to) {
  for (let i = to.matched.length - 1; i >= 0; i--) {
    const perms = to.matched[i].meta && to.matched[i].meta.permissions
    if (perms && perms.length) return perms
  }
  return null
}

export default function () {
  const createHistory =
    process.env.VUE_ROUTER_MODE === 'history' ? createWebHistory : createWebHashHistory

  const Router = createRouter({
    scrollBehavior: () => ({ left: 0, top: 0 }),
    routes,
    history: createHistory(process.env.VUE_ROUTER_BASE)
  })

  Router.beforeEach((to) => {
    const auth = useAuthStore()
    const isAuthed = auth.isAuthenticated
    const requiresAuth = to.matched.some((r) => r.meta && r.meta.requiresAuth)
    const isAuthArea = to.path.startsWith('/auth')

    // (1) Protected route without a session.
    if (requiresAuth && !isAuthed) {
      const redirect = to.fullPath && to.fullPath !== '/' ? to.fullPath : undefined
      return { path: '/auth/login', query: redirect ? { redirect } : undefined }
    }

    // (1b) Non-staff (storefront customers) must NEVER reach the admin shell — whatever admin URL they
    // open (dashboard included), send them to the storefront. Staff = any role other than Customer.
    // change-password is excluded so a forced password change (rule 3) can't ping-pong.
    if (isAuthed && requiresAuth && !auth.isStaff && to.name !== 'change-password') {
      return { name: 'shop-home' }
    }

    // (2) Already signed in but visiting the auth area — staff → dashboard, customers → storefront.
    if (isAuthed && isAuthArea) {
      return auth.isStaff ? { path: '/dashboard' } : { name: 'shop-home' }
    }

    // (3) Forced password change.
    if (
      isAuthed &&
      auth.mustChangePassword &&
      to.name !== 'change-password' &&
      to.name !== 'setup'
    ) {
      return { path: '/change-password' }
    }

    // (4) Permission gate on the deepest matched record. Bounce staff to the admin dashboard
    // (which carries no permissions of its own, so this can't loop) — "/" is the storefront.
    const requiredPerms = deepestPermissions(to)
    if (requiredPerms && !auth.hasAnyPermission(requiredPerms)) {
      notifyNegative('You are not authorized to view this page.')
      return auth.isStaff ? { path: '/dashboard' } : { name: 'shop-home' }
    }

    return true
  })

  return Router
}
