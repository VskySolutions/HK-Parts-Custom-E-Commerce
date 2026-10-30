/* Webhooks admin module routes (WO-118). */
export default [
  {
    path: 'webhooks',
    name: 'admin-webhooks',
    meta: { title: 'Webhooks', permissions: ['Webhooks.Read'] },
    component: () => import('modules/webhooks/pages/index.vue')
  },
  {
    // Create: the same detail page in "create mode" (must precede the :id route).
    path: 'webhooks/new',
    name: 'webhook-new',
    meta: { title: 'New webhook', permissions: ['Webhooks.Write'] },
    component: () => import('modules/webhooks/pages/webhook-detail.vue')
  },
  {
    path: 'webhooks/:id',
    name: 'webhook-detail',
    meta: { title: 'Webhook', permissions: ['Webhooks.Read'] },
    component: () => import('modules/webhooks/pages/webhook-detail.vue')
  }
]
