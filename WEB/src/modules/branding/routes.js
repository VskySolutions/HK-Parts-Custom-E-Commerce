/* Branding module routes (WO-94 Step 12). */
export default [
  {
    path: 'branding',
    name: 'branding',
    meta: { title: 'Branding', permissions: ['Branding.Read'] },
    component: () => import('modules/branding/pages/index.vue')
  }
]
