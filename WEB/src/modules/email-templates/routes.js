/* Email Templates module routes (WO-94 Step 12; editor added WO-80). */
export default [
  {
    path: 'email-templates',
    name: 'email-templates',
    meta: { title: 'Email Templates', permissions: ['EmailTemplates.Read'] },
    component: () => import('modules/email-templates/pages/index.vue')
  },
  {
    // Template key is carried as a ?key= query param (not a path segment) so
    // dotted keys like "account.password-reset" survive history-mode reloads.
    path: 'email-templates/editor',
    name: 'email-template-editor',
    meta: { title: 'Email Template', permissions: ['EmailTemplates.Read'] },
    component: () => import('modules/email-templates/pages/editor.vue')
  }
]
