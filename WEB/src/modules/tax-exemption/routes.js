/*
 * Tax Exemption review-queue admin routes (WO-126). A single list page with an inline review drawer
 * (approve/reject); no create route — requests originate from the customer portal.
 */
export default [
  {
    path: 'tax-exemption-requests',
    name: 'admin-tax-exemption-requests',
    meta: { title: 'Tax Exemptions', permissions: ['Users.Read'] },
    component: () => import('modules/tax-exemption/pages/index.vue')
  }
]
