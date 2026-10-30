import { ref } from 'vue'
import { anonApi, unwrap } from 'services/api'

/*
 * Square Web Payments SDK helper for the storefront checkout.
 *
 * Square takes card details on-site: the SDK renders a hosted card field, and card.tokenize() turns the
 * entered card into a single-use nonce (source id). That nonce is sent to the server as the checkout's
 * paymentToken, which the Square gateway adapter charges via CreatePayment — the store never touches the
 * raw card number.
 *
 * The public config { configured, applicationId, locationId, isProduction } and the SDK <script> are
 * module singletons (fetched/loaded once); the card instance is per-composable-call because it binds to a
 * DOM element that unmounts with the page.
 */

// Public config (Application Id + Location Id + environment) — never carries the access token/secret.
const config = ref(null)
let configPromise = null
let sdkPromise = null

async function ensureConfig () {
  if (config.value !== null) return config.value
  if (!configPromise) {
    configPromise = anonApi
      .get('/api/storefront/config/square')
      .then(unwrap)
      .then((c) => {
        config.value = c || {}
        return config.value
      })
      .catch(() => {
        // Config endpoint unavailable → treat Square as not configured (the option degrades gracefully).
        config.value = {}
        return config.value
      })
  }
  return configPromise
}

// Load the matching Web Payments SDK once — the sandbox and live builds live on different hosts.
function loadSdk (isProduction) {
  if (window.Square) return Promise.resolve(true)
  if (!sdkPromise) {
    sdkPromise = new Promise((resolve) => {
      const s = document.createElement('script')
      s.src = isProduction
        ? 'https://web.squarecdn.com/v1/square.js'
        : 'https://sandbox.web.squarecdn.com/v1/square.js'
      s.async = true
      s.onload = () => resolve(true)
      s.onerror = () => { sdkPromise = null; resolve(false) }
      document.head.appendChild(s)
    })
  }
  return sdkPromise
}

export function useSquarePayment () {
  let payments = null
  let card = null
  let mounting = null

  // Whether Square is set up for on-site card entry (Application Id + Location Id present).
  async function isConfigured () {
    const cfg = await ensureConfig()
    return !!(cfg && cfg.configured)
  }

  // Attach the Square card field into `containerSelector` (a CSS selector for an element already in the DOM).
  // Idempotent — once attached, further calls are a no-op. Returns true when the field is ready to tokenize.
  async function mount (containerSelector) {
    if (card) return true
    if (mounting) return mounting
    mounting = (async () => {
      const cfg = await ensureConfig()
      if (!cfg || !cfg.configured) return false
      const ok = await loadSdk(cfg.isProduction)
      if (!ok || !window.Square) return false
      payments = window.Square.payments(cfg.applicationId, cfg.locationId)
      const c = await payments.card()
      await c.attach(containerSelector)
      card = c
      return true
    })()
    try {
      return await mounting
    } catch (e) {
      return false
    } finally {
      mounting = null
    }
  }

  // Tokenize the entered card into a single-use source id (nonce). Throws a buyer-facing Error on failure
  // (empty/invalid card, declined tokenization) so the caller can show it and stop placement.
  async function tokenize () {
    if (!card) throw new Error('The card form is still loading. Please wait a moment and try again.')
    const result = await card.tokenize()
    if (result && result.status === 'OK' && result.token) return result.token
    const detail = result && Array.isArray(result.errors) && result.errors.length ? result.errors[0].message : null
    throw new Error(detail || 'Please check your card details and try again.')
  }

  // Tear down the card field (e.g. when the quote is invalidated and its container unmounts), so a later
  // mount re-attaches a fresh field rather than pointing at a removed element.
  async function destroy () {
    const c = card
    card = null
    payments = null
    if (c) {
      try { await c.destroy() } catch (e) { /* ignore */ }
    }
  }

  return { config, isConfigured, mount, tokenize, destroy }
}

export default useSquarePayment
