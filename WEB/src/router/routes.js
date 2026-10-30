/*
 * Base route skeleton (WO-94 Step 7).
 *
 * - appShellRoute: the authenticated layout mounted at "/", supplying the admin URLs
 *   (/dashboard, /catalog/*, ...) as children appended in router/index.js. It owns no ""
 *   child: the bare site root is the public storefront home (modules/storefront/routes),
 *   which is registered ahead of this record. The admin front door is /dashboard.
 * - /not-authorized and the catch-all 404 are always present.
 */

export const appShellRoute = {
  path: '/',
  component: () => import('layouts/layout.vue'),
  meta: { requiresAuth: true },
  children: []
}

export const notAuthorizedRoute = {
  path: '/not-authorized',
  name: 'not-authorized',
  meta: { title: 'Not Authorized' },
  component: () => import('shared/error_not_authorized.vue')
}

export const catchAllRoute = {
  path: '/:catchAll(.*)*',
  name: 'not-found',
  meta: { title: 'Not Found' },
  component: () => import('shared/error_not_found.vue')
}

export default [appShellRoute, notAuthorizedRoute, catchAllRoute]
