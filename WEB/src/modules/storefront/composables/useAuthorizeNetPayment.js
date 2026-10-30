import { ref } from 'vue'
import { anonApi, unwrap } from 'services/api'

/*
 * Authorize.Net Accept.js helper for the storefront checkout.
 *
 * Authorize.Net takes the payment details on-site, but the raw card / bank account never reaches our server:
 * the Accept.js library tokenizes what the buyer entered into a single-use opaque-data nonce (dataValue) in
 * the browser, using the public API Login ID + Public Client Key. That nonce is sent to the server as the
 * checkout's paymentToken, which the Authorize.Net gateway adapter charges as opaqueData (dataDescriptor
 * COMMON.ACCEPT.INAPP.PAYMENT). The same nonce mechanism covers both instruments: tokenize() sends cardData
 * (credit/debit card) and tokenizeBank() sends bankData (ACH/eCheck), and the resulting nonce is charged the
 * same way — the checkout tells the server which instrument it is so the adapter applies the eCheck rules.
 *
 * The public config { configured, apiLoginId, clientKey, isProduction } and the Accept.js <script> are module
 * singletons (fetched/loaded once). Fields are plain inputs rendered by the checkout page — unlike Square
 * there is no hosted field to bind to a DOM element — so this composable only exposes config + tokenize*().
 */

// Public config — never carries the Transaction Key or Signature Key.
const config = ref(null)
let configPromise = null
let sdkPromise = null

async function ensureConfig () {
  if (config.value !== null) return config.value
  if (!configPromise) {
    configPromise = anonApi
      .get('/api/storefront/config/authorizenet')
      .then(unwrap)
      .then((c) => {
        config.value = c || {}
        return config.value
      })
      .catch(() => {
        // Config endpoint unavailable → treat Authorize.Net as not configured (the option degrades gracefully).
        config.value = {}
        return config.value
      })
  }
  return configPromise
}

// Load the matching Accept.js build once — the sandbox and live libraries live on different hosts.
function loadSdk (isProduction) {
  if (window.Accept) return Promise.resolve(true)
  if (!sdkPromise) {
    sdkPromise = new Promise((resolve) => {
      const s = document.createElement('script')
      s.src = isProduction
        ? 'https://js.authorize.net/v1/Accept.js'
        : 'https://jstest.authorize.net/v1/Accept.js'
      s.async = true
      s.onload = () => resolve(true)
      s.onerror = () => { sdkPromise = null; resolve(false) }
      document.head.appendChild(s)
    })
  }
  return sdkPromise
}

// Split an "MM/YY" (or "MM / YYYY") expiry into the parts Accept.js expects. Accept.js accepts a 2- or 4-digit
// year, so whatever the buyer typed after the slash is passed through as-is.
function parseExpiry (raw) {
  const parts = String(raw || '').split('/')
  const month = (parts[0] || '').trim().padStart(2, '0')
  const year = (parts[1] || '').trim()
  return { month, year }
}

// Accept.js keeps initialising asynchronously after its <script> onload fires; dispatchData called before that
// finishes returns "Accept.js is not loaded correctly." Detect that specific message so tokenize can retry.
const NOT_READY = /not loaded/i

function delay (ms) {
  return new Promise((resolve) => setTimeout(resolve, ms))
}

// One dispatchData round-trip, normalised to { token } on success or { error, notReady } on failure.
function dispatchOnce (secureData) {
  return new Promise((resolve) => {
    window.Accept.dispatchData(secureData, (response) => {
      const messages = response && response.messages
      if (messages && messages.resultCode === 'Ok' && response.opaqueData && response.opaqueData.dataValue) {
        resolve({ token: response.opaqueData.dataValue })
        return
      }
      const msg = messages && Array.isArray(messages.message) && messages.message.length
        ? messages.message[0].text
        : null
      resolve({ error: msg, notReady: !!(msg && NOT_READY.test(msg)) })
    })
  })
}

export function useAuthorizeNetPayment () {
  // Whether Authorize.Net is set up for on-site card entry (API Login ID + Public Client Key present).
  async function isConfigured () {
    const cfg = await ensureConfig()
    return !!(cfg && cfg.configured)
  }

  // Start loading Accept.js as soon as Authorize.Net is chosen, so the library has finished its own async
  // init by the time Place order tokenizes — avoids the "not loaded correctly" race on the first click.
  async function preload () {
    const cfg = await ensureConfig()
    if (cfg && cfg.configured) await loadSdk(cfg.isProduction)
  }

  // Ensure Authorize.Net is configured and Accept.js is loaded, then dispatch the given secureData (cardData
  // or bankData) with the shared not-ready retry, returning the single-use nonce. `authLabel`/`detailMsg`
  // tailor the buyer-facing errors to the instrument. Throws a buyer-facing Error on any failure.
  async function tokenizeSecureData (payload, authLabel, detailMsg) {
    const cfg = await ensureConfig()
    if (!cfg || !cfg.configured) {
      throw new Error(`${authLabel} is not fully set up. Please choose another payment method.`)
    }
    const ok = await loadSdk(cfg.isProduction)
    if (!ok || !window.Accept) {
      throw new Error('Could not load the secure payment library. Check your connection and try again.')
    }

    const secureData = { authData: { clientKey: cfg.clientKey, apiLoginID: cfg.apiLoginId }, ...payload }

    // window.Accept exists once the script's onload fires, but dispatchData can still report "not loaded
    // correctly" until the library finishes wiring up. Retry a few times with a short backoff to ride out
    // that race; any other failure (invalid details, declined) is returned as-is and not retried.
    let last = null
    for (let attempt = 0; attempt < 6; attempt++) {
      const result = await dispatchOnce(secureData)
      if (result.token) return result.token
      last = result
      if (!result.notReady) break
      await delay(300)
    }
    throw new Error((last && last.error) || detailMsg)
  }

  // Tokenize the entered card into a single-use Accept.js nonce (opaqueData.dataValue). Throws a buyer-facing
  // Error on failure (not configured, library load failure, invalid card) so the caller can show it and stop
  // placement. `card` is { number, expiry, cvc, name?, zip? }.
  async function tokenize (card) {
    const { month, year } = parseExpiry(card && card.expiry)
    const cardData = {
      cardNumber: String((card && card.number) || '').replace(/\s+/g, ''),
      month,
      year,
      cardCode: String((card && card.cvc) || '').trim()
    }
    if (card && card.zip) cardData.zip = String(card.zip).trim()
    if (card && card.name) cardData.fullName = String(card.name).trim()
    return tokenizeSecureData({ cardData }, 'Card payment', 'Please check your card details and try again.')
  }

  // Tokenize the entered bank account (ACH/eCheck) into a single-use Accept.js nonce. `bank` is
  // { accountNumber, routingNumber, nameOnAccount, accountType } where accountType is one of
  // 'checking' | 'savings' | 'businessChecking'. Same nonce/charge path as a card — the store never sees the
  // account number. Throws a buyer-facing Error on failure so the caller can show it and stop placement.
  async function tokenizeBank (bank) {
    const bankData = {
      accountNumber: String((bank && bank.accountNumber) || '').replace(/\s+/g, ''),
      routingNumber: String((bank && bank.routingNumber) || '').replace(/\s+/g, ''),
      nameOnAccount: String((bank && bank.nameOnAccount) || '').trim(),
      accountType: String((bank && bank.accountType) || 'checking').trim()
    }
    return tokenizeSecureData({ bankData }, 'Bank (ACH) payment', 'Please check your bank account details and try again.')
  }

  return { config, isConfigured, preload, tokenize, tokenizeBank }
}

export default useAuthorizeNetPayment
