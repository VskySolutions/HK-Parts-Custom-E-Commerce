import { ref } from 'vue'
import { anonApi, unwrap } from 'services/api'

// Module-singleton public reCAPTCHA config { siteKey, version, perFormSettings } fetched once (WO-108).
const config = ref(null)
let configPromise = null
let scriptPromise = null

async function ensureConfig () {
  if (config.value !== null) return config.value
  if (!configPromise) {
    configPromise = anonApi
      .get('/api/storefront/config/recaptcha')
      .then(unwrap)
      .then((c) => {
        config.value = c || {}
        return config.value
      })
      .catch(() => {
        // If the config endpoint is unavailable, treat reCAPTCHA as not configured (forms proceed).
        config.value = {}
        return config.value
      })
  }
  return configPromise
}

function isFormEnabled (cfg, formType) {
  return !!(cfg && cfg.siteKey && cfg.perFormSettings && cfg.perFormSettings[formType])
}

// Load the Google v3 script once, only when reCAPTCHA is actually configured (WO-108).
function loadScript (siteKey) {
  if (!scriptPromise) {
    scriptPromise = new Promise((resolve) => {
      const s = document.createElement('script')
      s.src = `https://www.google.com/recaptcha/api.js?render=${siteKey}`
      s.async = true
      s.defer = true
      s.onload = () => resolve()
      s.onerror = () => resolve()
      document.head.appendChild(s)
    })
  }
  return scriptPromise
}

export function useRecaptcha () {
  // Returns a token for the given form type, or null when reCAPTCHA is disabled/unconfigured (no-op).
  async function getToken (formType) {
    const cfg = await ensureConfig()
    if (!isFormEnabled(cfg, formType)) return null

    // v3 (invisible) token generation. v2 checkbox/invisible widget rendering is a follow-up enhancement.
    const version = cfg.version
    if (version === 'V3' || version === 2) {
      await loadScript(cfg.siteKey)
      const grecaptcha = window.grecaptcha
      if (!grecaptcha || !grecaptcha.execute) return null
      return new Promise((resolve) => {
        grecaptcha.ready(() => {
          grecaptcha
            .execute(cfg.siteKey, { action: formType })
            .then((token) => resolve(token))
            .catch(() => resolve(null))
        })
      })
    }
    return null
  }

  return { getToken, ensureConfig, config }
}

export default useRecaptcha
