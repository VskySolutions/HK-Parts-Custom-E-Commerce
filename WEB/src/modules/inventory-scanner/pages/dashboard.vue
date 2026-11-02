<template>
  <div>
    <router-link class="inv-btn inv-btn--green inv-scan-btn" :to="{ name: 'inventory-scanner-scan' }">
      <q-icon name="o_photo_camera" size="20px" /> Scan Barcode
    </router-link>

    <button v-if="!manualOpen" type="button" class="inv-btn inv-btn--light inv-manual-btn" @click="openManual">
      <q-icon name="o_keyboard" size="20px" /> Enter SKU Manually
    </button>

    <!-- Manual entry (also catches USB / Bluetooth handheld scanners: they type the code + Enter) -->
    <div v-else class="inv-card inv-manual-form">
      <label class="inv-label" for="inv-manual-sku">SKU Number</label>
      <input
        id="inv-manual-sku"
        ref="manualInput"
        v-model="manualCode"
        type="text"
        class="inv-input"
        placeholder="e.g. HK-12345"
        autocomplete="off"
        @keyup.enter="goManual"
        @input="manualError = ''"
      >
      <div v-if="manualError" class="inv-manual-form__error">{{ manualError }}</div>
      <div class="inv-manual-form__actions">
        <button type="button" class="inv-btn inv-btn--grey" @click="closeManual">Cancel</button>
        <button type="button" class="inv-btn inv-btn--green" :disabled="looking" @click="goManual">
          {{ looking ? 'Checking…' : 'Go' }}
        </button>
      </div>
    </div>

    <div class="inv-stats">
      <div class="inv-card inv-stat">
        <div class="inv-stat__label"><q-icon name="o_inventory_2" color="brown-4" /> Products Updated Today</div>
        <div class="inv-stat__value">{{ dash ? dash.productsUpdatedToday : '—' }}</div>
      </div>
      <div class="inv-card inv-stat">
        <div class="inv-stat__label"><q-icon name="o_schedule" color="grey-7" /> Last Update</div>
        <div class="inv-stat__value inv-stat__value--sm">
          {{ dash ? (dash.lastUpdateUtc ? timeAgo(dash.lastUpdateUtc) : 'No updates yet') : '—' }}
        </div>
      </div>
    </div>

    <div class="inv-section-title">Recent Inventory Updates</div>
    <div class="inv-update-list">
      <div v-if="loading && !dash" class="inv-update-list__empty"><q-spinner color="green-6" size="24px" /></div>
      <template v-else-if="dash && dash.recentUpdates.length">
        <div v-for="u in dash.recentUpdates" :key="u.id" class="inv-update-row">
          <div class="inv-update-row__info">
            <div class="inv-update-row__name">
              {{ u.productName }}<span v-if="u.variantLabel" class="inv-update-row__variant"> — {{ u.variantLabel }}</span>
            </div>
            <div class="inv-update-row__meta">
              SKU: {{ u.sku || '—' }} · {{ shortDate(u.createdOnUtc) }}<template v-if="u.adminName"> · {{ u.adminName }}</template>
            </div>
          </div>
          <!-- +N green, −N red, 0 grey (price-only change) -->
          <div class="inv-update-row__qty" :class="deltaClass(u)" :title="u.action === 'PriceUpdate' ? 'Price change only' : ''">
            {{ u.qtyChange > 0 ? '+' + u.qtyChange : u.qtyChange }}
          </div>
          <router-link class="inv-view-btn" :to="viewRoute(u)">View</router-link>
        </div>
      </template>
      <div v-else class="inv-update-list__empty">No inventory updates yet. Scan a product to get started.</div>
    </div>
  </div>
</template>

<script setup>
/*
 * Barcode inventory scanner — dashboard (PHP dashboard.blade.php, 1:1 layout): Scan Barcode, Enter SKU
 * Manually (validated before navigating), Products Updated Today + Last Update, and the 10 most recent
 * updates (name, "SKU · Oct 29, 5:25am · user", +N green / −N red / 0 grey qty badge, View).
 * A handheld (keyboard-wedge) scanner works here too: scan anywhere on the page.
 */
import { ref, nextTick, onMounted } from 'vue'
import { getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'
import { parseUtc, getActiveTimeZone } from 'src/utils/datetime'
import { inventoryScannerApi, timeAgo, productRoute, ScanSource } from '../api'
import { useScannerLookup } from '../composables/useScannerLookup'
import { useHardwareScanner } from '../composables/useHardwareScanner'

const notify = useNotify()
const { open } = useScannerLookup()

const dash = ref(null)
const loading = ref(false)
const manualOpen = ref(false)
const manualInput = ref(null)
const manualCode = ref('')
const manualError = ref('')
const looking = ref(false)

async function load () {
  loading.value = true
  try {
    dash.value = await inventoryScannerApi.dashboard()
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    loading.value = false
  }
}

function openManual () {
  manualOpen.value = true
  manualCode.value = ''
  manualError.value = ''
  nextTick(() => manualInput.value?.focus())
}

function closeManual () {
  manualOpen.value = false
  manualError.value = ''
}

async function lookup (code, source) {
  if (looking.value) return
  looking.value = true
  manualError.value = ''
  try {
    const result = await open(code, source)
    if (result === 'not-found') {
      const msg = `No product found for "${code}".`
      if (source === ScanSource.Manual) manualError.value = msg
      else notify.error(msg)
    }
  } catch (err) {
    if (source === ScanSource.Manual) manualError.value = err.message
    else notify.error(err.message)
  } finally {
    looking.value = false
  }
}

function goManual () {
  const code = manualCode.value.trim()
  if (!code) { manualError.value = 'Please enter a SKU number.'; return }
  lookup(code, ScanSource.Manual)
}

useHardwareScanner((code) => lookup(code.trim(), ScanSource.Hardware))

function deltaClass (u) {
  if (u.qtyChange > 0) return 'inv-qty-up'
  if (u.qtyChange < 0) return 'inv-qty-down'
  return 'inv-update-row__qty--zero'
}

function viewRoute (u) {
  return productRoute({ productId: u.productId, productVariantId: u.productVariantId })
}

// "Oct 29, 5:25am" (PHP: Carbon format 'M j, g:ia'), in the app's active timezone.
function shortDate (value) {
  const d = parseUtc(value)
  if (!d) return ''
  const opts = { month: 'short', day: 'numeric', hour: 'numeric', minute: '2-digit', hour12: true }
  let parts
  try {
    parts = new Intl.DateTimeFormat('en-US', { ...opts, timeZone: getActiveTimeZone() || undefined }).formatToParts(d)
  } catch (e) {
    parts = new Intl.DateTimeFormat('en-US', opts).formatToParts(d)
  }
  const p = Object.fromEntries(parts.map((x) => [x.type, x.value]))
  return `${p.month} ${p.day}, ${p.hour}:${p.minute}${(p.dayPeriod || '').toLowerCase()}`
}

onMounted(load)
</script>

<style scoped lang="scss">
.inv-scan-btn { padding: 16px; font-size: 16px; margin-bottom: 12px; }
.inv-manual-btn { padding: 14px; margin-bottom: 20px; }
.inv-manual-form {
  margin-bottom: 20px;
  .inv-input { margin-bottom: 10px; }
  &__error { color: #c0392b; font-size: 12px; margin: -4px 0 10px; }
  &__actions {
    display: flex;
    gap: 10px;
    .inv-btn { flex: 1; padding: 12px; font-size: 14px; margin: 0; box-shadow: none; }
  }
}
.inv-stats {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 12px;
  margin-bottom: 20px;
  .inv-card { margin-bottom: 0; padding: 16px; border-radius: 10px; }
}
.inv-stat {
  &__label { font-size: 12px; color: #888; margin-bottom: 6px; display: flex; align-items: center; gap: 4px; }
  &__value { font-size: 20px; font-weight: 700; color: #161718; }
  &__value--sm { font-size: 14px; }
}
.inv-update-list {
  background: #fff;
  border-radius: 10px;
  overflow: hidden;
  box-shadow: 0 1px 4px rgba(0, 0, 0, 0.08);
  &__empty { padding: 28px 16px; text-align: center; color: #888; font-size: 14px; }
}
.inv-update-row {
  display: flex;
  align-items: center;
  padding: 14px 16px;
  border-bottom: 1px solid #f0f0f0;
  &:last-child { border-bottom: none; }
  &__info { flex: 1; min-width: 0; }
  &__name { font-size: 14px; font-weight: 600; color: #222; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
  &__variant { font-weight: 400; color: #666; }
  &__meta { font-size: 12px; color: #888; margin-top: 2px; }
  &__qty { font-size: 14px; font-weight: 700; margin: 0 10px; white-space: nowrap; }
  &__qty--zero { color: #999; }
}
.inv-view-btn {
  background: #f4f5f7;
  border: 1px solid #ddd;
  color: #333;
  padding: 6px 12px;
  border-radius: 6px;
  font-size: 12px;
  text-decoration: none;
  white-space: nowrap;
}
</style>
