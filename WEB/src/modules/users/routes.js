/*
 * Users module routes (WO-62 / REQ-ADM-004). Child of the authenticated shell.
 * The parent registers this array in router/index.js.
 */
export default [
  {
    path: 'users',
    name: 'users',
    meta: { title: 'Users', permissions: ['Users.Read'] },
    component: () => import('modules/users/pages/index.vue')
  },
  {
    // Create: the same detail page in "create mode" (must precede the :id route).
    path: 'users/new',
    name: 'user-new',
    meta: { title: 'New user', permissions: ['Users.Write'] },
    component: () => import('modules/users/pages/user-detail.vue')
  },
  {
    path: 'users/:id',
    name: 'user-detail',
    meta: { title: 'User', permissions: ['Users.Read'] },
    component: () => import('modules/users/pages/user-detail.vue')
  }
]
