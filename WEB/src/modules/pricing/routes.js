/* Pricing/promotions admin module routes (WO-115). */
export default [
  {
    path: 'promotions',
    name: 'admin-promotions',
    meta: { title: 'Promotions', permissions: ['Catalog.Read'] },
    component: () => import('modules/pricing/pages/index.vue')
  },
  {
    path: 'promotions/discounts/new',
    name: 'discount-new',
    meta: { title: 'New discount', permissions: ['Catalog.Write'] },
    component: () => import('modules/pricing/pages/discount-detail.vue')
  },
  {
    path: 'promotions/discounts/:id',
    name: 'discount-detail',
    meta: { title: 'Discount', permissions: ['Catalog.Read'] },
    component: () => import('modules/pricing/pages/discount-detail.vue')
  },
  {
    path: 'promotions/coupons/new',
    name: 'coupon-new',
    meta: { title: 'New coupon', permissions: ['Catalog.Write'] },
    component: () => import('modules/pricing/pages/coupon-detail.vue')
  },
  {
    path: 'promotions/coupons/:id',
    name: 'coupon-detail',
    meta: { title: 'Coupon', permissions: ['Catalog.Read'] },
    component: () => import('modules/pricing/pages/coupon-detail.vue')
  }
]
