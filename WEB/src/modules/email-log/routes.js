/* Email Log admin module routes. */
export default [
  {
    path: 'email-log',
    name: 'admin-email-log',
    meta: { title: 'Email Log', permissions: ['EmailLog.Read'] },
    component: () => import('modules/email-log/pages/index.vue')
  }
]
