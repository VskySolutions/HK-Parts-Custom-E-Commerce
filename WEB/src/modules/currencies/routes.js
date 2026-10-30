/* Currencies module routes (WO-94 Step 12). */
export default [
  {
    path: 'currencies',
    name: 'currencies',
    meta: { title: 'Currencies', permissions: ['Currencies.Read'] },
    component: () => import('modules/currencies/pages/index.vue')
  },
  {
    path: 'currencies/new',
    name: 'currency-new',
    meta: { title: 'New currency', permissions: ['Currencies.Write'] },
    component: () => import('modules/currencies/pages/currency-detail.vue')
  },
  {
    path: 'currencies/:id',
    name: 'currency-detail',
    meta: { title: 'Currency', permissions: ['Currencies.Read'] },
    component: () => import('modules/currencies/pages/currency-detail.vue')
  }
]
