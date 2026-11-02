<template>
  <div>
    <div v-if="product" class="inv-card inv-history-head">
      <div class="inv-history-head__name">{{ product.name }}</div>
      <div v-if="product.variantLabel" class="inv-history-head__variant">{{ product.variantLabel }}</div>
      <div class="inv-history-head__meta">SKU: {{ product.sku || '—' }} · Now {{ product.qtyInStock }} pcs · {{ formatMoney(product.price) }}</div>
    </div>

    <div class="inv-history-list">
      <div v-if="loading && !rows.length" class="inv-history-list__empty"><q-spinner color="green-6" size="24px" /></div>
      <div v-else-if="!rows.length" class="inv-history-list__empty">No scanner updates for this product yet.</div>

      <div v-for="r in rows" :key="r.id" class="inv-history-row">
        <div class="inv-history-row__top">
          <span class="inv-history-row__date">{{ formatDateTime(r.createdOnUtc) }}</span>
          <span class="inv-history-row__user">{{ r.adminName || '—' }}</span>
        </div>
        <div class="inv-history-row__changes">
          <span :class="{ 'inv-history-row__dim': r.action === 'PriceUpdate' }">
            Qty {{ r.oldQty ?? '—' }} → {{ r.newQty ?? '—' }}
            <b :class="r.qtyChange > 0 ? 'inv-qty-up' : r.qtyChange < 0 ? 'inv-qty-down' : ''">
              ({{ r.qtyChange > 0 ? '+' + r.qtyChange : r.qtyChange }})
            </b>
          </span>
          <span :class="{ 'inv-history-row__dim': r.action === 'QtyUpdate' }">
            Price {{ formatMoney(r.oldPrice) }} → {{ formatMoney(r.newPrice) }}
          </span>
        </div>
        <div v-if="r.variantLabel && !variantId" class="inv-history-row__note">{{ r.variantLabel }}</div>
        <div v-if="r.notes" class="inv-history-row__note">“{{ r.notes }}”</div>
        <div class="inv-history-row__source">{{ sourceLabel(r) }}<template v-if="r.storeName"> · {{ r.storeName }}</template></div>
      </div>
    </div>

    <div v-if="totalPages > 1" class="inv-history-pager">
      <button type="button" class="inv-btn inv-btn--light" :disabled="page <= 1 || loading" @click="go(page - 1)">Newer</button>
      <span>Page {{ page }} of {{ totalPages }}</span>
      <button type="button" class="inv-btn inv-btn--light" :disabled="page >= totalPages || loading" @click="go(page + 1)">Older</button>
    </div>
  </div>
</template>

<script setup>
/*
 * Barcode inventory scanner — per-product history (spec §3.5): paged audit rows, newest first, with
 * date, user, qty old → new (delta), price old → new, notes and how the item was scanned.
 */
import { ref, computed, watch } from 'vue'
import { useRoute } from 'vue-router'
import { useNotify } from 'composables/useNotify'
import { inventoryScannerApi, formatMoney, formatDateTime, errorText } from '../api'

const PAGE_SIZE = 20
const SOURCE_LABELS = { Camera: 'Camera scan', Gallery: 'Photo scan', Manual: 'Typed SKU', Hardware: 'Handheld scanner' }

const route = useRoute()
const notify = useNotify()

const productId = computed(() => String(route.params.productId || ''))
const variantId = computed(() => route.query.variantId || null)

const product = ref(null)
const rows = ref([])
const page = ref(1)
const totalPages = ref(0)
const loading = ref(false)

function sourceLabel (r) {
  const how = SOURCE_LABELS[r.source] || r.source
  return r.scannedCode ? `${how} "${r.scannedCode}"` : how
}

async function go (n) {
  loading.value = true
  try {
    const res = await inventoryScannerApi.history(productId.value, { variantId: variantId.value, page: n, pageSize: PAGE_SIZE })
    rows.value = res.items || []
    page.value = res.pageNumber || n
    totalPages.value = res.totalPages || 0
  } catch (err) {
    notify.error(errorText(err))
  } finally {
    loading.value = false
  }
}

async function loadProduct () {
  try {
    product.value = await inventoryScannerApi.getProduct(productId.value, variantId.value)
  } catch (e) {
    product.value = null // a deleted product still has history
  }
}

watch([productId, variantId], () => {
  if (!productId.value) return
  loadProduct()
  go(1)
}, { immediate: true })
</script>

<style scoped lang="scss">
.inv-history-head {
  &__name { font-size: 16px; font-weight: 700; }
  &__variant { font-size: 13px; color: #666; }
  &__meta { font-size: 12px; color: #888; margin-top: 4px; }
}
.inv-history-list {
  background: #fff;
  border-radius: 10px;
  overflow: hidden;
  box-shadow: 0 1px 4px rgba(0, 0, 0, 0.08);
  &__empty { padding: 28px 16px; text-align: center; color: #888; font-size: 14px; }
}
.inv-history-row {
  padding: 12px 16px;
  border-bottom: 1px solid #f0f0f0;
  font-size: 13px;
  &:last-child { border-bottom: none; }
  &__top { display: flex; justify-content: space-between; gap: 8px; color: #888; font-size: 12px; }
  &__user { font-weight: 600; color: #444; }
  &__changes { display: flex; flex-wrap: wrap; gap: 4px 16px; margin-top: 6px; color: #222; }
  &__dim { color: #aaa; }
  &__note { margin-top: 4px; color: #555; }
  &__source { margin-top: 4px; font-size: 11px; color: #999; word-break: break-all; }
}
.inv-history-pager {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  margin-top: 14px;
  font-size: 13px;
  color: #666;
  .inv-btn { width: auto; padding: 10px 18px; margin: 0; min-height: 44px; }
}
</style>
