/* eBay channel admin routes: Catalog → eBay Listings, and eBay Settings. */
export default [
  {
    path: 'settings/ebay',
    name: 'ebay-settings',
    meta: { title: 'eBay Settings', permissions: ['Ebay.Read'] },
    component: () => import('modules/ebay/pages/settings.vue')
  },
  {
    path: 'catalog/ebay-listings',
    name: 'ebay-listings',
    meta: { title: 'eBay Listings', permissions: ['Ebay.Read'] },
    component: () => import('modules/ebay/pages/listings.vue')
  },
  {
    // Create wizard — static segment, must precede the :id route.
    path: 'catalog/ebay-listings/new',
    name: 'ebay-listing-new',
    meta: { title: 'New eBay listing', permissions: ['Ebay.Write'] },
    component: () => import('modules/ebay/pages/listing-new.vue')
  },
  {
    path: 'catalog/ebay-listings/:id',
    name: 'ebay-listing-detail',
    meta: { title: 'eBay listing', permissions: ['Ebay.Read'] },
    component: () => import('modules/ebay/pages/listing-detail.vue')
  }
]
