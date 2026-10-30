/* Inquiries admin module routes (REQ-INQ-001): quote-request list + detail. */
export default [
  {
    path: 'inquiries',
    name: 'admin-inquiries',
    meta: { title: 'Inquiries', permissions: ['Inquiries.Read'] },
    component: () => import('modules/inquiries/pages/inquiries.vue')
  },
  {
    path: 'inquiries/:id',
    name: 'admin-inquiry-detail',
    meta: { title: 'Inquiry', permissions: ['Inquiries.Read'] },
    component: () => import('modules/inquiries/pages/inquiry-detail.vue')
  }
]
