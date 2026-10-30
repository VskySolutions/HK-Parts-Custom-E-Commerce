/*
 * Product Reviews module API resource group (WO-14).
 *
 * Admin review-moderation queue under /api/admin/product-reviews: a paged list,
 * a lightweight stats read (pending/approved counts for the header), single- and
 * bulk-moderation (Approve/Reject), and the per-product "reviews enabled" toggle.
 */
import { api, unwrap, qsSerializer } from 'services/api'

export const reviewApi = {
  // GET /api/admin/product-reviews -> PaginatedList<AdminProductReviewDto>.
  list (params = {}) {
    return api
      .get('/api/admin/product-reviews', { params, paramsSerializer: qsSerializer })
      .then(unwrap)
  },
  // GET /api/admin/product-reviews/stats -> { pendingCount, approvedCount, ... }.
  stats () {
    return api.get('/api/admin/product-reviews/stats').then(unwrap)
  },
  // POST /api/admin/product-reviews/{id}/moderate — status is 'Approved' | 'Rejected'.
  moderate (id, status) {
    return api.post(`/api/admin/product-reviews/${id}/moderate`, { status }).then(unwrap)
  },
  // POST /api/admin/product-reviews/bulk-moderate — apply one status to many reviews.
  bulkModerate (ids, status) {
    return api.post('/api/admin/product-reviews/bulk-moderate', { ids, status }).then(unwrap)
  },
  // PUT /api/admin/product-reviews/products/{productId}/reviews-enabled — per-product toggle.
  setReviewsEnabled (productId, enabled) {
    return api
      .put(`/api/admin/product-reviews/products/${productId}/reviews-enabled`, { enabled })
      .then(unwrap)
  }
}

export default reviewApi
