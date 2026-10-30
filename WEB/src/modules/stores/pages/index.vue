<template>
  <q-page class="app-page">
    <AppListHeader
      title="Stores"
      :breadcrumbs="[{ label: 'Home', icon: 'o_home', to: '/dashboard' }, { label: 'Stores' }]"
      :show-add="canWrite"
      add-label="New store"
      @add="onAdd"
    >
      <template #actions>
        <q-input v-model="search" dense outlined debounce="400" placeholder="Search stores" style="min-width: 240px" @update:model-value="reload">
          <template #prepend><q-icon name="o_search" /></template>
          <template v-if="search" #append><q-icon name="o_close" class="cursor-pointer" @click="search = ''; reload()" /></template>
        </q-input>
        <q-btn outline color="primary" no-caps icon="o_tune" label="Advanced" class="q-ml-sm" @click="filtersOpen = true">
          <q-badge v-if="activeFilterCount" color="red" floating>{{ activeFilterCount }}</q-badge>
        </q-btn>
      </template>
    </AppListHeader>

    <AppFilterDrawer v-model="filtersOpen" title="Filter stores" @clear="clearFilters">
      <AppSelect v-model="enabledFilter" label="Status" :options="statusOptions" @update:model-value="reload" />
      <AppSelect v-model="guestFilter" label="Guest ordering" :options="toggleOptions" @update:model-value="reload" />
      <AppSelect v-model="pickupFilter" label="Pickup in store" :options="toggleOptions" @update:model-value="reload" />
    </AppFilterDrawer>

    <AppDataTable
      page-key="stores"
      row-key="id"
      title="All stores"
      :rows="rows"
      :columns="columns"
      :loading="loading"
      :pagination="pagination"
      show-actions
      @request="onRequest"
      @refresh="reload"
    >
      <template #body-cell-name="cell">
        <q-td :props="cell"><a class="text-primary cursor-pointer text-weight-medium" @click="onManage(cell.row)">{{ cell.row.name }}</a></q-td>
      </template>
      <template #body-cell-location="cell">
        <q-td :props="cell">{{ [cell.row.city, cell.row.countryCode].filter(Boolean).join(', ') || '—' }}</q-td>
      </template>
      <template #body-cell-status="cell">
        <q-td :props="cell"><q-badge :color="storeStatus(cell.row).color" :label="storeStatus(cell.row).label" /></q-td>
      </template>
      <template #body-cell-guestOrderingEnabled="cell">
        <q-td :props="cell"><q-icon :name="cell.row.guestOrderingEnabled ? 'o_check_circle' : 'o_block'" :color="cell.row.guestOrderingEnabled ? 'positive' : 'grey'" /></q-td>
      </template>
      <template #actions="{ row }">
        <q-btn flat round dense icon="o_warehouse" @click="openInventory(row)"><q-tooltip>Inventory</q-tooltip></q-btn>
        <q-btn flat round dense icon="o_map" @click="openZones(row)"><q-tooltip>Delivery zones</q-tooltip></q-btn>
        <q-btn v-if="canWrite" flat round dense icon="o_tune" @click="onManage(row)"><q-tooltip>Edit</q-tooltip></q-btn>
        <q-btn v-if="canWrite" flat round dense icon="o_delete" color="negative" @click="onDelete(row)"><q-tooltip>Delete</q-tooltip></q-btn>
      </template>
    </AppDataTable>

    <DeliveryZonesDialog v-model="zonesOpen" :store="zonesStore" />
  </q-page>
</template>

<script setup>
/* Admin store configuration (WO-113): store CRUD (full profile + toggles) + per-store delivery zones. */
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { getApiErrorMessage } from 'services/api'
import { usePermissions, Permissions } from 'composables/usePermissions'
import { useNotify } from 'composables/useNotify'
import { deleteConfirmation } from 'dialogs/delete_confirmation'
import { storeApi, storeStatus } from 'modules/stores/api'
import DeliveryZonesDialog from 'modules/stores/components/DeliveryZonesDialog.vue'

const router = useRouter()
const { has } = usePermissions()
const notify = useNotify()
const canWrite = computed(() => has(Permissions.StoresWrite))

const columns = [
  { name: 'name', label: 'Name', field: 'name', align: 'left', sortable: true },
  { name: 'location', label: 'Location', field: 'city', align: 'left' },
  { name: 'guestOrderingEnabled', label: 'Guest', field: 'guestOrderingEnabled', align: 'center', sortable: true },
  { name: 'status', label: 'Status', field: 'status', align: 'center' }
]

const statusOptions = [
  { label: 'All', value: null },
  { label: 'Enabled', value: true },
  { label: 'Disabled', value: false }
]
const toggleOptions = [
  { label: 'All', value: null },
  { label: 'Enabled', value: true },
  { label: 'Disabled', value: false }
]

const rows = ref([])
const loading = ref(false)
const search = ref('')
const enabledFilter = ref(null)
const guestFilter = ref(null)
const pickupFilter = ref(null)
const filtersOpen = ref(false)
const pagination = ref({ page: 1, rowsPerPage: 10, rowsNumber: 0 })
const zonesOpen = ref(false)
const zonesStore = ref(null)

const activeFilterCount = computed(() =>
  (enabledFilter.value !== null ? 1 : 0) + (guestFilter.value !== null ? 1 : 0) + (pickupFilter.value !== null ? 1 : 0)
)

function clearFilters () {
  enabledFilter.value = null
  guestFilter.value = null
  pickupFilter.value = null
  reload()
}

async function fetch (props) {
  const p = props?.pagination || pagination.value
  loading.value = true
  try {
    const result = await storeApi.list({
      page: p.page,
      pageSize: p.rowsPerPage,
      search: search.value || undefined,
      isEnabled: enabledFilter.value === null ? undefined : enabledFilter.value,
      guestOrderingEnabled: guestFilter.value === null ? undefined : guestFilter.value,
      pickupEnabled: pickupFilter.value === null ? undefined : pickupFilter.value,
      sortBy: p.sortBy || undefined,
      sortDescending: !!p.descending
    })
    const items = Array.isArray(result) ? result : result?.items || []
    rows.value = items
    pagination.value = { ...p, rowsNumber: Array.isArray(result) ? items.length : result?.totalCount ?? items.length }
  } catch (err) {
    rows.value = []
    notify.error(getApiErrorMessage(err))
  } finally {
    loading.value = false
  }
}

function onRequest (props) { fetch(props) }
function reload () { fetch({ pagination: { ...pagination.value, page: 1 } }) }

function onAdd () { router.push({ name: 'store-new' }) }
function onManage (row) { router.push({ name: 'store-detail', params: { id: row.id } }) }
function openInventory (row) { router.push({ name: 'catalog-inventory', query: { storeId: row.id } }) }

async function onDelete (row) {
  if (!(await deleteConfirmation(`the store "${row.name}"`))) return
  try {
    await storeApi.remove(row.id)
    notify.success('Store deleted')
    reload()
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  }
}

function openZones (row) { zonesStore.value = row; zonesOpen.value = true }

onMounted(() => fetch())
</script>
