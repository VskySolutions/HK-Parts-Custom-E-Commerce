<template>
  <div class="sf-section">
    <div class="sf-container">
      <div class="sf-section__head">
        <h2 class="sf-section__title">{{ heading }}</h2>
        <q-tabs
          v-model="active"
          dense
          no-caps
          inline-label
          active-color="primary"
          indicator-color="primary"
          class="text-grey-7 gt-xs"
          @update:model-value="loadTab"
        >
          <q-tab v-for="t in tabs" :key="t.key" :name="t.key" :label="t.label" />
        </q-tabs>
      </div>

      <!-- Mobile tab select -->
      <q-select
        v-model="active"
        dense
        outlined
        emit-value
        map-options
        :options="tabs.map((t) => ({ label: t.label, value: t.key }))"
        class="lt-sm q-mb-md"
        @update:model-value="loadTab"
      />

      <div v-if="loading" class="row q-col-gutter-md">
        <div v-for="n in 4" :key="n" class="col-6 col-md-3">
          <q-skeleton height="260px" class="sf-skeleton-card" />
        </div>
      </div>

      <div v-else-if="!products.length" class="text-grey-6 q-py-lg text-center">
        No products to show yet.
      </div>

      <ProductCarousel v-else :products="products" />
    </div>
  </div>
</template>

<script setup>
/*
 * Porto tabbed product row (WO-110). Curated product-row rules (true New Arrivals /
 * Featured / Best Sellers / On Sale — REQ-CNT-010) and the Home Sections API don't
 * exist yet (flagged on WO-110), so the tabs map to the REAL faceted-search sort
 * orders the catalog already supports, showing genuine products. Swap each tab's
 * loader to a Collection / auto-generated-row endpoint when available.
 */
import { ref, onMounted } from 'vue'
import { storefrontApi } from 'modules/storefront/api'
import ProductCarousel from 'modules/storefront/components/ProductCarousel.vue'

defineProps({ heading: { type: String, default: 'Featured Products' } })

// Honest labels tied to sorts the backend actually supports.
const tabs = [
  { key: 'newest', label: 'New Arrivals', sort: 'newest' },
  { key: 'price_asc', label: 'Best Value', sort: 'price_asc' },
  { key: 'price_desc', label: 'Premium Picks', sort: 'price_desc' },
  { key: 'name_asc', label: 'A–Z', sort: 'name_asc' }
]

const active = ref('newest')
const products = ref([])
const loading = ref(false)
const cache = {}

async function loadTab (key) {
  const tab = tabs.find((t) => t.key === key) || tabs[0]
  if (cache[key]) { products.value = cache[key]; return }
  loading.value = true
  try {
    const res = await storefrontApi.search({ sort: tab.sort, page: 1, pageSize: 8 })
    const items = Array.isArray(res?.items) ? res.items : []
    cache[key] = items
    products.value = items
  } catch (e) {
    products.value = []
  } finally {
    loading.value = false
  }
}

onMounted(() => loadTab(active.value))
</script>
