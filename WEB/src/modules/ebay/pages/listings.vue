<template>
  <q-page class="app-page">
    <AppListHeader
      title="eBay Listings"
      subtitle="Products listed on eBay. Quantities sync automatically; eBay orders are imported every 15 minutes."
      :breadcrumbs="[{ label: 'Home', icon: 'o_home', to: '/dashboard' }, { label: 'Catalog' }, { label: 'eBay Listings' }]"
      :show-add="canWrite"
      add-label="Add New Listing"
      @add="router.push({ name: 'ebay-listing-new' })"
    >
      <template #actions>
        <EbayStorePicker @change="reload" />
        <q-input v-model="search" dense outlined debounce="400" placeholder="Search title or SKU" style="min-width: 220px" @update:model-value="reload">
          <template #prepend><q-icon name="o_search" /></template>
          <template v-if="search" #append><q-icon name="o_close" class="cursor-pointer" @click="search = ''; reload()" /></template>
        </q-input>
        <q-select v-model="status" dense outlined emit-value map-options :options="statusOptions" style="min-width: 140px" @update:model-value="reload" />
        <q-toggle v-model="includeEnded" label="Show ended" @update:model-value="reload" />
        <q-btn v-if="canWrite" outline no-caps color="primary" icon="o_sync" label="Sync Quantities Now" :loading="syncing" @click="syncNow" />
      </template>
    </AppListHeader>

    <AppDataTable
      page-key="ebay-listings"
      row-key="id"
      title="Listings"
      :rows="rows"
      :columns="columns"
      :loading="loading"
      :pagination="pagination"
      show-actions
      @request="onRequest"
      @refresh="reload"
    >
      <template #body-cell-title="cell">
        <q-td :props="cell">
          <a class="text-primary cursor-pointer text-weight-medium" @click="open(cell.row)">{{ cell.row.title }}</a>
        </q-td>
      </template>

      <template #body-cell-status="cell">
        <q-td :props="cell">
          <q-badge :color="badge(cell.row).color" :label="badge(cell.row).label">
            <q-tooltip v-if="cell.row.status === 'Error' && cell.row.lastError" max-width="420px" class="text-body2" style="white-space: pre-line">
              {{ cell.row.lastError }}
            </q-tooltip>
          </q-badge>
        </q-td>
      </template>

      <template #body-cell-ebayItemId="cell">
        <q-td :props="cell">
          <a v-if="cell.row.ebayItemUrl" :href="cell.row.ebayItemUrl" target="_blank" rel="noopener" class="text-primary">
            {{ cell.row.ebayItemId }} <q-icon name="o_open_in_new" size="14px" />
          </a>
          <span v-else class="text-grey-6">—</span>
        </q-td>
      </template>

      <template #body-cell-quantity="cell">
        <q-td :props="cell">
          <span :class="cell.row.status === 'Active' && cell.row.quantity !== cell.row.storeInventory ? 'text-warning text-weight-bold' : ''">
            {{ cell.row.quantity }}
          </span>
          <q-tooltip v-if="cell.row.status === 'Active' && cell.row.quantity !== cell.row.storeInventory">
            Differs from store inventory; the next sync will update eBay.
          </q-tooltip>
        </q-td>
      </template>

      <template #actions="{ row }">
        <q-btn v-if="canWrite && row.status !== 'Ended'" flat round dense icon="o_edit" @click="open(row)">
          <q-tooltip>Edit</q-tooltip>
        </q-btn>
        <q-btn v-if="canWrite && row.status !== 'Ended'" flat round dense icon="o_cancel" color="negative" @click="endListing(row)">
          <q-tooltip>End listing</q-tooltip>
        </q-btn>
      </template>
    </AppDataTable>
  </q-page>
</template>

<script setup>
/*
 * eBay listings (/catalog/ebay-listings): non-ended listings by default (Active first, then Draft and
 * Error, then Title A–Z — the API's order). End asks first and calls eBay's withdraw for published listings.
 */
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { date } from 'quasar'
import { getApiErrorMessage } from 'services/api'
import { ebayApi, STATUS_BADGE, notifyEbayErrors, money } from 'modules/ebay/api'
import { usePermissions } from 'composables/usePermissions'
import { useNotify } from 'composables/useNotify'
import { confirmation } from 'dialogs/confirmation'
import EbayStorePicker from 'modules/ebay/components/EbayStorePicker.vue'

const router = useRouter()
const notify = useNotify()
const { has } = usePermissions()
const canWrite = computed(() => has('Ebay.Write'))

const statusOptions = [
  { label: 'All statuses', value: null },
  { label: 'Active', value: 'Active' },
  { label: 'Inactive (Draft)', value: 'Draft' },
  { label: 'Error', value: 'Error' },
  { label: 'Ended', value: 'Ended' }
]

const columns = [
  { name: 'title', label: 'Title', field: 'title', align: 'left' },
  { name: 'sku', label: 'SKU', field: 'sku', align: 'left' },
  { name: 'storeInventory', label: 'Store Inventory', field: 'storeInventory', align: 'right' },
  { name: 'quantity', label: 'eBay Qty', field: 'quantity', align: 'right' },
  { name: 'price', label: 'Price', field: 'price', align: 'right', format: money },
  { name: 'status', label: 'Status', field: 'status', align: 'center' },
  { name: 'ebayItemId', label: 'eBay Item ID', field: 'ebayItemId', align: 'left' },
  { name: 'lastSyncedAtUtc', label: 'Last Synced', field: 'lastSyncedAtUtc', align: 'left', format: (v) => (v ? date.formatDate(v, 'YYYY-MM-DD HH:mm') : '—') }
]

const rows = ref([])
const loading = ref(false)
const syncing = ref(false)
const search = ref('')
const status = ref(null)
const includeEnded = ref(false)
const pagination = ref({ page: 1, rowsPerPage: 25, rowsNumber: 0 })

const badge = (row) => STATUS_BADGE[row.status] || { color: 'grey', label: row.status }

async function fetch (props) {
  const p = props?.pagination || pagination.value
  loading.value = true
  try {
    const result = await ebayApi.listings({
      page: p.page,
      pageSize: p.rowsPerPage,
      search: search.value || undefined,
      status: status.value || undefined,
      includeEnded: includeEnded.value || undefined
    })
    rows.value = result?.items || []
    pagination.value = { ...p, rowsNumber: result?.totalCount ?? rows.value.length }
  } catch (err) {
    rows.value = []
    notify.error(getApiErrorMessage(err))
  } finally {
    loading.value = false
  }
}

function onRequest (props) { fetch(props) }
function reload () { fetch({ pagination: { ...pagination.value, page: 1 } }) }
function open (row) { router.push({ name: 'ebay-listing-detail', params: { id: row.id } }) }

async function endListing (row) {
  const ok = await confirmation({
    title: 'End this eBay listing?',
    message: row.ebayItemId
      ? `"${row.title}" will be withdrawn from eBay (item ${row.ebayItemId}). You can list the product again later.`
      : `The draft "${row.title}" will be closed. You can list the product again later.`,
    okLabel: 'End listing',
    color: 'negative'
  })
  if (!ok) return
  try {
    await ebayApi.endListing(row.id)
    notify.success('Your eBay listing has been ended')
    reload()
  } catch (err) {
    notifyEbayErrors(err)
  }
}

async function syncNow () {
  syncing.value = true
  try {
    const result = await ebayApi.syncQuantities()
    if (result.failed > 0) notify.warning(`Updated ${result.updated}, failed ${result.failed}: ${(result.errors || []).slice(0, 3).join('; ')}`)
    else notify.success(result.updated ? `Updated ${result.updated} listing(s) on eBay` : 'eBay quantities are already in sync')
    reload()
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    syncing.value = false
  }
}

onMounted(() => fetch())
</script>
