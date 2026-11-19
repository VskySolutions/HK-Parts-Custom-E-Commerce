/*
 * Shipping admin module API layer (WO-116): custom methods + zones + provider configuration.
 * Wraps AdminShippingMethodsController + AdminShippingZonesController + AdminShippingConfigController.
 */
import { api, unwrap, qsSerializer } from 'services/api'

const METHODS = '/api/admin/shipping-methods'
const ZONES = '/api/admin/shipping-zones'
const CONFIG = '/api/admin/shipping/config'

export const shippingMethodApi = {
  list (params = {}) { return api.get(METHODS, { params, paramsSerializer: qsSerializer }).then(unwrap) },
  get (id) { return api.get(`${METHODS}/${id}`).then(unwrap) },
  create (payload) { return api.post(METHODS, payload).then(unwrap) },
  update (id, payload) { return api.put(`${METHODS}/${id}`, payload).then(unwrap) },
  remove (id) { return api.delete(`${METHODS}/${id}`).then(unwrap) }
}

export const shippingZoneApi = {
  list (params = {}) { return api.get(ZONES, { params, paramsSerializer: qsSerializer }).then(unwrap) },
  get (id) { return api.get(`${ZONES}/${id}`).then(unwrap) },
  create (payload) { return api.post(ZONES, payload).then(unwrap) },
  update (id, payload) { return api.put(`${ZONES}/${id}`, payload).then(unwrap) },
  remove (id) { return api.delete(`${ZONES}/${id}`).then(unwrap) }
}

export const shippingConfigApi = {
  get () { return api.get(CONFIG).then(unwrap) },
  update (payload) { return api.put(CONFIG, payload).then(unwrap) }
}

/* Shipping rules (AD-20/21): add-on fees + destination restrictions, read at quote and placement. */
const RULES = '/api/admin/shipping/rules'
export const shippingRulesApi = {
  get () { return api.get(RULES).then(unwrap) },
  update (payload) { return api.put(RULES, payload).then(unwrap) }
}


export const shippingSelectionModeOptions = [
  { label: 'Manual — the customer picks from every option', value: 'Manual' },
  { label: 'Automatic — recommend the best value, customer can still change it', value: 'Automatic' }
]

export const shippingMethodTypeOptions = [
  { label: 'Flat rate', value: 'FlatRate' },
  { label: 'Weight-based', value: 'WeightBased' },
  { label: 'Price-based', value: 'PriceBased' },
  { label: 'Free shipping', value: 'FreeShipping' },
  { label: 'Size-based (Shipping Package Size)', value: 'SizeBased' }
]

/* Per-carrier presentation: icon plus, for live carriers, the integrations-hub section holding its
 * credentials (the hub deep-links via ?section=<key>). Manual has no credentials to configure. */
export const shippingCarrierMeta = {
  Manual: { icon: 'o_tune', section: null, hint: 'Rates come from the Methods tab' },
  FedEx: { icon: 'o_local_shipping', section: 'fedex', hint: 'Live rates from the FedEx Rate API' },
  DHLExpress: { icon: 'o_local_shipping', section: 'dhl', hint: 'Live rates from the MyDHL API' },
  USPS: { icon: 'o_local_shipping', section: 'usps', hint: 'Live rates from the USPS REST API' },
  UPS: { icon: 'o_local_shipping', section: 'ups', hint: 'Live rates from the UPS Rating API' },
  Freightview: { icon: 'o_rv_hookup', section: 'freightview', hint: 'LTL freight quotes from Freightview (needs product weight, freight class and dimensions)' }
}

/* Why custom methods can't quote right now, or '' when they can. They only reach checkout through the
 * Manual rate source, and only while shipping calculation is on, so both have to be on for the Methods tab
 * to be worth opening. Shared by the tab page and the Configuration panel so the two read one rule.
 * Anything not loaded yet reads as usable — an absent config shouldn't be what disables the tab. */
export function shippingMethodsBlockedReason (config) {
  if (!config) return ''
  if (config.isEnabled === false) return 'Shipping calculation is off — turn it on in Configuration to use custom methods'
  const manual = config.carriers?.find((c) => c.carrier === 'Manual')
  if (manual && !manual.isEnabled) return 'Turn on the Manual rate source in Configuration to use custom methods'
  return ''
}

export default {
  shippingMethodApi,
  shippingZoneApi,
  shippingConfigApi,
  shippingRulesApi,
  shippingMethodTypeOptions,
  shippingSelectionModeOptions,
  shippingCarrierMeta,
  shippingMethodsBlockedReason
}

/* ShipStation Custom Store (docs/shipstation-verification-requirements.md §5.1): settings (password write-only)
 * + the call log ShipStation's export / shipnotify requests leave behind. */
const SHIPSTATION = '/api/admin/shipstation'
export const shipStationApi = {
  get () { return api.get(`${SHIPSTATION}/settings`).then(unwrap) },
  update (payload) { return api.put(`${SHIPSTATION}/settings`, payload).then(unwrap) },
  activity (params = {}) { return api.get(`${SHIPSTATION}/activity`, { params, paramsSerializer: qsSerializer }).then(unwrap) }
}

export const shipStationStatusOptions = [
  { label: 'Pending', value: 'Pending', caption: 'awaiting_shipment' },
  { label: 'Processing', value: 'Processing', caption: 'awaiting_shipment' },
  { label: 'Shipped', value: 'Shipped', caption: 'shipped' },
  { label: 'Cancelled', value: 'Cancelled', caption: 'cancelled' },
  { label: 'On hold (payment under review)', value: 'OnHold', caption: 'on_hold' }
]

/* Checkout address verification (§5.2) + the P.O. box rule + the admin "Test address" box (AD-61). */
const VERIFICATION = '/api/admin/address-verification'
export const addressVerificationApi = {
  get () { return api.get(VERIFICATION).then(unwrap) },
  update (payload) { return api.put(VERIFICATION, payload).then(unwrap) },
  test (payload) { return api.post(`${VERIFICATION}/test`, payload).then(unwrap) }
}

export const verificationProviderOptions = [
  { label: 'Disabled', value: 'None' },
  { label: 'Avalara (AvaTax)', value: 'Avalara' },
  { label: 'USPS (US addresses only)', value: 'Usps' }
]

export const verificationUnavailableOptions = [
  { label: 'Allow the order and flag it as not verified', value: 'AllowAndFlag' },
  { label: 'Block checkout until the address can be verified', value: 'Block' }
]
