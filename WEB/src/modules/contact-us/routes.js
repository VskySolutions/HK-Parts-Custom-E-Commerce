
export default [
  {
    path: 'contact-us-enquiries',
    name: 'contact-us-enquiries',
    meta: { title: 'Contact Us Enquiries', permissions: ['Users.Read'] },
    component: () => import('modules/contact-us/pages/index.vue')
  }
]
