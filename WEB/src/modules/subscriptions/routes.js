/*
 * Subscriptions module routes (WO-49). Child of the authenticated shell; the
 * parent registers this array in router/index.js. Admin view is a read-only
 * list with row-level pause/cancel actions (no create/detail).
 */
export default [
  {
    path: 'subscriptions',
    name: 'subscriptions',
    meta: { title: 'Subscriptions', permissions: ['Orders.Read'] },
    component: () => import('modules/subscriptions/pages/index.vue')
  }
]
