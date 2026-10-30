<template>
  <q-page class="app-page">
    <AppListHeader
      title="Subscriptions"
      :breadcrumbs="[{ label: 'Home', icon: 'o_home', to: '/dashboard' }, { label: 'Subscriptions' }]"
    >
      <template #actions>
        <q-input
          v-model="search"
          dense
          outlined
          debounce="300"
          placeholder="Search customer or product"
          style="min-width: 260px"
        >
          <template #prepend><q-icon name="o_search" /></template>
          <template v-if="search" #append>
            <q-icon name="o_close" class="cursor-pointer" @click="search = ''" />
          </template>
        </q-input>
        <q-btn outline color="primary" no-caps icon="o_tune" label="Advanced" class="q-ml-sm" @click="filtersOpen = true">
          <q-badge v-if="statusFilter" color="red" floating>1</q-badge>
        </q-btn>
      </template>
    </AppListHeader>

    <AppFilterDrawer v-model="filtersOpen" title="Filter subscriptions" @clear="clearFilters">
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
      page-key="subscriptions"
      row-key="id"
      title="All subscriptions"
      :rows="displayRows"
      :columns="columns"
      :loading="loading"
      :pagination="pagination"
      show-actions
      @request="onRequest"
      @refresh="reload"
    >
      <template #body-cell-interval="cell">
        <q-td :props="cell">{{ intervalLabel(cell.row.interval) }}</q-td>
      </template>
      <template #body-cell-status="cell">
        <q-td :props="cell">
          <q-badge :color="subscriptionStatusColor(cell.row.status)" :label="cell.row.status" />
        </q-td>
      </template>
      <template #body-cell-nextOrderOnUtc="cell">
        <q-td :props="cell">{{ $date(cell.row.nextOrderOnUtc) }}</q-td>
      </template>

      <template #actions="{ row }">
        <template v-if="canManage">
          <q-btn
            v-if="row.status === 'Active'"
            flat round dense
            icon="o_pause_circle"
            color="primary"
            :loading="actingId === row.id"
            @click="onPause(row)"
          >
            <q-tooltip>Pause</q-tooltip>
          </q-btn>
          <q-btn
            v-if="row.status !== 'Cancelled'"
            flat round dense
            icon="o_cancel"
            color="negative"
            :loading="actingId === row.id"
            @click="onCancel(row)"
          >
            <q-tooltip>Cancel subscription</q-tooltip>
          </q-btn>
          <span v-if="row.status === 'Cancelled'" class="text-grey-5">&mdash;</span>
        </template>
        <span v-else class="text-grey-5">&mdash;</span>
      </template>
    </AppDataTable>
  </q-page>
</template>

<script setup>
/*
 * Admin subscriptions list (WO-49): server-paginated AppDataTable of recurring
 * subscriptions with a status filter and per-row Pause / Cancel actions. Read-only
 * otherwise — subscriptions are created by customers on the storefront, not here.
 *
 * The backend list query only supports status + customerId, so the quick-search box
 * filters the loaded page client-side by customer / product name as a convenience.
 */
import { ref, computed, onMounted } from 'vue'
import { getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'
import { usePermissions, Permissions } from 'composables/usePermissions'
import { deleteConfirmation } from 'dialogs/delete_confirmation'
import { subscriptionApi, intervalLabel, subscriptionStatusColor } from 'modules/subscriptions/api'

const notify = useNotify()
const { has } = usePermissions()
const canManage = computed(() => has(Permissions.OrdersWrite))

const columns = [
  { name: 'customerName', label: 'Customer', field: 'customerName', align: 'left', sortable: true },
  { name: 'productName', label: 'Product', field: 'productName', align: 'left', sortable: true },
  { name: 'interval', label: 'Interval', field: 'interval', align: 'left', sortable: true },
  { name: 'status', label: 'Status', field: 'status', align: 'left', sortable: true },
  { name: 'nextOrderOnUtc', label: 'Next order', field: 'nextOrderOnUtc', align: 'left', sortable: true },
  { name: 'quantity', label: 'Qty', field: 'quantity', align: 'right', sortable: true }
]

const statusOptions = [
  { label: 'Active', value: 'Active' },
  { label: 'Paused', value: 'Paused' },
  { label: 'Cancelled', value: 'Cancelled' }
]

const rows = ref([])
const loading = ref(false)
const actingId = ref(null)
const search = ref('')
const statusFilter = ref(null)
const filtersOpen = ref(false)
const pagination = ref({ page: 1, rowsPerPage: 10, rowsNumber: 0 })

// Client-side convenience filter over the loaded page (no server-side text search).
const displayRows = computed(() => {
  const q = search.value.trim().toLowerCase()
  if (!q) return rows.value
  return rows.value.filter((r) =>
    (r.customerName || '').toLowerCase().includes(q) ||
    (r.productName || '').toLowerCase().includes(q)
  )
})

async function fetch (props) {
  const p = props?.pagination || pagination.value
  loading.value = true
  try {
    const result = await subscriptionApi.list({
      page: p.page,
      pageSize: p.rowsPerPage,
      status: statusFilter.value || undefined,
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

async function onPause (row) {
  actingId.value = row.id
  try {
    await subscriptionApi.pause(row.id)
    notify.success('Subscription paused')
    reload()
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    actingId.value = null
  }
}

async function onCancel (row) {
  const confirmed = await deleteConfirmation(`the "${row.productName}" subscription for ${row.customerName}`, {
    title: 'Cancel subscription',
    okLabel: 'Cancel subscription',
    cancelLabel: 'Keep it',
    message: `Cancel the "${row.productName}" subscription for ${row.customerName}? This can't be undone.`
  })
  if (!confirmed) return
  actingId.value = row.id
  try {
    await subscriptionApi.cancel(row.id)
    notify.success('Subscription cancelled')
    reload()
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    actingId.value = null
  }
}

onMounted(() => fetch())
</script>
