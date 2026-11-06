<template>
  <q-select
    v-if="stores.length > 1"
    :model-value="ebayStoreId"
    dense
    outlined
    emit-value
    map-options
    option-value="id"
    option-label="name"
    :options="stores"
    label="Store"
    style="min-width: 200px"
    @update:model-value="onChange"
  />
</template>

<script setup>
/*
 * Store switcher for the eBay pages (one eBay account per store). Hidden when there is only one store.
 * The choice is remembered per browser and sent as ?storeId= by modules/ebay/api.
 */
import { ref, onMounted } from 'vue'
import { storeApi } from 'modules/stores/api'
import { ebayStoreId, setEbayStore } from 'modules/ebay/api'

const emit = defineEmits(['change'])
const stores = ref([])

function onChange (id) {
  setEbayStore(id)
  emit('change', id)
}

onMounted(async () => {
  try {
    const result = await storeApi.list({ page: 1, pageSize: 100 })
    stores.value = Array.isArray(result) ? result : result?.items || []
    if (ebayStoreId.value && !stores.value.some((s) => s.id === ebayStoreId.value)) setEbayStore(null)
  } catch (e) {
    stores.value = []
  }
})
</script>
