/* Integrations hub routes: the single-page hub + the SMTP account editor sub-pages. */
export default [
  {
    path: 'integrations',
    name: 'integrations',
    meta: { title: 'Integrations', permissions: ['Credentials.Read'] },
    component: () => import('modules/integration-credentials/pages/index.vue')
  },
  {
    path: 'integrations/smtp/new',
    name: 'smtp-account-new',
    meta: { title: 'New SMTP account', permissions: ['SmtpAccounts.Write'] },
    component: () => import('modules/integration-credentials/pages/smtp-account-detail.vue')
  },
  {
    path: 'integrations/smtp/:id',
    name: 'smtp-account-detail',
    meta: { title: 'SMTP account', permissions: ['SmtpAccounts.Read'] },
    component: () => import('modules/integration-credentials/pages/smtp-account-detail.vue')
  }
]
