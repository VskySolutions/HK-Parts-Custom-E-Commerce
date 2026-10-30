/*
 * Widget module routes (WO-94 Step 12) — the template other feature modules
 * copy. Children of the authenticated shell.
 */
export default [
  {
    path: 'widgets',
    name: 'widgets',
    meta: { title: 'Widgets' },
    component: () => import('modules/widget/pages/index.vue')
  },
  {
    // Create: the same detail page in "create mode" (must precede the :id route).
    path: 'widgets/new',
    name: 'widget-new',
    meta: { title: 'New widget' },
    component: () => import('modules/widget/pages/detail.vue')
  },
  {
    path: 'widgets/:id',
    name: 'widget-detail',
    meta: { title: 'Widget' },
    component: () => import('modules/widget/pages/detail.vue')
  }
]
