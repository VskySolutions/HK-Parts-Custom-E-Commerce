/*
 * Log Viewer routes (WO-70). Child of the authenticated app shell.
 * The parent registers this array in router/index.js (see FRONTEND REPORT).
 */
export default [
  {
    path: 'logs',
    name: 'logs',
    meta: { title: 'Logs', permissions: ['Logs.Read'] },
    component: () => import('modules/log-viewer/pages/index.vue')
  }
]
