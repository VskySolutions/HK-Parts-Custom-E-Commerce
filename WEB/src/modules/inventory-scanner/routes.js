/*
 * Barcode inventory scanner routes (port of the PHP admin "Update Inventory" scanner, /echo/inventory):
 * dashboard → scan (camera / photo / manual SKU / handheld scanner) → product (edit stock + price,
 * review, confirm) → success; plus per-product history.
 *
 * Mounted as its OWN top-level record with the standalone mobile layout (no admin sidebar/header), like
 * the PHP pages — registered in router/index.js ahead of the admin shell. requiresAuth keeps it staff-only
 * (the global guard also bounces storefront customers). The API enforces access: the Inventory module
 * opens the scanner and saves stock; the Catalog module is also needed to change a price.
 *
 * The product screen is addressed by id, never by the scanned code (Code 39 values may contain '/', '+',
 * '%'); the code, source and match type travel in the query for the audit row.
 */
export default [
  {
    path: '/inventory-scanner',
    component: () => import('layouts/scanner_layout.vue'),
    meta: { requiresAuth: true },
    children: [
      {
        path: '',
        name: 'inventory-scanner',
        meta: {
          title: 'Update Inventory',
          scannerTitle: 'Update Inventory Dashboard',
          scannerBack: { name: 'dashboard' },
          scannerTabs: true,
          permissions: ['Catalog.Read']
        },
        component: () => import('modules/inventory-scanner/pages/dashboard.vue')
      },
      {
        path: 'scan',
        name: 'inventory-scanner-scan',
        meta: { title: 'Scan barcode', scannerBare: true, permissions: ['Catalog.Read'] },
        component: () => import('modules/inventory-scanner/pages/scan.vue')
      },
      {
        // ?variantId=&code=&source=&matchedBy=
        path: 'product/:productId',
        name: 'inventory-scanner-product',
        meta: {
          title: 'Product Details',
          scannerBack: { name: 'inventory-scanner-scan' },
          permissions: ['Catalog.Read']
        },
        component: () => import('modules/inventory-scanner/pages/product.vue')
      },
      {
        // ?variantId=
        path: 'product/:productId/history',
        name: 'inventory-scanner-history',
        meta: {
          title: 'Update History',
          scannerBack: { name: 'inventory-scanner' },
          permissions: ['Catalog.Read']
        },
        component: () => import('modules/inventory-scanner/pages/history.vue')
      },
      {
        path: 'success/:id',
        name: 'inventory-scanner-success',
        meta: { title: 'Update Successful', scannerBare: true, permissions: ['Catalog.Read'] },
        component: () => import('modules/inventory-scanner/pages/success.vue')
      }
    ]
  }
]
