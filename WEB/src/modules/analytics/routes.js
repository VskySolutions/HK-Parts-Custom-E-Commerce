/*
 * Analytics module routes (WO-59). Child of the authenticated app shell.
 * The parent registers this array in router/index.js (see FRONTEND REPORT).
 */
export default [
  {
    path: 'analytics',
    name: 'analytics',
    meta: { title: 'Analytics', permissions: ['Dashboard.Read'] },
    component: () => import('modules/analytics/pages/index.vue')
  }
]
