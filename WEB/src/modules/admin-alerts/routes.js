/* Admin Alerts module route. */
export default [
  {
    path: 'alerts',
    name: 'admin-alerts',
    meta: { title: 'Admin Alerts', permissions: ['Alerts.Read'] },
    component: () => import('modules/admin-alerts/pages/index.vue')
  }
]
