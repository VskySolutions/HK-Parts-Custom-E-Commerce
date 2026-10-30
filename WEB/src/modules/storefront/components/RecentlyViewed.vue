<template>
  <section v-if="products.length" class="recently-viewed">
    <div class="text-h6 q-mb-md">{{ title }}</div>
    <div class="row q-col-gutter-md">
      <div
        v-for="p in products"
        :key="p.id"
        class="col-6 col-sm-4 col-md-3 col-lg-2"
      >
        <ProductCard :product="p" />
      </div>
    </div>
  </section>
</template>

<script setup>
/*
 * Recently-viewed row (WO-19). Reads the ordered id list from the shared
 * localStorage state, resolves them to published summaries via the storefront
 * API (order preserved server-side, published-only), and renders a row of
 * ProductCards. Silently renders nothing when there is no history or the lookup
 * fails. `excludeId` drops the current product (used on the detail page).
 */
import { ref, watch, onMounted } from 'vue'
import { storefrontApi } from 'modules/storefront/api'
import { useRecentlyViewed } from 'modules/storefront/composables/useStorefrontStorage'
import ProductCard from 'modules/storefront/components/ProductCard.vue'

const props = defineProps({
  title: { type: String, default: 'Recently viewed' },
  excludeId: { type: String, default: null }
})

const { recentlyViewedIds } = useRecentlyViewed()
const products = ref([])

async function load () {
  const ids = recentlyViewedIds.value.filter((id) => id !== props.excludeId)
  if (!ids.length) {
    products.value = []
    return
  }
  try {
    const result = await storefrontApi.recentlyViewed(ids)
    products.value = Array.isArray(result) ? result : []
  } catch (e) {
    products.value = []
  }
}

onMounted(load)
watch(recentlyViewedIds, load)
watch(() => props.excludeId, load)
</script>
