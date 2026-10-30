/*
 * Auth module routes (WO-94 Step 12).
 *   /auth/login + /auth/forgot-password + /auth/reset-password render inside the
 *   public storefront_layout (WO-112 unified login), so admin sign-in looks like
 *   the storefront auth pages (/shop/login, /shop/register).
 *   /setup and /change-password keep the bare, chrome-less auth_layout.
 */
export default [
  {
    path: '/auth',
    component: () => import('layouts/storefront_layout.vue'),
    children: [
      { path: '', redirect: '/auth/login' },
      {
        path: 'login',
        name: 'login',
        meta: { title: 'Sign in' },
        component: () => import('modules/auth/pages/login.vue')
      },
      {
        path: 'forgot-password',
        name: 'forgot-password',
        meta: { title: 'Forgot password' },
        component: () => import('modules/auth/pages/forgot_password.vue')
      },
      {
        path: 'reset-password',
        name: 'reset-password',
        meta: { title: 'Reset password' },
        component: () => import('modules/auth/pages/reset_password.vue')
      }
    ]
  },
  {
    path: '/setup',
    component: () => import('layouts/auth_layout.vue'),
    children: [
      {
        path: '',
        name: 'setup',
        meta: { title: 'Setup' },
        component: () => import('modules/auth/pages/setup.vue')
      }
    ]
  },
  {
    path: '/change-password',
    component: () => import('layouts/auth_layout.vue'),
    children: [
      {
        path: '',
        name: 'change-password',
        meta: { requiresAuth: true, title: 'Change Password' },
        component: () => import('modules/auth/pages/change_password.vue')
      }
    ]
  }
]
