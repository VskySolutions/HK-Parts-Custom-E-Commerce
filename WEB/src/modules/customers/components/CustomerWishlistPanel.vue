<template>
  <q-table
    flat bordered dense
    class="app-table-card"
    row-key="id"
    :rows="rows"
    :columns="columns"
    :loading="loading"
    v-model:pagination="pagination"
    :rows-per-page-options="[10, 25, 50]"
    @request="onRequest"
  >
    <template #top>
      <div class="app-section__title col ellipsis">Wishlist</div>
    </template>

    <template #body-cell-image="p">
      <q-td :props="p">
        <q-avatar rounded size="36px" class="bg-grey-2">
          <img v-if="p.row.imageUrl" :src="$media(p.row.imageUrl)" :alt="p.row.productName || ''" class="image-fit-contain" />
          <q-icon v-else name="o_image" color="grey-5" />
        </q-avatar>
      </q-td>
    </template>

    <template #no-data>
      <div class="full-width column items-center q-pa-lg text-grey-6">
        <q-icon name="o_favorite_border" size="40px" class="q-mb-sm" />
        <div>No items in this customer's wishlist.</div>
      </div>
    </template>
  </q-table>
</template>

<script setup>
/*
 * Customer → Wishlist tab (read-only). Mounted on first visit to the tab (same lazy pattern as
 * CustomerDocumentsPanel) and paged server-side. Emits the total count so the tab badge stays in sync.
 */
import { ref, onMounted } from 'vue'
import { getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'
import { customerAdminApi } from 'modules/customers/api'

const props = defineProps({
  customerId: { type: String, required: true }
})
const emit = defineEmits(['count'])

const notify = useNotify()
const rows = ref([])
const loading = ref(false)
const pagination = ref({ page: 1, rowsPerPage: 10, rowsNumber: 0 })

const dash = (v) => v || '—'
const columns = [
  { name: 'image', label: 'Image', field: 'imageUrl', align: 'left', style: 'width: 56px' },
  { name: 'productName', label: 'Product name', field: 'productName', align: 'left', format: dash },
  { name: 'sku', label: 'SKU', field: 'sku', align: 'left', format: dash }
]

async function onRequest ({ pagination: next }) {
  loading.value = true
  try {
    const res = await customerAdminApi.getWishlist(props.customerId, { page: next.page, pageSize: next.rowsPerPage })
    rows.value = res?.items || []
    pagination.value = { ...next, rowsNumber: res?.totalCount || 0 }
    emit('count', res?.totalCount || 0)
  } catch (e) {
    notify.error(getApiErrorMessage(e))
  } finally { loading.value = false }
}

onMounted(() => onRequest({ pagination: pagination.value }))
</script>
