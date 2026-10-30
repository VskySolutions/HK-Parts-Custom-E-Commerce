/* Settings module routes (WO-94 Step 12; commerce mode REQ-INQ-001). */
export default [
  {
    path: 'settings',
    name: 'settings',
    meta: { title: 'Settings', permissions: ['Settings.Read'] },
    component: () => import('modules/settings/pages/index.vue')
  },
  {
    path: 'settings/commerce',
    name: 'settings-commerce-mode',
    meta: { title: 'Commerce Mode', permissions: ['Settings.Read'] },
    component: () => import('modules/settings/pages/commerce-mode.vue')
  }
]
