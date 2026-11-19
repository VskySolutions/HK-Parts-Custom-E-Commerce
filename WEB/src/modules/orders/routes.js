/* Orders admin module routes (WO-114): order list + detail, returns (RMA) list + detail + settings. */

/** Standalone (no admin shell) pages: the printable invoice, opened in its own tab like the PHP admin's. */
export const ordersStandaloneRoutes = [
  {
    path: '/orders/:id/invoice',
    name: 'admin-order-invoice',
    meta: { title: 'Invoice', requiresAuth: true, permissions: ['Orders.Read'] },
    component: () => import('modules/orders/pages/order-invoice.vue')
  }
]

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
    path: 'orders/:id/return',
    name: 'admin-order-quick-return',
    meta: { title: 'Apply Credit', permissions: ['Orders.Read'] },
    component: () => import('modules/orders/pages/order-quick-return.vue')
  },
  {
    path: 'returns',
    name: 'admin-rmas',
    meta: { title: 'Returns', permissions: ['Orders.Read'] },
    component: () => import('modules/orders/pages/rmas.vue')
  },
  {
    // Before 'returns/:id' so "settings" is never read as an RMA id.
    path: 'returns/settings',
    name: 'admin-return-settings',
    meta: { title: 'Return Settings', permissions: ['Orders.Read'] },
    component: () => import('modules/orders/pages/return-settings.vue')
  },
  {
    path: 'returns/:id',
    name: 'admin-rma-detail',
    meta: { title: 'Return', permissions: ['Orders.Read'] },
    component: () => import('modules/orders/pages/rma-detail.vue')
  }
]
