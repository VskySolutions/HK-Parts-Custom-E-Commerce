/*
 * Storefront module routes (WO-19).
 *
 * Default-exports an ARRAY with ONE parent route: the PUBLIC storefront shell mounted at
 * "/" (its own storefront_layout, not the authenticated app shell). Children are public —
 * no meta.requiresAuth and no meta.permissions.
 *
 * The storefront home is the site landing page and lives at the bare root "/"; every other
 * storefront page keeps its "/shop/*" URL, and the legacy "/shop" root redirects to "/".
 * Link to the landing page by NAME ({ name: 'shop-home' }), never by hardcoding a path.
 *
 * The parent router (router/index.js) should spread this into the TOP-LEVEL `routes` array
 * AHEAD of appShellRoute — both are mounted at "/", so registration order decides which one
 * owns the bare root. It must not go into appShellRoute.children, because it brings its own
 * layout and must not require authentication.
 */
import { getStoredToken } from 'services/storage'

// Route guard for the authenticated customer account area. WO-112 unified auth into
// a single session, so it reads the shared token and redirects to the unified login.
function requireCustomer (to, from, next) {
  if (getStoredToken()) next()
  else next({ path: '/auth/login', query: { redirect: to.fullPath } })
}

export default [
  {
    path: '/',
    component: () => import('layouts/storefront_layout.vue'),
    children: [
      // The landing page: the bare site root, NOT /shop.
      {
        path: '',
        name: 'shop-home',
        meta: { title: 'Shop' },
        component: () => import('modules/storefront/pages/home.vue')
      },
      // Legacy storefront root — bookmarks and old links land on the real one.
      { path: 'shop', redirect: { name: 'shop-home' } },
      {
        path: 'shop/category/:idOrSlug',
        name: 'shop-category',
        meta: { title: 'Category' },
        component: () => import('modules/storefront/pages/category.vue')
      },
      {
        path: 'shop/product/:idOrSlug',
        name: 'shop-product',
        meta: { title: 'Product' },
        component: () => import('modules/storefront/pages/product.vue')
      },
      {
        path: 'shop/search',
        name: 'shop-search',
        meta: { title: 'Search' },
        component: () => import('modules/storefront/pages/search.vue')
      },
      {
        path: 'shop/compare',
        name: 'shop-compare',
        meta: { title: 'Compare' },
        component: () => import('modules/storefront/pages/compare.vue')
      },
      {
        path: 'shop/cart',
        name: 'shop-cart',
        meta: { title: 'Cart' },
        component: () => import('modules/storefront/pages/cart.vue')
      },
      {
        path: 'shop/wishlist',
        name: 'shop-wishlist',
        meta: { title: 'Wishlist' },
        component: () => import('modules/storefront/pages/wishlist.vue')
      },
      {
        path: 'shop/checkout',
        name: 'shop-checkout',
        meta: { title: 'Checkout' },
        component: () => import('modules/storefront/pages/checkout.vue')
      },

      // ---- CMS content pages + blog (public) — WO-54 ----------------------
      {
        path: 'shop/page/:slug',
        name: 'shop-cms-page',
        meta: { title: 'Page' },
        component: () => import('modules/storefront/pages/cms-page.vue')
      },
      {
        path: 'shop/blog',
        name: 'shop-blog',
        meta: { title: 'Blog' },
        component: () => import('modules/storefront/pages/blog-list.vue')
      },
      {
        path: 'shop/blog/:slug',
        name: 'shop-blog-post',
        meta: { title: 'Article' },
        component: () => import('modules/storefront/pages/blog-post.vue')
      },

      // ---- Customer auth (public) — WO-21 / unified login WO-112 ----------
      {
        // The single login lives at /auth/login (WO-112); /shop/login redirects there,
        // preserving any post-login return path.
        path: 'shop/login',
        name: 'shop-login',
        redirect: (to) => ({ path: '/auth/login', query: to.query })
      },
      {
        path: 'shop/register',
        name: 'shop-register',
        meta: { title: 'Create account' },
        component: () => import('modules/storefront/pages/account/register.vue')
      },
      {
        path: 'shop/verify-email',
        name: 'shop-verify-email',
        meta: { title: 'Verify email' },
        component: () => import('modules/storefront/pages/account/verify-email.vue')
      },
      {
        path: 'shop/forgot-password',
        name: 'shop-forgot-password',
        meta: { title: 'Reset password' },
        component: () => import('modules/storefront/pages/account/forgot-password.vue')
      },
      {
        path: 'shop/reset-password',
        name: 'shop-reset-password',
        meta: { title: 'Choose a new password' },
        component: () => import('modules/storefront/pages/account/reset-password.vue')
      },

      // ---- Customer account (authenticated) — WO-21 ----------------------
      {
        path: 'shop/account',
        component: () => import('modules/storefront/pages/account/account.vue'),
        beforeEnter: requireCustomer,
        children: [
          { path: '', redirect: { name: 'shop-account-profile' } },
          {
            path: 'profile',
            name: 'shop-account-profile',
            meta: { title: 'My profile' },
            component: () => import('modules/storefront/pages/account/profile.vue')
          },
          {
            path: 'addresses',
            name: 'shop-account-addresses',
            meta: { title: 'My addresses' },
            component: () => import('modules/storefront/pages/account/addresses.vue')
          },
          {
            path: 'orders',
            name: 'shop-account-orders',
            meta: { title: 'My orders' },
            component: () => import('modules/storefront/pages/account/orders.vue')
          },
          {
            path: 'inquiries',
            name: 'shop-account-inquiries',
            meta: { title: 'My requests' },
            component: () => import('modules/storefront/pages/account/inquiries.vue')
          },
          {
            path: 'tax-exemption',
            name: 'shop-account-tax-exemption',
            meta: { title: 'Tax exemption' },
            component: () => import('modules/storefront/pages/account/tax-exemption.vue')
          },
          {
            path: 'subscriptions',
            name: 'shop-account-subscriptions',
            meta: { title: 'Subscriptions' },
            component: () => import('modules/storefront/pages/account/subscriptions.vue')
          },
          {
            path: 'rewards',
            name: 'shop-account-rewards',
            meta: { title: 'Rewards' },
            component: () => import('modules/storefront/pages/account/rewards.vue')
          },
          {
            path: 'privacy',
            name: 'shop-account-privacy',
            meta: { title: 'Data & Privacy' },
            component: () => import('modules/storefront/pages/account/privacy.vue')
          }
        ]
      }
    ]
  }
]
