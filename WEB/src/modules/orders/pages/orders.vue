<template>
  <q-page class="app-page">
    <AppListHeader
      title="Orders"
      :breadcrumbs="[{ label: 'Home', icon: 'o_home', to: '/dashboard' }, { label: 'Orders' }]"
    >
      <template #actions>
        <q-input
          v-model="search"
          dense
          outlined
          debounce="400"
          placeholder="Search by order # or customer"
          class="q-mr-sm"
          style="min-width: 260px"
          @update:model-value="reload"
        >
          <template #prepend><q-icon name="o_search" /></template>
          <template v-if="search" #append>
            <q-icon name="o_close" class="cursor-pointer" @click="search = ''; reload()" />
          </template>
        </q-input>
        <q-btn outline color="primary" no-caps icon="o_tune" label="Advanced" @click="filtersOpen = true">
          <q-badge v-if="statusFilter" color="red" floating>1</q-badge>
        </q-btn>
      </template>
    </AppListHeader>

    <AppFilterDrawer v-model="filtersOpen" title="Filter orders" @clear="clearFilters">
      <AppSelect
        v-model="statusFilter"
        label="Status"
        clearable
        placeholder="Any status"
        :options="statusOptions"
        @update:model-value="reload"
      />
    </AppFilterDrawer>

    <AppDataTable
      page-key="admin-orders"
      row-key="id"
      title="All orders"
      :rows="rows"
      :columns="columns"
      :loading="loading"
      :pagination="pagination"
      show-actions
      @request="onRequest"
      @refresh="reload"
    >
      <template #body-cell-orderNumber="cell">
        <q-td :props="cell">
          <a class="text-primary cursor-pointer text-weight-medium" @click="openOrder(cell.row)">{{ cell.row.orderNumber }}</a>
        </q-td>
      </template>
      <template #body-cell-status="cell">
        <q-td :props="cell"><q-badge :color="orderStatusColor(cell.row.status)" :label="cell.row.status" /></q-td>
      </template>
      <template #body-cell-totalAmount="cell">
        <q-td :props="cell">{{ formatMoney(cell.row.totalAmount) }}</q-td>
      </template>
      <template #body-cell-placedOnUtc="cell">
        <q-td :props="cell">{{ formatDate(cell.row.placedOnUtc) }}</q-td>
      </template>
      <template #actions="{ row }">
        <q-btn flat round dense icon="o_visibility" @click="openOrder(row)"><q-tooltip>Manage</q-tooltip></q-btn>
      </template>
    </AppDataTable>
  </q-page>
</template>

<script setup>
/*
 * Admin order list (WO-114). Status filter + paging come from the backend
 * (AdminOrdersController supports status + page only); the quick-search box filters
 * the loaded page client-side by order number / customer as a convenience.
 */
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'
import { orderApi, orderStatusColor, formatMoney, formatDate } from 'modules/orders/api'

const router = useRouter()
const notify = useNotify()

const columns = [
  { name: 'orderNumber', label: 'Order #', field: 'orderNumber', align: 'left', sortable: true },
  { name: 'contactName', label: 'Customer', field: 'contactName', align: 'left' },
  { name: 'placedOnUtc', label: 'Date', field: 'placedOnUtc', align: 'left', sortable: true },
  { name: 'itemCount', label: 'Items', field: 'itemCount', align: 'center' },
  { name: 'totalAmount', label: 'Total', field: 'totalAmount', align: 'right', sortable: true },
  { name: 'status', label: 'Status', field: 'status', align: 'left', sortable: true }
]

const statusOptions = [
  'Pending', 'Processing', 'Shipped', 'Delivered', 'Cancelled', 'ReadyForPickup',
  'PendingRouting', 'Routed', 'Unrouted', 'Accepted', 'Rejected', 'Preparing'
].map((s) => ({ label: s, value: s }))

const rows = ref([])
const loading = ref(false)
const search = ref('')
const statusFilter = ref(null)
const filtersOpen = ref(false)
const pagination = ref({ page: 1, rowsPerPage: 20, rowsNumber: 0 })

async function fetch (props) {
  const p = props?.pagination || pagination.value
  loading.value = true
  try {
    const result = await orderApi.list({
      page: p.page,
      pageSize: p.rowsPerPage,
      status: statusFilter.value || undefined,
      search: search.value || undefined,
      sortBy: p.sortBy || undefined,
      sortDescending: !!p.descending
    })
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
function reload () { fetch({ pagination: { ...pagination.value, page: 1 } }) }
function clearFilters () { statusFilter.value = null; reload() }
function openOrder (row) { router.push({ name: 'admin-order-detail', params: { id: row.id } }) }

onMounted(() => fetch())
</script>
