/*
 * Operational Reports routes (WO-60). Child of the authenticated app shell.
 *
 * NOTE: a pre-existing `modules/reports/routes.js` (WO-119 store-performance report, name
 * `admin-reports`) already mounts `/reports`. To avoid a route-path collision the parent should
 * point the existing `reportsRoutes` import at THIS file instead (see FRONTEND REPORT). Nothing
 * in the old module is edited.
 */
export default [
  {
    path: 'operational-reports',
    name: 'operational-reports',
    meta: { title: 'Reports', permissions: ['Dashboard.Read'] },
    component: () => import('modules/reports/pages/operational-reports.vue')
  }
]
