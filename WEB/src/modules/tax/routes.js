/* Tax admin module routes (WO-120). */
export default [
  {
    path: 'tax',
    name: 'admin-tax',
    meta: { title: 'Tax', permissions: ['Settings.Read'] },
    component: () => import('modules/tax/pages/index.vue')
  }
]
