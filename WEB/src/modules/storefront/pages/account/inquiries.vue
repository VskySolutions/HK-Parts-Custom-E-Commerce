<template>
  <q-card flat bordered>
    <q-card-section class="text-subtitle1 text-weight-medium">My requests</q-card-section>
    <q-separator />

    <q-table
      flat
      :rows="rows"
      :columns="columns"
      row-key="id"
      :loading="loading"
      :pagination="pagination"
      :rows-per-page-options="[10, 20, 50]"
      no-data-label="You haven't submitted any requests yet."
      @request="onRequest"
    >
      <template #body-cell-inquiryStatus="props">
        <q-td :props="props">
          <q-badge :color="statusColor(props.value)" :label="humanStatus(props.value)" />
        </q-td>
      </template>
      <template #body-cell-estimatedValue="props">
        <q-td :props="props" class="text-weight-medium">{{ formatPrice(props.value) }}</q-td>
      </template>
      <template #body-cell-submittedOnUtc="props">
        <q-td :props="props">{{ formatDate(props.value) }}</q-td>
      </template>
    </q-table>

    <q-card-section class="text-caption text-grey-6">
      Values shown are indicative. Shipping, taxes and final pricing are confirmed in the quote our team
      sends you — nothing has been charged.
    </q-card-section>
  </q-card>
</template>

<script setup>
/*
 * Storefront account — my requests (REQ-INQ-001).
 *
 * Inquiries are deliberately kept out of "My orders": nothing was paid for and nothing ships, so mixing
 * them into the order history would misrepresent both. Guests can still submit requests; their
 * acknowledgement email carries the reference instead.
 */
import { ref, onMounted } from 'vue'
import { getApiErrorMessage } from 'services/api'
import { accountApi } from 'modules/storefront/account-api'
import { formatPrice } from 'modules/storefront/api'
import { useNotify } from 'composables/useNotify'

const notify = useNotify()

const columns = [
  { name: 'referenceNumber', label: 'Reference', field: 'referenceNumber', align: 'left' },
  { name: 'submittedOnUtc', label: 'Submitted', field: 'submittedOnUtc', align: 'left' },
  { name: 'itemCount', label: 'Items', field: 'itemCount', align: 'center' },
  { name: 'estimatedValue', label: 'Indicative value', field: 'estimatedValue', align: 'right' },
  { name: 'inquiryStatus', label: 'Status', field: 'inquiryStatus', align: 'left' }
]

const rows = ref([])
const loading = ref(false)
const pagination = ref({ page: 1, rowsPerPage: 10, rowsNumber: 0 })

function humanStatus (status) {
  return status === 'InReview' ? 'In review' : status
}

function statusColor (status) {
  switch (status) {
    case 'New': return 'blue'
    case 'InReview': return 'indigo'
    case 'Quoted': return 'orange'
    case 'Accepted': return 'teal'
    case 'Declined': return 'red'
    case 'Converted': return 'green'
    default: return 'grey'
  }
}

function formatDate (raw) {
  if (!raw) return ''
  const d = new Date(raw)
  return Number.isNaN(d.getTime())
    ? ''
    : d.toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' })
}

async function fetch (props) {
  const p = props?.pagination || pagination.value
  loading.value = true
  try {
    // The customer's bearer token is attached by the shared interceptor; the endpoint is [Authorize].
    const result = await accountApi.inquiries({ page: p.page, pageSize: p.rowsPerPage })
    rows.value = Array.isArray(result?.items) ? result.items : []
    pagination.value = { ...p, rowsNumber: result?.totalCount ?? rows.value.length }
  } catch (err) {
    rows.value = []
    pagination.value = { ...p, rowsNumber: 0 }
    notify.error(getApiErrorMessage(err))
  } finally {
    loading.value = false
  }
}

function onRequest (props) { fetch(props) }

onMounted(() => fetch())
</script>
