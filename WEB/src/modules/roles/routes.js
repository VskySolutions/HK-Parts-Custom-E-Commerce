/*
 * Roles module routes (WO-62 / REQ-ADM-004). Child of the authenticated shell.
 * The parent registers this array in router/index.js.
 */
export default [
  {
    path: 'roles',
    name: 'roles',
    meta: { title: 'Roles', permissions: ['Roles.Read'] },
    component: () => import('modules/roles/pages/index.vue')
  },
  {
    path: 'roles/new',
    name: 'role-new',
    meta: { title: 'New role', permissions: ['Roles.Write'] },
    component: () => import('modules/roles/pages/role-detail.vue')
  },
  {
    path: 'roles/:id',
    name: 'role-detail',
    meta: { title: 'Role', permissions: ['Roles.Read'] },
    component: () => import('modules/roles/pages/role-detail.vue')
  }
]
