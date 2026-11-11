/* Customers admin module routes (WO-117). */
export default [
  {
    path: 'customers',
    name: 'admin-customers',
    meta: { title: 'Customers', permissions: ['Users.Read'] },
    component: () => import('modules/customers/pages/index.vue')
  },
  {
    // Customer blacklist (PY-10). A static path of its own, so it never collides with customers/:id.
    path: 'customer-blacklist',
    name: 'admin-customer-blacklist',
    meta: { title: 'Blocked buyers', permissions: ['Users.Read'] },
    component: () => import('modules/customers/pages/blacklist.vue')
  },
  {
    path: 'customers/:id',
    name: 'admin-customer-detail',
    meta: { title: 'Customer', permissions: ['Users.Read'] },
    component: () => import('modules/customers/pages/detail.vue')
  }
]
