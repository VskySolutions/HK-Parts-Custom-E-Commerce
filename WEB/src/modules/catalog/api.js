/*
 * Catalog module API layer (WO-15).
 *
 * Wraps the admin catalog endpoints exposed by AdminProductsController,
 * AdminCategoriesController, AdminManufacturersController and
 * AdminProductAttributesController. Mirrors the widgetApi style: uses the
 * authenticated `api` instance, `qsSerializer` for query strings and unwraps
 * every response with `unwrap`.
 *
 * Enums are transported as their string names (the API registers a
 * JsonStringEnumConverter): ProductType = Simple|Grouped|WithVariants|
 * Downloadable|GiftCard, GiftCardType = Fixed|OpenAmount, ProductMediaType =
 * Image|Video.
 */
import { api, unwrap, qsSerializer } from 'services/api'

// ---- Products ---------------------------------------------------------------
export const productApi = {
  // Core CRUD
  list (params = {}) {
    return api
      .get('/api/admin/products', { params, paramsSerializer: qsSerializer })
      .then(unwrap)
  },
  get (id) {
    return api.get(`/api/admin/products/${id}`).then(unwrap)
  },
  create (payload) {
    return api.post('/api/admin/products', payload).then(unwrap)
  },
  update (id, payload) {
    return api.put(`/api/admin/products/${id}`, payload).then(unwrap)
  },
  remove (id) {
    return api.delete(`/api/admin/products/${id}`).then(unwrap)
  },

  // Variants
  generateVariants (id) {
    return api.post(`/api/admin/products/${id}/variants/generate`).then(unwrap)
  },
  updateVariant (variantId, payload) {
    return api.put(`/api/admin/products/variants/${variantId}`, payload).then(unwrap)
  },
  deleteVariant (variantId) {
    return api.delete(`/api/admin/products/variants/${variantId}`).then(unwrap)
  },

  // Assignments (replace-semantics)
  setCategories (id, categoryIds) {
    return api.put(`/api/admin/products/${id}/categories`, { categoryIds }).then(unwrap)
  },
  setAttributes (id, productAttributeIds) {
    return api.put(`/api/admin/products/${id}/attributes`, { productAttributeIds }).then(unwrap)
  },
  setTags (id, tagNames) {
    return api.put(`/api/admin/products/${id}/tags`, { tagNames }).then(unwrap)
  },
  setTierPrices (id, tiers, productVariantId = null) {
    return api.put(`/api/admin/products/${id}/tier-prices`, { productVariantId, tiers }).then(unwrap)
  },

  // Video embeds are Media-backed pictures too (added by URL); images use the picture endpoints below.
  addVideo (id, payload) {
    return api.post(`/api/admin/products/${id}/videos`, payload).then(unwrap)
  },

  // Media-library-backed pictures (WO-123): list / assign / remove product pictures.
  listPictures (id) {
    return api.get(`/api/admin/products/${id}/pictures`).then(unwrap)
  },
  assignPicture (id, payload) {
    return api.post(`/api/admin/products/${id}/pictures`, payload).then(unwrap)
  },
  removePicture (pictureId) {
    return api.delete(`/api/admin/products/pictures/${pictureId}`).then(unwrap)
  },

  // Bulk import / export (WO-124 UI over the WO-13 CSV endpoints). Export returns the raw
  // blob response so the caller can trigger a browser download; import posts a multipart file
  // and unwraps the ImportResultDto (Success / Created / Updated / Errors).
  exportCsv (params = {}) {
    return api.get('/api/admin/products/export', { params, paramsSerializer: qsSerializer, responseType: 'blob' })
  },
  importCsv (file) {
    const fd = new FormData()
    fd.append('file', file)
    return api.post('/api/admin/products/import', fd).then(unwrap)
  }
}

// ---- Inventory (per-store stock levels, AdminInventoryController) ------------
export const inventoryApi = {
  // Paged inventory levels, optionally filtered by product and/or store.
  list (params = {}) {
    return api.get('/api/admin/inventory', { params, paramsSerializer: qsSerializer }).then(unwrap)
  },
  // Every stock level held for a product across all stores (product + each variant).
  getForProduct (productId) {
    return api.get(`/api/admin/inventory/${productId}/levels`).then(unwrap)
  },
  // Create/update the level for a (product, variant?, store); LowStockThreshold optional.
  upsertLevel (payload) {
    return api.put('/api/admin/inventory/levels', payload).then(unwrap)
  }
}

// ---- Media (WO-122 two-step upload: prepare -> commit) ----------------------
export const mediaApi = {
  // Step 1: upload bytes in-memory; returns a draft with a suggested SEO file name (no DB write).
  prepare (file) {
    const fd = new FormData()
    fd.append('file', file)
    return api.post('/api/admin/media/prepare', fd).then(unwrap)
  },
  // Step 2: commit the prepared upload with reviewed metadata; returns { mediaId, publicUrl }.
  commit (payload) {
    return api.post('/api/admin/media', payload).then(unwrap)
  }
}

// ---- Categories -------------------------------------------------------------
export const categoryApi = {
  list (params = {}) {
    return api
      .get('/api/admin/categories', { params, paramsSerializer: qsSerializer })
      .then(unwrap)
  },
  tree () {
    return api.get('/api/admin/categories/tree').then(unwrap)
  },
  get (id) {
    return api.get(`/api/admin/categories/${id}`).then(unwrap)
  },
  create (payload) {
    return api.post('/api/admin/categories', payload).then(unwrap)
  },
  update (id, payload) {
    return api.put(`/api/admin/categories/${id}`, payload).then(unwrap)
  },
  remove (id) {
    return api.delete(`/api/admin/categories/${id}`).then(unwrap)
  },

  // Media-library-backed pictures (mirrors productApi): list / assign / remove category pictures.
  listPictures (id) {
    return api.get(`/api/admin/categories/${id}/pictures`).then(unwrap)
  },
  assignPicture (id, payload) {
    return api.post(`/api/admin/categories/${id}/pictures`, payload).then(unwrap)
  },
  removePicture (pictureId) {
    return api.delete(`/api/admin/categories/pictures/${pictureId}`).then(unwrap)
  }
}

// ---- Manufacturers ----------------------------------------------------------
export const manufacturerApi = {
  list (params = {}) {
    return api
      .get('/api/admin/manufacturers', { params, paramsSerializer: qsSerializer })
      .then(unwrap)
  },
  get (id) {
    return api.get(`/api/admin/manufacturers/${id}`).then(unwrap)
  },
  create (payload) {
    return api.post('/api/admin/manufacturers', payload).then(unwrap)
  },
  update (id, payload) {
    return api.put(`/api/admin/manufacturers/${id}`, payload).then(unwrap)
  },
  remove (id) {
    return api.delete(`/api/admin/manufacturers/${id}`).then(unwrap)
  }
}

// ---- Tax categories ---------------------------------------------------------
export const taxCategoryApi = {
  list (params = {}) {
    return api
      .get('/api/admin/tax-categories', { params, paramsSerializer: qsSerializer })
      .then(unwrap)
  },
  create (payload) {
    return api.post('/api/admin/tax-categories', payload).then(unwrap)
  }
}

// ---- Product attributes (global library CRUD, WO-15) ------------------------
export const productAttributeApi = {
  list (params = {}) {
    return api
      .get('/api/admin/product-attributes', { params, paramsSerializer: qsSerializer })
      .then(unwrap)
  },
  get (id) {
    return api.get(`/api/admin/product-attributes/${id}`).then(unwrap)
  },
  create (payload) {
    return api.post('/api/admin/product-attributes', payload).then(unwrap)
  },
  update (id, payload) {
    return api.put(`/api/admin/product-attributes/${id}`, payload).then(unwrap)
  },
  remove (id) {
    return api.delete(`/api/admin/product-attributes/${id}`).then(unwrap)
  }
}

// ---- Specification attributes (global library CRUD, WO-15) ------------------
export const specificationAttributeApi = {
  list (params = {}) {
    return api
      .get('/api/admin/specification-attributes', { params, paramsSerializer: qsSerializer })
      .then(unwrap)
  },
  get (id) {
    return api.get(`/api/admin/specification-attributes/${id}`).then(unwrap)
  },
  create (payload) {
    return api.post('/api/admin/specification-attributes', payload).then(unwrap)
  },
  update (id, payload) {
    return api.put(`/api/admin/specification-attributes/${id}`, payload).then(unwrap)
  },
  remove (id) {
    return api.delete(`/api/admin/specification-attributes/${id}`).then(unwrap)
  }
}

// Product-attribute display types (transported as enum names). Swatch values carry a colour;
// Custom input has no values at all — the buyer types their own, bounded by the input settings.
export const attributeDisplayTypeOptions = [
  { label: 'Dropdown', value: 'Dropdown' },
  { label: 'Button', value: 'Button' },
  { label: 'Swatch', value: 'Swatch' },
  { label: 'Custom input', value: 'CustomInput' }
]

// What a Custom input attribute accepts on the storefront.
export const attributeInputTypeOptions = [
  { label: 'Text (characters)', value: 'Text' },
  { label: 'Number', value: 'Number' }
]

// ---- Shared enum option catalogs --------------------------------------------
export const productTypeOptions = [
  { label: 'Simple', value: 'Simple' },
  { label: 'Grouped', value: 'Grouped' },
  { label: 'With variants', value: 'WithVariants' },
  { label: 'Downloadable', value: 'Downloadable' },
  { label: 'Gift card', value: 'GiftCard' }
]

export const giftCardTypeOptions = [
  { label: 'Fixed amount', value: 'Fixed' },
  { label: 'Open amount', value: 'OpenAmount' }
]

export const mediaTypeOptions = [
  { label: 'Image', value: 'Image' },
  { label: 'Video', value: 'Video' }
]

export function attributeDisplayTypeLabel (value) {
  const match = attributeDisplayTypeOptions.find((o) => o.value === value)
  return match ? match.label : value || '—'
}

export function productTypeLabel (value) {
  const match = productTypeOptions.find((o) => o.value === value)
  return match ? match.label : value || '—'
}

export default {
  productApi,
  inventoryApi,
  categoryApi,
  manufacturerApi,
  taxCategoryApi,
  productAttributeApi,
  specificationAttributeApi,
  attributeDisplayTypeOptions,
  attributeInputTypeOptions,
  attributeDisplayTypeLabel,
  productTypeOptions,
  giftCardTypeOptions,
  mediaTypeOptions,
  productTypeLabel
}
