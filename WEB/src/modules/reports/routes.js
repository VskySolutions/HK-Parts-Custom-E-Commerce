/* Reports admin module routes (WO-119). */
export default [
  {
    path: 'reports',
    name: 'admin-reports',
    meta: { title: 'Reports', permissions: ['Stores.Read'] },
    component: () => import('modules/reports/pages/index.vue')
  }
]
