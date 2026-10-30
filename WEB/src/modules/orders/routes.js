/* Orders admin module routes (WO-114): order list + detail, returns (RMA) list + detail. */
export default [
  {
    path: 'orders',
    name: 'admin-orders',
    meta: { title: 'Orders', permissions: ['Orders.Read'] },
    component: () => import('modules/orders/pages/orders.vue')
  },
  {
    path: 'orders/:id',
    name: 'admin-order-detail',
    meta: { title: 'Order', permissions: ['Orders.Read'] },
    component: () => import('modules/orders/pages/order-detail.vue')
  },
  {
    path: 'returns',
    name: 'admin-rmas',
    meta: { title: 'Returns', permissions: ['Orders.Read'] },
    component: () => import('modules/orders/pages/rmas.vue')
  },
  {
    path: 'returns/:id',
    name: 'admin-rma-detail',
    meta: { title: 'Return', permissions: ['Orders.Read'] },
    component: () => import('modules/orders/pages/rma-detail.vue')
  }
]
