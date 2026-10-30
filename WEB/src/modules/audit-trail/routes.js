/*
 * Audit Trail routes (WO-61). Child of the authenticated app shell.
 * The parent registers this array in router/index.js (see FRONTEND REPORT).
 */
export default [
  {
    path: 'audit-trail',
    name: 'audit-trail',
    meta: { title: 'Audit Trail', permissions: ['AuditTrail.Read'] },
    component: () => import('modules/audit-trail/pages/index.vue')
  }
]
