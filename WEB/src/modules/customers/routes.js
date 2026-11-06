/* Customers admin module routes (WO-117). */
export default [
  {
    path: 'customers',
    name: 'admin-customers',
    meta: { title: 'Customers', permissions: ['Users.Read'] },
    component: () => import('modules/customers/pages/index.vue')
  },
  {
    path: 'customers/:id',
    name: 'admin-customer-detail',
    meta: { title: 'Customer', permissions: ['Users.Read'] },
    component: () => import('modules/customers/pages/detail.vue')
  },
  {
    // Add customer: same page as edit, opened without an :id (only the General tab is enabled).
    path: 'customers/new',
    name: 'admin-customer-create',
    meta: { title: 'New customer', permissions: ['Users.Write'] },
    component: () => import('modules/customers/pages/detail.vue')
  }
]
