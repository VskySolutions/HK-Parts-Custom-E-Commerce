<template>
  <q-card flat bordered>
    <q-card-section class="row items-center">
      <div class="text-h6">My returns</div>
      <q-space />
      <q-btn flat no-caps color="primary" icon="o_receipt_long" label="Start a return from an order" :to="{ name: 'shop-account-orders' }" />
    </q-card-section>
    <q-table
      flat
      row-key="id"
      :rows="rows"
      :columns="columns"
      :loading="loading"
      v-model:pagination="pagination"
      :rows-per-page-options="[10, 20, 50]"
      no-data-label="You haven't requested any returns."
      @request="onRequest"
    >
      <template #body-cell-rmaNumber="props">
        <q-td :props="props">
          <router-link class="text-primary text-weight-medium" :to="{ name: 'shop-account-return-detail', params: { id: props.row.id } }">{{ props.value }}</router-link>
        </q-td>
      </template>
      <template #body-cell-requestedOnUtc="props">
        <q-td :props="props">{{ formatDate(props.value) }}</q-td>
      </template>
      <template #body-cell-status="props">
        <q-td :props="props"><q-badge :color="returnStatusColor(props.value)" :label="returnStatusLabel(props.value)" /></q-td>
      </template>
      <template #body-cell-totalRefund="props">
        <q-td :props="props">{{ props.value ? formatPrice(props.value, props.row.currencyCode) : '—' }}</q-td>
      </template>
    </q-table>
  </q-card>
</template>

<script setup>
/* My Returns (order-return-requirements RT-22). */
import { ref, onMounted } from 'vue'
import { useQuasar } from 'quasar'
import { returnsApi } from 'modules/storefront/account-api'
import { formatPrice } from 'modules/storefront/api'
import { formatDateTime as formatDate } from 'src/utils/datetime'
import { getApiErrorMessage } from 'services/api'
import { returnStatusColor, returnStatusLabel } from 'modules/storefront/composables/useReturnStatus'

const $q = useQuasar()
const rows = ref([])
const loading = ref(false)
const pagination = ref({ page: 1, rowsPerPage: 20, rowsNumber: 0 })

const columns = [
  { name: 'rmaNumber', label: 'Return #', field: 'rmaNumber', align: 'left' },
  { name: 'orderNumber', label: 'Order #', field: 'orderNumber', align: 'left' },
  { name: 'requestedOnUtc', label: 'Requested', field: 'requestedOnUtc', align: 'left' },
  { name: 'unitCount', label: 'Items', field: 'unitCount', align: 'center' },
  { name: 'status', label: 'Status', field: 'status', align: 'left' },
  { name: 'totalRefund', label: 'Refund', field: 'totalRefund', align: 'right' }
]

async function fetchPage (page, rowsPerPage) {
  loading.value = true
  try {
    const data = await returnsApi.list({ page, pageSize: rowsPerPage })
    rows.value = data?.items || []
    pagination.value = { page: data?.pageNumber ?? page, rowsPerPage, rowsNumber: data?.totalCount ?? rows.value.length }
  } catch (e) {
    $q.notify({ type: 'negative', message: getApiErrorMessage(e) })
  } finally {
    loading.value = false
  }
}

function onRequest (props) {
  fetchPage(props.pagination.page, props.pagination.rowsPerPage)
}

onMounted(() => fetchPage(1, pagination.value.rowsPerPage))
</script>
