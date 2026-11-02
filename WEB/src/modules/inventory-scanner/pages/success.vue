<template>
  <div class="inv-success-page">
    <div v-if="loading" class="inv-success-card"><q-spinner color="green-6" size="28px" /></div>

    <div v-else-if="!log" class="inv-success-card">
      <p class="inv-success-card__subtitle">This update could not be found.</p>
      <router-link class="inv-btn inv-btn--green" :to="{ name: 'inventory-scanner' }">Go to Dashboard</router-link>
    </div>

    <div v-else class="inv-success-card">
      <div class="inv-success-card__check"><q-icon name="o_check" size="40px" /></div>
      <h1 class="inv-success-card__title">Update Successful!</h1>
      <div class="inv-success-card__subtitle">Inventory has been updated and logged.</div>

      <div class="inv-success-card__image">
        <img v-if="p && p.imageUrl" :src="$media(p.imageUrl)" :alt="log.productName">
        <span v-else>No Image</span>
      </div>
      <div class="inv-success-card__name">
        {{ log.productName }}<div v-if="log.variantLabel" class="inv-success-card__variant">{{ log.variantLabel }}</div>
      </div>

      <div class="inv-summary">
        <div class="inv-summary__row"><span class="label">Barcode</span><span class="val">{{ log.barcode || '—' }}</span></div>
        <div class="inv-summary__row">
          <span class="label">Stock</span>
          <span class="val">{{ log.oldQty ?? '—' }} → {{ log.newQty }} Pcs</span>
        </div>
        <div class="inv-summary__row">
          <span class="label">Price</span>
          <span class="val">{{ formatMoney(log.oldPrice) }} → {{ formatMoney(log.newPrice) }}</span>
        </div>
        <div class="inv-summary__row"><span class="label">Updated On</span><span class="val">{{ formatDateTime(log.createdOnUtc) }}</span></div>
        <div v-if="log.adminName" class="inv-summary__row"><span class="label">Updated By</span><span class="val">{{ log.adminName }}</span></div>
        <div v-if="log.notes" class="inv-summary__row"><span class="label">Note</span><span class="val">{{ log.notes }}</span></div>
      </div>

      <router-link class="inv-btn inv-btn--green inv-success-card__btn" :to="{ name: 'inventory-scanner-scan' }">
        <q-icon name="o_autorenew" size="18px" /> Scan Another Product
      </router-link>
      <router-link class="inv-btn inv-btn--light inv-success-card__btn" :to="{ name: 'inventory-scanner' }">
        <q-icon name="o_home" size="18px" /> Go to Dashboard
      </router-link>
    </div>
  </div>
</template>

<script setup>
/*
 * Barcode inventory scanner — success screen (PHP success.blade.php, same layout). Reads the audit row
 * back by id (so a refresh still works) with the product's current state.
 */
import { ref, computed, watch } from 'vue'
import { useRoute } from 'vue-router'
import { getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'
import { inventoryScannerApi, formatMoney, formatDateTime } from '../api'

const route = useRoute()
const notify = useNotify()
const log = ref(null)
const loading = ref(false)
const p = computed(() => log.value?.product || null)

async function load () {
  loading.value = true
  try {
    log.value = await inventoryScannerApi.log(route.params.id)
  } catch (err) {
    log.value = null
    if (err?.response?.status !== 404) notify.error(getApiErrorMessage(err))
  } finally {
    loading.value = false
  }
}

watch(() => route.params.id, (id) => { if (id) load() }, { immediate: true })
</script>

<style scoped lang="scss">
.inv-success-page {
  min-height: calc(100vh - 32px);
  display: flex;
  align-items: center;
  justify-content: center;
}
.inv-success-card {
  background: #fff;
  border-radius: 14px;
  padding: 30px 24px;
  width: 100%;
  max-width: 380px;
  text-align: center;
  box-shadow: 0 2px 12px rgba(0, 0, 0, 0.08);
  &__check {
    width: 72px;
    height: 72px;
    border-radius: 50%;
    background: #eaf7e0;
    color: #7ac143;
    margin: 0 auto 16px;
    display: flex;
    align-items: center;
    justify-content: center;
  }
  &__title { font-size: 19px; margin: 0 0 6px; color: #222; font-weight: 700; line-height: 1.3; }
  &__subtitle { font-size: 13px; color: #888; margin-bottom: 22px; }
  &__image {
    width: 100%;
    height: 150px;
    border-radius: 10px;
    background: #f0f0f0;
    margin-bottom: 16px;
    display: flex;
    align-items: center;
    justify-content: center;
    color: #bbb;
    font-size: 13px;
    overflow: hidden;
    img { width: 100%; height: 100%; object-fit: contain; }
  }
  &__name { font-size: 16px; font-weight: 700; margin-bottom: 14px; }
  &__variant { font-size: 13px; font-weight: 400; color: #666; }
  &__btn { padding: 14px; font-size: 14px; box-shadow: none; }
}
.inv-summary {
  background: #f8f9fa;
  border-radius: 10px;
  padding: 14px 16px;
  margin-bottom: 22px;
  text-align: left;
  &__row {
    display: flex;
    justify-content: space-between;
    gap: 12px;
    font-size: 13px;
    padding: 6px 0;
    border-bottom: 1px solid #eceef0;
    &:last-child { border-bottom: none; }
    .label { color: #888; }
    .val { font-weight: 600; color: #222; text-align: right; word-break: break-word; }
  }
}
</style>
