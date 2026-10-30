/*
 * Storefront product-engagement API (WO-14 reviews, WO-58 Q&A).
 *
 * Public reads return APPROVED content only and use the ANONYMOUS instance
 * (`anonApi`) so they never trip the authenticated 401-refresh interceptor.
 * Submitting a review belongs to a signed-in customer (`customerApi` bearer
 * token); asking a question is public (`anonApi`) and carries a reCAPTCHA token.
 *
 * JSON is camelCase. Endpoints (WO-14 / WO-58):
 *   GET  /api/storefront/products/{productId}/reviews    -> { summary, reviews[] }
 *   POST /api/storefront/products/{productId}/reviews    -> { rating, title, body }
 *   GET  /api/storefront/products/{productId}/questions  -> [{ askerName, questionText, answerText, ... }]
 *   POST /api/storefront/products/{productId}/questions  -> { askerName, askerEmail, questionText, recaptchaToken }
 */
import { anonApi, customerApi, unwrap } from 'services/api'

const PRODUCTS = '/api/storefront/products'

export const reviewsApi = {
  // Approved reviews + summary { enabled, averageRating, totalCount, star1..star5 (or a breakdown) }.
  list (productId) {
    return anonApi.get(`${PRODUCTS}/${encodeURIComponent(productId)}/reviews`).then(unwrap)
  },
  // Whether the signed-in customer may review this product { canReview, reason, message } (authenticated).
  eligibility (productId) {
    return customerApi.get(`${PRODUCTS}/${encodeURIComponent(productId)}/reviews/eligibility`).then(unwrap)
  },
  // Submit a review (authenticated customer). A 403 carries the "must have purchased" message.
  submit (productId, { rating, title, body }) {
    return customerApi
      .post(`${PRODUCTS}/${encodeURIComponent(productId)}/reviews`, { rating, title, body })
      .then(unwrap)
  }
}

export const questionsApi = {
  // Approved + answered questions only.
  list (productId) {
    return anonApi.get(`${PRODUCTS}/${encodeURIComponent(productId)}/questions`).then(unwrap)
  },
  // Ask a public question. `recaptchaToken` comes from useRecaptcha().getToken('QaSubmit').
  submit (productId, { askerName, askerEmail, questionText, recaptchaToken }) {
    return anonApi
      .post(`${PRODUCTS}/${encodeURIComponent(productId)}/questions`, {
        askerName,
        askerEmail,
        questionText,
        recaptchaToken
      })
      .then(unwrap)
  }
}

export default { reviewsApi, questionsApi }
