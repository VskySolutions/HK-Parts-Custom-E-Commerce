/*
 * Storefront shopping cart state (WO-28).
 *
 * A single module-level reactive cart shared by the header badge, the cart page
 * and checkout. Guests are identified by a client-generated session id persisted
 * in localStorage ('storefront.cartSession'); it is created on first use and sent
 * as `sessionId` on every cart call. Cart CRUD returns the recalculated CartDto
 * and is written straight back to `cart`; coupon apply/remove are keyed by the
 * cart id and return no cart, so the cart is re-fetched afterwards.
 */
import { ref, computed } from 'vue'
import { cartApi } from 'modules/storefront/api'

const SESSION_KEY = 'storefront.cartSession'

// RFC-4122 v4 uuid; prefers the native crypto implementation (mirrors boot/axios).
function uuidV4 () {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
    return crypto.randomUUID()
  }
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
    const r = (Math.random() * 16) | 0
    const v = c === 'x' ? r : (r & 0x3) | 0x8
    return v.toString(16)
  })
}

// Read the persisted guest session id, generating and storing one if absent.
function readSessionId () {
  try {
    let id = localStorage.getItem(SESSION_KEY)
    if (!id) {
      id = uuidV4()
      localStorage.setItem(SESSION_KEY, id)
    }
    return id
  } catch (e) {
    // localStorage unavailable — fall back to an in-memory id for this page load.
    return uuidV4()
  }
}

// Shared reactive state (module singletons).
const sessionId = ref(readSessionId())
const cart = ref(null)
const loading = ref(false)
const loaded = ref(false)

const items = computed(() => cart.value?.items || [])
const itemCount = computed(() => items.value.reduce((sum, i) => sum + (i.quantity || 0), 0))
const subtotal = computed(() => cart.value?.subtotal ?? 0)
const warnings = computed(() => cart.value?.warnings || [])
const appliedCouponCode = computed(() => cart.value?.appliedCouponCode || null)
const cartId = computed(() => cart.value?.id || null)
const currencyCode = computed(() => cart.value?.currencyCode || 'USD')

export function useCart () {
  // Fetch the latest cart from the server and mirror it into the shared state.
  async function refresh () {
    loading.value = true
    try {
      cart.value = await cartApi.get(sessionId.value)
      loaded.value = true
      return cart.value
    } finally {
      loading.value = false
    }
  }

  // Load once (used by the header badge so it does not re-fetch on every mount).
  async function ensureLoaded () {
    if (!loaded.value && !loading.value) await refresh()
    return cart.value
  }

  async function addItem (payload) {
    cart.value = await cartApi.addItem(sessionId.value, payload)
    loaded.value = true
    return cart.value
  }

  async function updateItem (itemId, quantity) {
    cart.value = await cartApi.updateItem(sessionId.value, itemId, quantity)
    return cart.value
  }

  async function removeItem (itemId) {
    cart.value = await cartApi.removeItem(sessionId.value, itemId)
    return cart.value
  }

  async function clear () {
    cart.value = await cartApi.clear(sessionId.value)
    return cart.value
  }

  async function applyCoupon (code) {
    if (!cartId.value) await refresh()
    await cartApi.applyCoupon(cartId.value, code)
    return refresh()
  }

  async function removeCoupon () {
    if (!cartId.value) await refresh()
    await cartApi.removeCoupon(cartId.value)
    return refresh()
  }

  return {
    sessionId,
    cart,
    items,
    itemCount,
    subtotal,
    warnings,
    appliedCouponCode,
    cartId,
    currencyCode,
    loading,
    loaded,
    refresh,
    ensureLoaded,
    addItem,
    updateItem,
    removeItem,
    clear,
    applyCoupon,
    removeCoupon
  }
}

export default useCart
