/* Catalog module routes (WO-15): products (+ detail), categories, manufacturers. */
export default [
  {
    path: 'catalog/products',
    name: 'catalog-products',
    meta: { title: 'Products', permissions: ['Catalog.Read'] },
    component: () => import('modules/catalog/pages/products.vue')
  },
  {
    // Create: the same detail page in "create mode" (must precede the :id route).
    path: 'catalog/products/new',
    name: 'catalog-product-new',
    meta: { title: 'New product', permissions: ['Catalog.Write'] },
    component: () => import('modules/catalog/pages/product-detail.vue')
  },
  {
    // Bulk import/export (WO-124) — static segment, must precede the :id route.
    path: 'catalog/products/import-export',
    name: 'catalog-product-import-export',
    meta: { title: 'Import & Export', permissions: ['Catalog.Write'] },
    component: () => import('modules/catalog/pages/product-import-export.vue')
  },
  {
    path: 'catalog/products/:id',
    name: 'catalog-product-detail',
    meta: { title: 'Product', permissions: ['Catalog.Read'] },
    component: () => import('modules/catalog/pages/product-detail.vue')
  },
  {
    path: 'catalog/inventory',
    name: 'catalog-inventory',
    meta: { title: 'Inventory', permissions: ['Catalog.Read'] },
    component: () => import('modules/catalog/pages/inventory.vue')
  },
  {
    path: 'catalog/categories',
    name: 'catalog-categories',
    meta: { title: 'Categories', permissions: ['Catalog.Read'] },
    component: () => import('modules/catalog/pages/categories.vue')
  },
  {
    // Create: the same detail page in "create mode" (must precede the :id route).
    path: 'catalog/categories/new',
    name: 'catalog-category-new',
    meta: { title: 'New category', permissions: ['Catalog.Write'] },
    component: () => import('modules/catalog/pages/category-detail.vue')
  },
  {
    path: 'catalog/categories/:id',
    name: 'catalog-category-detail',
    meta: { title: 'Category', permissions: ['Catalog.Read'] },
    component: () => import('modules/catalog/pages/category-detail.vue')
  },
  {
    path: 'catalog/manufacturers',
    name: 'catalog-manufacturers',
    meta: { title: 'Manufacturers', permissions: ['Catalog.Read'] },
    component: () => import('modules/catalog/pages/manufacturers.vue')
  },
  {
    // Create: the same detail page in "create mode" (must precede the :id route).
    path: 'catalog/manufacturers/new',
    name: 'catalog-manufacturer-new',
    meta: { title: 'New manufacturer', permissions: ['Catalog.Write'] },
    component: () => import('modules/catalog/pages/manufacturer-detail.vue')
  },
  {
    path: 'catalog/manufacturers/:id',
    name: 'catalog-manufacturer-detail',
    meta: { title: 'Manufacturer', permissions: ['Catalog.Read'] },
    component: () => import('modules/catalog/pages/manufacturer-detail.vue')
  },
  {
    path: 'catalog/attributes',
    name: 'catalog-attributes',
    meta: { title: 'Attributes', permissions: ['Catalog.Read'] },
    component: () => import('modules/catalog/pages/attributes.vue')
  },
  {
    path: 'catalog/attributes/product/new',
    name: 'product-attribute-new',
    meta: { title: 'New product attribute', permissions: ['Catalog.Write'] },
    component: () => import('modules/catalog/pages/product-attribute-detail.vue')
  },
  {
    path: 'catalog/attributes/product/:id',
    name: 'product-attribute-detail',
    meta: { title: 'Product attribute', permissions: ['Catalog.Read'] },
    component: () => import('modules/catalog/pages/product-attribute-detail.vue')
  },
  {
    path: 'catalog/attributes/specification/new',
    name: 'spec-attribute-new',
    meta: { title: 'New specification attribute', permissions: ['Catalog.Write'] },
    component: () => import('modules/catalog/pages/spec-attribute-detail.vue')
  },
  {
    path: 'catalog/attributes/specification/:id',
    name: 'spec-attribute-detail',
    meta: { title: 'Specification attribute', permissions: ['Catalog.Read'] },
    component: () => import('modules/catalog/pages/spec-attribute-detail.vue')
  }
]
