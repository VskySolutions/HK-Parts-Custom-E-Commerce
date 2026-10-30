/*
 * Customer Groups admin routes (WO-22). Full-page list + routed create/edit detail (auto-save),
 * matching the product/manufacturer pattern. Gated on Users.Read like the rest of Access Management.
 */
export default [
  {
    path: 'customer-groups',
    name: 'admin-customer-groups',
    meta: { title: 'Customer Groups', permissions: ['Users.Read'] },
    component: () => import('modules/customer-groups/pages/index.vue')
  },
  {
    path: 'customer-groups/new',
    name: 'admin-customer-group-new',
    meta: { title: 'New customer group', permissions: ['Users.Write'] },
    component: () => import('modules/customer-groups/pages/detail.vue')
  },
  {
    path: 'customer-groups/:id',
    name: 'admin-customer-group-detail',
    meta: { title: 'Customer group', permissions: ['Users.Read'] },
    component: () => import('modules/customer-groups/pages/detail.vue')
  }
]
