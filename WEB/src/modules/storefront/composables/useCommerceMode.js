/*
 * Commerce mode (REQ-INQ-001).
 *
 * The tenant either sells with payment (Standard) or collects inquiries only (InquiryOnly).
 * The public config is fetched once per session and shared as a module singleton — every /shop
 * surface branches off it, so an inquiry-only storefront never renders a payment step, a shipping
 * selector or a tax line.
 *
 * Fails open to Standard: if the endpoint is unreachable the shop keeps behaving normally rather
 * than silently turning into a catalogue.
 */
import { ref, computed } from 'vue'
import { anonApi, unwrap } from 'services/api'

const DEFAULTS = Object.freeze({
  mode: 'Standard',
  isInquiryOnly: false,
  showPrices: true,
  collectAddress: true,
  inquiryButtonLabel: 'Request a Quote',
  submitNote: null
})

const config = ref({ ...DEFAULTS })
const loaded = ref(false)
let configPromise = null

function ensureConfig () {
  if (loaded.value) return Promise.resolve(config.value)
  if (!configPromise) {
    configPromise = anonApi
      .get('/api/storefront/config/commerce')
      .then(unwrap)
      .then((c) => {
        config.value = { ...DEFAULTS, ...(c || {}) }
        loaded.value = true
        return config.value
      })
      .catch(() => {
        config.value = { ...DEFAULTS }
        loaded.value = true
        return config.value
      })
  }
  return configPromise
}

export function useCommerceMode () {
  ensureConfig()

  return {
    commerce: config,
    commerceLoaded: loaded,
    load: ensureConfig,

    // Whole tenant sells by inquiry.
    isInquiryOnly: computed(() => config.value.isInquiryOnly === true),
    // Prices are shown (always true in Standard mode; a toggle for inquiry-only tenants).
    showPrices: computed(() => config.value.showPrices !== false),
    // Ask for a postal address on the inquiry form, or contact details only.
    collectAddress: computed(() => config.value.collectAddress !== false),
    inquiryButtonLabel: computed(() => config.value.inquiryButtonLabel || DEFAULTS.inquiryButtonLabel),
    submitNote: computed(() => config.value.submitNote || null),

    // The call-to-action for a given product: its own override, else the tenant default.
    buttonLabelFor (product) {
      return (product && product.inquiryButtonLabel) || config.value.inquiryButtonLabel || DEFAULTS.inquiryButtonLabel
    },

    // Whether this product checks out as an inquiry: tenant-wide, or flagged on the product itself.
    isInquiryProduct (product) {
      return config.value.isInquiryOnly === true || !!(product && product.isInquiryOnly)
    }
  }
}
