<template>
  <div>
    <div v-if="loading && !product" class="inv-card inv-center"><q-spinner color="green-6" size="28px" /></div>

    <!-- Not found -->
    <div v-else-if="!product" class="inv-card inv-center inv-not-found">
      <div class="inv-not-found__icon">❌</div>
      <p v-if="loadError">{{ loadError }}</p>
      <p v-else>Product not found</p>
      <router-link class="inv-btn inv-btn--green inv-not-found__btn" :to="{ name: 'inventory-scanner-scan' }">Scan Another Product</router-link>
    </div>

    <template v-else>
      <!-- Product card -->
      <div class="inv-card">
        <div class="inv-product-image">
          <img v-if="product.imageUrl" :src="$media(product.imageUrl)" :alt="product.name">
          <span v-else>No Image</span>
        </div>
        <span class="inv-badge" :class="product.isPublished ? 'inv-badge--active' : 'inv-badge--inactive'">
          {{ product.isPublished ? 'Active' : 'Inactive' }}
        </span>
        <div class="inv-product-name">{{ product.name }}</div>
        <div v-if="product.variantLabel" class="inv-product-variant">{{ product.variantLabel }}</div>
        <div class="inv-detail-grid">
          <div class="label">Model</div><div class="val">{{ product.model || '—' }}</div>
          <div class="label">SKU</div><div class="val">{{ product.sku || '—' }}</div>
          <div class="label">Barcode</div><div class="val">{{ product.barcode || '—' }}</div>
          <div class="label">MRP</div><div class="val">{{ formatMoney(product.msrp) }}</div>
          <div class="label">Selling Price</div><div class="val">{{ formatMoney(product.price) }}</div>
          <div class="label">Available Stock</div><div class="val">{{ product.qtyInStock }} pcs</div>
          <template v-if="product.storeName">
            <div class="label">Store</div><div class="val">{{ product.storeName }}{{ product.stockSplit ? ' (+ others)' : '' }}</div>
          </template>
        </div>
        <router-link class="inv-history-link" :to="historyRoute">
          <q-icon name="o_history" size="16px" /> Update history
        </router-link>
      </div>

      <!-- Update -->
      <div class="inv-card">
        <div class="inv-section-title">Update Stock &amp; Price</div>
        <div v-if="errorMsg" class="inv-error" role="alert">{{ errorMsg }}</div>

        <label class="inv-label" for="inv-qty">Stock (Pcs)</label>
        <div class="inv-stepper">
          <button type="button" :disabled="!qtyEditable || parsedQty <= 0" aria-label="Decrease" @click="step(-1)">−</button>
          <input
            id="inv-qty"
            v-model="newQty"
            type="number"
            inputmode="numeric"
            min="0"
            step="1"
            class="inv-input"
            :disabled="!qtyEditable"
            @input="onEdit"
          >
          <button type="button" :disabled="!qtyEditable" aria-label="Increase" @click="step(1)">+</button>
        </div>
        <div v-if="!product.canEditQty" class="inv-hint">{{ product.qtyLockedReason }}</div>

        <label class="inv-label" for="inv-price">Selling Price</label>
        <div class="inv-price-field">
          <span class="inv-price-field__prefix">$</span>
          <input
            id="inv-price"
            v-model="newPrice"
            type="number"
            inputmode="decimal"
            min="0"
            step="0.01"
            class="inv-input"
            :disabled="!product.canChangePrice"
            @input="onEdit"
            @blur="normalizePrice"
          >
        </div>
        <div v-if="!product.canChangePrice" class="inv-hint inv-hint--price">You don't have permission to change the selling price.</div>

        <label class="inv-label" for="inv-notes">Notes (optional)</label>
        <input
          id="inv-notes"
          v-model="notes"
          type="text"
          maxlength="500"
          class="inv-input inv-notes"
          placeholder="e.g. Cycle count, Damaged stock removed"
          @input="onEdit"
        >

        <button v-if="canSave" type="button" class="inv-btn inv-btn--green" :disabled="reviewing" @click="review">Save Update</button>
        <div v-else class="inv-hint">Nothing on this item can be changed from the scanner.</div>

        <!-- Field | Current (struck through) | Updated (green), then Confirm -->
        <div v-if="reviewing" class="inv-compare">
          <div class="inv-compare__row inv-compare__row--head"><div>Field</div><div>Current</div><div>Updated</div></div>
          <div class="inv-compare__row">
            <div class="field">Stock</div>
            <div :class="{ old: qtyChanged }">{{ product.qtyInStock }}</div>
            <div :class="{ new: qtyChanged }">{{ parsedQty }}</div>
          </div>
          <div class="inv-compare__row">
            <div class="field">Price</div>
            <div :class="{ old: priceChanged }">{{ formatMoney(product.price) }}</div>
            <div :class="{ new: priceChanged }">{{ formatMoney(parsedPrice) }}</div>
          </div>
          <button type="button" class="inv-btn inv-btn--dark inv-confirm" :disabled="saving" @click="confirm">
            {{ saving ? 'Saving…' : 'Confirm Update' }}
          </button>
        </div>
      </div>
    </template>
  </div>
</template>

<script setup>
/*
 * Barcode inventory scanner — product screen (PHP product-detail.blade.php, same layout). Addressed by
 * product id (+ ?variantId) from the lookup; ?code / ?source / ?matchedBy are passed through to the
 * audit row. Stock stepper + price + notes → "Save Update" validates and shows the current-vs-updated
 * review → "Confirm Update" posts with the values the user SAW (expectedQty / expectedPrice):
 *   200 → success screen · 409 → someone else changed it: refresh, keep the user's inputs
 *   400 → server message inline · 403 → permission message · network → retry message
 * The price field is read-only without price permission (server: Catalog module), and an unchanged price
 * is sent as null so qty-only users never trip the price check.
 */
import { ref, computed, watch, nextTick } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { inventoryScannerApi, formatMoney, errorText, ScanSource } from '../api'

const route = useRoute()
const router = useRouter()

const productId = computed(() => String(route.params.productId || ''))
const variantId = computed(() => route.query.variantId || null)

const product = ref(null)
const loading = ref(false)
const loadError = ref('')
const newQty = ref(0)
const newPrice = ref('')
const notes = ref('')
const reviewing = ref(false)
const saving = ref(false)
const errorMsg = ref('')

const qtyEditable = computed(() => !!product.value?.canEditQty)
const canSave = computed(() => qtyEditable.value || !!product.value?.canChangePrice)
const parsedQty = computed(() => Number(newQty.value))
const parsedPrice = computed(() => Number(newPrice.value))
const cents = (v) => Math.round(Number(v || 0) * 100)
const qtyChanged = computed(() => !!product.value && parsedQty.value !== product.value.qtyInStock)
const priceChanged = computed(() => !!product.value && cents(parsedPrice.value) !== cents(product.value.price))
const historyRoute = computed(() => ({
  name: 'inventory-scanner-history',
  params: { productId: productId.value },
  query: variantId.value ? { variantId: variantId.value } : {}
}))

function fill (p, { keepInputs = false } = {}) {
  product.value = p
  if (!keepInputs) {
    newQty.value = p.qtyInStock
    newPrice.value = p.price == null ? '0.00' : Number(p.price).toFixed(2)
    notes.value = ''
  }
}

async function load () {
  loading.value = true
  product.value = null
  loadError.value = ''
  reviewing.value = false
  errorMsg.value = ''
  try {
    fill(await inventoryScannerApi.getProduct(productId.value, variantId.value))
  } catch (err) {
    product.value = null
    const status = err?.response?.status
    if (status === 403) loadError.value = "You don't have permission to use the inventory scanner."
    else if (status !== 404 && status !== 400) loadError.value = errorText(err)
  } finally {
    loading.value = false
  }
}

function step (delta) {
  newQty.value = Math.max(0, (parseInt(newQty.value, 10) || 0) + delta)
  onEdit()
}

function normalizePrice () {
  if (newPrice.value !== '' && !Number.isNaN(parsedPrice.value)) newPrice.value = parsedPrice.value.toFixed(2)
}

// Any edit after "Save Update" hides the review so what gets confirmed is what's shown.
function onEdit () {
  reviewing.value = false
  errorMsg.value = ''
}

function validate () {
  const qty = parsedQty.value
  const price = parsedPrice.value
  if (newQty.value === '' || !Number.isInteger(qty) || qty < 0 || qty > 1000000) return 'Stock must be a whole number of 0 or more.'
  if (newPrice.value === '' || Number.isNaN(price) || price < 0) return 'Price must be a number of 0 or more.'
  if (notes.value.length > 500) return 'Notes must be 500 characters or fewer.'
  if (!qtyChanged.value && !priceChanged.value) return 'No changes to save'
  return ''
}

function review () {
  errorMsg.value = validate()
  if (errorMsg.value) return
  reviewing.value = true
  nextTick(() => document.querySelector('.inv-confirm')?.scrollIntoView({ behavior: 'smooth', block: 'end' }))
}

function queryValue (name, allowed) {
  const v = route.query[name]
  return allowed.includes(v) ? v : null
}

async function confirm () {
  if (saving.value) return
  saving.value = true
  errorMsg.value = ''
  const p = product.value
  try {
    const log = await inventoryScannerApi.update({
      productId: p.productId,
      productVariantId: p.productVariantId || null,
      expectedQty: p.qtyInStock,
      expectedPrice: Number(p.price || 0),
      newQty: parsedQty.value,
      newPrice: priceChanged.value ? Math.round(parsedPrice.value * 100) / 100 : null,
      notes: notes.value.trim() || null,
      scannedCode: route.query.code ? String(route.query.code).slice(0, 100) : null,
      source: queryValue('source', Object.values(ScanSource)) || ScanSource.Manual,
      matchedBy: queryValue('matchedBy', ['Sku', 'Barcode'])
    })
    router.replace({ name: 'inventory-scanner-success', params: { id: log.id } })
  } catch (err) {
    reviewing.value = false
    const status = err?.response?.status
    const data = err?.response?.data || {}
    if (status === 409) {
      errorMsg.value = `This product was changed by someone else (stock is now ${data.currentQty}, price ${formatMoney(data.currentPrice)}). Review and try again.`
      if (data.product) fill({ ...data.product, canChangePrice: p.canChangePrice }, { keepInputs: true })
      else await refreshKeepingInputs()
    } else if (status === 403) {
      errorMsg.value = "You don't have permission to change the selling price."
    } else if (status === 404) {
      product.value = null
    } else if (!err?.response) {
      errorMsg.value = 'Network error, please try again.'
    } else {
      // 400 (validation / no change / qty locked); 401 is handled by the auth interceptor → login.
      errorMsg.value = errorText(err)
    }
  } finally {
    saving.value = false
  }
}

async function refreshKeepingInputs () {
  try {
    fill(await inventoryScannerApi.getProduct(productId.value, variantId.value), { keepInputs: true })
  } catch (e) { /* keep the stale card; the message already tells the user to review */ }
}

watch([productId, variantId], () => { if (productId.value) load() }, { immediate: true })
</script>

<style scoped lang="scss">
.inv-center { text-align: center; }
.inv-not-found {
  padding: 40px 20px;
  &__icon { font-size: 40px; margin-bottom: 12px; }
  p { color: #666; font-size: 14px; margin-bottom: 20px; }
  &__btn { display: inline-flex; width: auto; padding: 12px 28px; border-radius: 8px; }
}
.inv-product-name { font-size: 17px; font-weight: 700; margin: 10px 0 4px; }
.inv-product-variant { font-size: 13px; color: #666; margin-bottom: 6px; }
.inv-detail-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  row-gap: 8px;
  column-gap: 12px;
  margin-top: 10px;
  font-size: 13px;
  .label { color: #888; }
  .val { font-weight: 600; color: #222; word-break: break-word; }
}
.inv-history-link {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  margin-top: 14px;
  min-height: 44px;
  font-size: 13px;
  color: #4c9a2a;
  text-decoration: none;
}
.inv-stepper {
  display: flex;
  gap: 10px;
  margin-bottom: 6px;
  button {
    width: 52px;
    height: 46px;
    border: 1px solid #ddd;
    border-radius: 8px;
    background: #f4f5f7;
    font-size: 22px;
    cursor: pointer;
    &:disabled { cursor: not-allowed; color: #bbb; }
  }
  .inv-input { flex: 1; text-align: center; font-size: 18px; font-weight: 600; padding: 10px; }
}
.inv-hint { font-size: 12px; color: #b26a00; margin: 2px 0 14px; }
.inv-hint--price { margin-top: -10px; }
.inv-stepper + .inv-label, .inv-hint + .inv-label { margin-top: 14px; }
.inv-price-field {
  position: relative;
  margin-bottom: 16px;
  &__prefix { position: absolute; left: 12px; top: 50%; transform: translateY(-50%); font-size: 16px; color: #666; }
  .inv-input { padding-left: 28px; font-size: 16px; }
}
.inv-notes { margin-bottom: 20px; }
.inv-compare {
  margin-top: 16px;
  border-top: 1px solid #f0f0f0;
  padding-top: 8px;
  &__row {
    display: grid;
    grid-template-columns: 1fr 1fr 1fr;
    padding: 8px 0;
    font-size: 13px;
    border-bottom: 1px solid #f0f0f0;
    .field { color: #888; }
    .old { color: #999; text-decoration: line-through; }
    .new { color: #4c9a2a; font-weight: 700; }
    &--head { color: #888; font-size: 12px; font-weight: 600; }
  }
}
.inv-confirm { margin-top: 12px; }
</style>
