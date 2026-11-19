<template>
  <q-page class="app-page">
    <AppListHeader
      title="Returns (RMA)"
      :breadcrumbs="[{ label: 'Home', icon: 'o_home', to: '/dashboard' }, { label: 'Returns' }]"
    >
      <template #actions>
        <q-input v-model="search" dense outlined debounce="400" placeholder="RMA #, order # or email" style="min-width: 260px" @update:model-value="reload">
          <template #prepend><q-icon name="o_search" /></template>
          <template v-if="search" #append><q-icon name="o_close" class="cursor-pointer" @click="search = ''; reload()" /></template>
        </q-input>
        <q-btn outline color="primary" no-caps icon="o_tune" label="Advanced" class="q-ml-sm" @click="filtersOpen = true">
          <q-badge v-if="activeFilterCount" color="red" floating>{{ activeFilterCount }}</q-badge>
        </q-btn>
        <q-btn flat color="primary" no-caps icon="o_settings" label="Return settings" class="q-ml-sm" :to="{ name: 'admin-return-settings' }" />
      </template>
    </AppListHeader>

    <q-tabs v-model="view" dense align="left" no-caps active-color="primary" indicator-color="primary" class="q-mb-sm" @update:model-value="reload">
      <q-tab name="open" label="Open" />
      <q-tab name="Requested">
        <div class="row items-center no-wrap">
          <span>Awaiting review</span>
          <q-badge v-if="pendingCount" color="orange" class="q-ml-xs">{{ pendingCount }}</q-badge>
        </div>
      </q-tab>
      <q-tab name="Received" label="To inspect" />
      <q-tab name="Inspected" label="To refund" />
      <q-tab name="all" label="All" />
    </q-tabs>

    <AppFilterDrawer v-model="filtersOpen" title="Filter returns" @clear="clearFilters">
      <AppSelect v-model="filters.status" label="Status" clearable placeholder="Any status" :options="rmaStatusOptions" @update:model-value="reload" />
      <AppSelect v-model="filters.resolution" label="Resolution" clearable placeholder="Any resolution" :options="rmaResolutionOptions" @update:model-value="reload" />
      <AppSelect v-model="filters.reasonId" label="Reason" clearable placeholder="Any reason" :options="reasonOptions" @update:model-value="reload" />
      <AppSelect
        v-model="filters.requiresFfl" label="Firearm (FFL)" clearable placeholder="Any"
        :options="[{ label: 'FFL returns only', value: true }, { label: 'Exclude FFL', value: false }]"
        @update:model-value="reload"
      />
      <q-input v-model="filters.from" type="date" dense outlined stack-label label="Requested from" class="q-mt-sm" @update:model-value="reload" />
      <q-input v-model="filters.to" type="date" dense outlined stack-label label="Requested to" class="q-mt-sm" @update:model-value="reload" />
    </AppFilterDrawer>

    <AppDataTable
      page-key="admin-rmas"
      row-key="id"
      title="Returns"
      :rows="rows"
      :columns="columns"
      :loading="loading"
      :pagination="pagination"
      show-actions
      @request="onRequest"
      @refresh="reload"
    >
      <template #body-cell-rmaNumber="cell">
        <q-td :props="cell">
          <a class="text-primary cursor-pointer text-weight-medium" @click="open(cell.row)">{{ cell.row.rmaNumber }}</a>
          <q-badge v-if="cell.row.requiresFfl" color="deep-orange" label="FFL" class="q-ml-xs" />
          <q-icon v-if="cell.row.lastRefundError" name="o_error" color="negative" class="q-ml-xs"><q-tooltip>{{ cell.row.lastRefundError }}</q-tooltip></q-icon>
        </q-td>
      </template>
      <template #body-cell-orderNumber="cell">
        <q-td :props="cell">
          <a class="text-primary cursor-pointer" @click="openOrder(cell.row)">{{ cell.row.orderNumber || '—' }}</a>
        </q-td>
      </template>
      <template #body-cell-customerName="cell">
        <q-td :props="cell">
          <div>{{ cell.row.customerName || 'Guest' }}</div>
          <div class="text-caption text-grey-7">{{ cell.row.customerEmail }}</div>
        </q-td>
      </template>
      <template #body-cell-requestedOnUtc="cell">
        <q-td :props="cell">{{ formatDate(cell.row.requestedOnUtc) }}</q-td>
      </template>
      <template #body-cell-status="cell">
        <q-td :props="cell">
          <q-badge :color="rmaStatusColor(cell.row.status)" :label="humanizeEnum(cell.row.status)" />
          <div class="text-caption text-grey-7">{{ cell.row.daysInStatus }} day{{ cell.row.daysInStatus === 1 ? '' : 's' }}</div>
        </q-td>
      </template>
      <template #body-cell-resolution="cell">
        <q-td :props="cell">{{ resolutionLabel(cell.row.resolution !== 'None' ? cell.row.resolution : cell.row.requestedResolution) }}</q-td>
      </template>
      <template #body-cell-totalRefund="cell">
        <q-td :props="cell">{{ formatMoney(cell.row.totalRefund) }}</q-td>
      </template>
      <template #actions="{ row }">
        <q-btn flat round dense icon="o_visibility" @click="open(row)"><q-tooltip>Open</q-tooltip></q-btn>
      </template>
    </AppDataTable>
  </q-page>
</template>

<script setup>
/* Admin returns queue (order-return-requirements RT-30 / RT-31): quick views by stage, filters, refund-failure flag. */
import { ref, reactive, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'
import {
  rmaApi, rmaStatusColor, rmaStatusOptions, rmaResolutionOptions, humanizeEnum, formatMoney, formatDate
} from 'modules/orders/api'

const router = useRouter()
const notify = useNotify()

const columns = [
  { name: 'rmaNumber', label: 'RMA #', field: 'rmaNumber', align: 'left', sortable: true },
  { name: 'orderNumber', label: 'Order #', field: 'orderNumber', align: 'left' },
  { name: 'customerName', label: 'Customer', field: 'customerName', align: 'left' },
  { name: 'requestedOnUtc', label: 'Requested', field: 'requestedOnUtc', align: 'left', sortable: true },
  { name: 'status', label: 'Status', field: 'status', align: 'left', sortable: true },
  { name: 'unitCount', label: 'Units', field: 'unitCount', align: 'right' },
  { name: 'resolution', label: 'Resolution', field: 'resolution', align: 'left', sortable: true },
  { name: 'totalRefund', label: 'Refund', field: 'totalRefund', align: 'right', sortable: true }
]

const rows = ref([])
const loading = ref(false)
const search = ref('')
const view = ref('open')
const pendingCount = ref(0)
const filters = reactive({ status: null, resolution: null, reasonId: null, requiresFfl: null, from: '', to: '' })
const filtersOpen = ref(false)
const reasonOptions = ref([])
const pagination = ref({ page: 1, rowsPerPage: 20, rowsNumber: 0 })

const activeFilterCount = computed(() => ['status', 'resolution', 'reasonId', 'requiresFfl', 'from', 'to']
  .filter((k) => filters[k] !== null && filters[k] !== '').length)

const resolutionLabel = (v) => rmaResolutionOptions.find((o) => o.value === v)?.label || '—'

// The date inputs are local calendar days; send them as the UTC instants of local midnight.
const dayStartUtc = (d) => (d ? new Date(`${d}T00:00:00`).toISOString() : undefined)
const dayEndUtc = (d) => {
  if (!d) return undefined
  const next = new Date(`${d}T00:00:00`)
  next.setDate(next.getDate() + 1)
  return next.toISOString()
}

async function fetch (props) {
  const p = props?.pagination || pagination.value
  loading.value = true
  try {
    const status = filters.status || (['open', 'all'].includes(view.value) ? undefined : view.value)
    const result = await rmaApi.list({
      page: p.page,
      pageSize: p.rowsPerPage,
      status,
      openOnly: view.value === 'open' && !filters.status ? true : undefined,
      resolution: filters.resolution || undefined,
      reasonId: filters.reasonId || undefined,
      requiresFfl: filters.requiresFfl ?? undefined,
      fromUtc: dayStartUtc(filters.from),
      toUtc: dayEndUtc(filters.to),
      search: search.value || undefined,
      sortBy: p.sortBy || undefined,
      sortDescending: !!p.descending
    })
    rows.value = Array.isArray(result?.items) ? result.items : []
    pagination.value = { ...p, rowsNumber: result?.totalCount ?? rows.value.length }
  } catch (err) {
    rows.value = []
    notify.error(getApiErrorMessage(err))
  } finally {
    loading.value = false
  }
  rmaApi.pendingCount().then((n) => { pendingCount.value = n }).catch(() => {})
}

function onRequest (props) { fetch(props) }
function reload () { fetch({ pagination: { ...pagination.value, page: 1 } }) }
function clearFilters () {
  Object.assign(filters, { status: null, resolution: null, reasonId: null, requiresFfl: null, from: '', to: '' })
  reload()
}
function open (row) { router.push({ name: 'admin-rma-detail', params: { id: row.id } }) }
function openOrder (row) { router.push({ name: 'admin-order-detail', params: { id: row.orderId } }) }

onMounted(async () => {
  fetch()
  try {
    reasonOptions.value = (await rmaApi.reasons()).map((r) => ({ label: r.name, value: r.id }))
  } catch { /* filter just stays empty */ }
})
</script>
