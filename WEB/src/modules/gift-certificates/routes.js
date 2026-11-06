/* Gift Certificates admin routes (top-level menu entry). */
export default [
  {
    path: 'gift-certificates',
    name: 'gift-certificates',
    meta: { title: 'Gift Certificates', permissions: ['GiftCertificates.Manage'] },
    component: () => import('modules/gift-certificates/pages/index.vue')
  },
  {
    // Static segment: must precede the :id route.
    path: 'gift-certificates/new',
    name: 'gift-certificate-new',
    meta: { title: 'Create Gift Certificate', permissions: ['GiftCertificates.Manage'] },
    component: () => import('modules/gift-certificates/pages/new.vue')
  },
  {
    path: 'gift-certificates/:id',
    name: 'gift-certificate-detail',
    meta: { title: 'Gift Certificate', permissions: ['GiftCertificates.Manage'] },
    component: () => import('modules/gift-certificates/pages/detail.vue')
  }
]
