/*
 * Storefront checkout extras API layer (WO-27 loyalty points).
 *
 * A small companion to modules/storefront/api.js for the checkout-only loyalty
 * calls. Uses the isolated storefront customer instance (`customerApi`) so the
 * signed-in buyer's bearer token flows through — reward points belong to an
 * authenticated customer. Every call unwraps the standard { success, data } envelope.
 *
 * Endpoints:
 *   GET    /api/customer/points        -> { balance, ... }              (also read by the account rewards page)
 *   POST   /api/checkout/apply-points  -> { pointsApplied, pointsDiscountAmount, remainingBalance }
 *   DELETE /api/checkout/remove-points -> (no content)
 *
 * NOTE: apply/remove are delivered by WO-27's backend slice; until it ships they
 * 404 and the checkout degrades gracefully (the loyalty UI stays hidden when the
 * balance can't be fetched, and apply/remove surface a toast on failure).
 */
import { customerApi, unwrap } from 'services/api'

const CUSTOMER_POINTS = '/api/customer/points'
const CHECKOUT = '/api/checkout'

export const pointsApi = {
  // Current reward-points balance for the signed-in customer: { balance, ... }.
  balance () {
    return customerApi.get(CUSTOMER_POINTS).then(unwrap)
  },
  // Redeem points against the active checkout. Body is { points }; sessionId is sent
  // defensively so the server can resolve the (session-keyed) cart. Returns
  // { pointsApplied, pointsDiscountAmount, remainingBalance }.
  apply ({ points, sessionId = null }) {
    return customerApi.post(CHECKOUT + '/apply-points', { points, sessionId }).then(unwrap)
  },
  // Clear any redeemed points from the active checkout (DELETE with a small body, mirroring remove-coupon).
  remove ({ sessionId = null } = {}) {
    return customerApi.delete(CHECKOUT + '/remove-points', { data: { sessionId } }).then(unwrap)
  }
}

export default { pointsApi }
