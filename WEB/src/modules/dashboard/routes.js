/* Dashboard module routes (WO-94 Step 12). Child of the authenticated shell. */
export default [
  {
    path: 'dashboard',
    name: 'dashboard',
    meta: { title: 'Dashboard' },
    component: () => import('modules/dashboard/pages/index.vue')
  }
]
