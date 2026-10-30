<template>
  <q-page class="app-page">
    <AppListHeader
      title="Returns (RMA)"
      :breadcrumbs="[{ label: 'Home', icon: 'o_home', to: '/dashboard' }, { label: 'Returns' }]"
    >
      <template #actions>
        <q-input v-model="search" dense outlined debounce="400" placeholder="Search by RMA #" style="min-width: 240px" @update:model-value="reload">
          <template #prepend><q-icon name="o_search" /></template>
          <template v-if="search" #append><q-icon name="o_close" class="cursor-pointer" @click="search = ''; reload()" /></template>
        </q-input>
        <q-btn outline color="primary" no-caps icon="o_tune" label="Advanced" class="q-ml-sm" @click="filtersOpen = true">
          <q-badge v-if="activeFilterCount" color="red" floating>{{ activeFilterCount }}</q-badge>
        </q-btn>
      </template>
    </AppListHeader>

    <AppFilterDrawer v-model="filtersOpen" title="Filter returns" @clear="clearFilters">
      <AppSelect
        v-model="statusFilter"
        label="Status"
        clearable
        placeholder="Any status"
        :options="['Requested', 'Approved', 'Rejected', 'Completed', 'Cancelled'].map((s) => ({ label: s, value: s }))"
        @update:model-value="reload"
      />
      <AppSelect
        v-model="resolutionFilter"
        label="Resolution"
        clearable
        placeholder="Any resolution"
        :options="rmaResolutionOptions"
        @update:model-value="reload"
      />
    </AppFilterDrawer>

    <AppDataTable
      page-key="admin-rmas"
      row-key="id"
      title="All returns"
      :rows="rows"
      :columns="columns"
      :loading="loading"
      :pagination="pagination"
      show-actions
      @request="onRequest"
      @refresh="reload"
    >
      <template #body-cell-rmaNumber="cell">
        <q-td :props="cell"><a class="text-primary cursor-pointer text-weight-medium" @click="open(cell.row)">{{ cell.row.rmaNumber }}</a></q-td>
      </template>
      <template #body-cell-status="cell">
        <q-td :props="cell"><q-badge :color="rmaStatusColor(cell.row.status)" :label="cell.row.status" /></q-td>
      </template>
      <template #body-cell-requestedOnUtc="cell">
        <q-td :props="cell">{{ formatDate(cell.row.requestedOnUtc) }}</q-td>
      </template>
      <template #actions="{ row }">
        <q-btn flat round dense icon="o_visibility" @click="open(row)"><q-tooltip>Review</q-tooltip></q-btn>
      </template>
    </AppDataTable>
  </q-page>
</template>

<script setup>
/* Admin RMA / returns list (WO-114). */
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'
import { rmaApi, rmaStatusColor, rmaResolutionOptions, formatDate } from 'modules/orders/api'

const router = useRouter()
const notify = useNotify()

const columns = [
  { name: 'rmaNumber', label: 'RMA #', field: 'rmaNumber', align: 'left', sortable: true },
  { name: 'requestedOnUtc', label: 'Requested', field: 'requestedOnUtc', align: 'left', sortable: true },
  { name: 'resolution', label: 'Resolution', field: 'resolution', align: 'left', sortable: true },
  { name: 'status', label: 'Status', field: 'status', align: 'left', sortable: true }
]

const rows = ref([])
const loading = ref(false)
const search = ref('')
const statusFilter = ref(null)
const resolutionFilter = ref(null)
const filtersOpen = ref(false)
const pagination = ref({ page: 1, rowsPerPage: 20, rowsNumber: 0 })

const activeFilterCount = computed(() => (statusFilter.value ? 1 : 0) + (resolutionFilter.value ? 1 : 0))

async function fetch (props) {
  const p = props?.pagination || pagination.value
  loading.value = true
  try {
    const result = await rmaApi.list({
      page: p.page,
      pageSize: p.rowsPerPage,
      status: statusFilter.value || undefined,
      resolution: resolutionFilter.value || undefined,
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
}

function onRequest (props) { fetch(props) }
function reload () { fetch({ pagination: { ...pagination.value, page: 1 } }) }
function clearFilters () { statusFilter.value = null; resolutionFilter.value = null; reload() }
function open (row) { router.push({ name: 'admin-rma-detail', params: { id: row.id } }) }

onMounted(() => fetch())
</script>
