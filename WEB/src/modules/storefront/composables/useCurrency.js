/*
 * Storefront currency selection + formatting (WO-26).
 *
 * Fetches the enabled display currencies (GET /api/storefront/currencies) once
 * and holds the buyer's selection in localStorage ('storefront.currency').
 *
 * Conversion model: every price the storefront receives is expressed in the base
 * currency (the CartDto/checkout totals default to USD and the base currency has
 * rate 1). Each StorefrontCurrencyDto.rate is the exchange rate relative to that
 * base, so `format(amount)` renders `amount * selectedRate` in the selected
 * currency. It is intentionally resilient: with no currencies loaded it falls
 * back to formatting the raw amount as USD.
 */
import { ref, computed } from 'vue'
import { currencyApi } from 'modules/storefront/api'

const CURRENCY_KEY = 'storefront.currency'

function readStored () {
  try {
    return localStorage.getItem(CURRENCY_KEY) || null
  } catch (e) {
    return null
  }
}

function writeStored (code) {
  try {
    if (code) localStorage.setItem(CURRENCY_KEY, code)
  } catch (e) {
    /* ignore quota / privacy-mode errors — selection is best-effort */
  }
}

// Shared reactive state (module singletons).
const currencies = ref([])
const selectedCode = ref(readStored())
const loading = ref(false)
const loaded = ref(false)

// The active currency: the stored/selected one, else the base, else the first.
const selected = computed(
  () =>
    currencies.value.find((c) => c.code === selectedCode.value) ||
    currencies.value.find((c) => c.isBase) ||
    currencies.value[0] ||
    null
)

export function useCurrency () {
  async function load () {
    if (loaded.value || loading.value) return currencies.value
    loading.value = true
    try {
      const list = await currencyApi.list()
      currencies.value = Array.isArray(list) ? list : []
      loaded.value = true
      // If the stored selection is no longer offered, fall back to base/first.
      if (!currencies.value.some((c) => c.code === selectedCode.value)) {
        const def = currencies.value.find((c) => c.isBase) || currencies.value[0]
        selectedCode.value = def ? def.code : null
      }
    } catch (e) {
      // Currency endpoint unavailable — leave the list empty; format() degrades to USD.
      currencies.value = []
    } finally {
      loading.value = false
    }
    return currencies.value
  }

  function select (code) {
    selectedCode.value = code
    writeStored(code)
  }

  // Convert a base-currency amount into the selected currency and format it.
  function format (amount) {
    if (amount === null || amount === undefined || amount === '') return '—'
    const n = Number(amount)
    if (Number.isNaN(n)) return '—'

    const cur = selected.value
    const rawRate = cur && cur.rate != null ? Number(cur.rate) : 1
    const rate = Number.isFinite(rawRate) && rawRate > 0 ? rawRate : 1
    const converted = n * rate
    const code = cur ? cur.code : 'USD'

    try {
      return new Intl.NumberFormat(undefined, { style: 'currency', currency: code }).format(converted)
    } catch (e) {
      // Non-ISO / custom currency code — fall back to the symbol + fixed decimals.
      const symbol = cur && cur.symbol ? cur.symbol : ''
      return symbol + converted.toFixed(2)
    }
  }

  return { currencies, selected, selectedCode, loading, loaded, load, select, format }
}

export default useCurrency
