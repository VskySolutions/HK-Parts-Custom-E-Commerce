<template>
  <q-page class="app-page">
    <AppListHeader title="Customers" :breadcrumbs="[{ label: 'Home', icon: 'o_home', to: '/dashboard' }, { label: 'Customers' }]" :show-add="false">
      <template #actions>
        <q-input v-model="search" dense outlined debounce="400" placeholder="Search name or email" style="min-width: 240px" @update:model-value="reload">
          <template #prepend><q-icon name="o_search" /></template>
          <template v-if="search" #append><q-icon name="o_close" class="cursor-pointer" @click="search = ''; reload()" /></template>
        </q-input>
        <q-btn outline color="primary" no-caps icon="o_tune" label="Advanced" class="q-ml-sm" @click="filtersOpen = true">
          <q-badge v-if="activeFilterCount" color="red" floating>{{ activeFilterCount }}</q-badge>
        </q-btn>
      </template>
    </AppListHeader>

    <AppFilterDrawer v-model="filtersOpen" title="Filter customers" @clear="clearFilters">
      <AppSelect v-model="groupFilter" label="Customer group" :options="groupFilterOptions" @update:model-value="reload" />
      <AppSelect v-model="activeFilter" label="Account status" :options="activeOptions" @update:model-value="reload" />
      <AppSelect v-model="verifiedFilter" label="Email verified" :options="verifiedOptions" @update:model-value="reload" />
      <div>
        <AppFieldLabel label="Registered from" />
        <q-input v-model="regFrom" dense outlined type="date" @update:model-value="reload" />
      </div>
      <div>
        <AppFieldLabel label="Registered to" />
        <q-input v-model="regTo" dense outlined type="date" @update:model-value="reload" />
      </div>
    </AppFilterDrawer>

    <AppDataTable page-key="admin-customers" row-key="id" title="All customers" :rows="rows" :columns="columns" :loading="loading" :pagination="pagination" show-actions @request="onRequest" @refresh="reload">
      <template #body-cell-name="cell"><q-td :props="cell"><a class="text-primary cursor-pointer text-weight-medium" @click="view(cell.row)">{{ cell.row.firstName }} {{ cell.row.lastName }}</a></q-td></template>
      <template #body-cell-customerGroupName="cell"><q-td :props="cell"><q-badge v-if="cell.row.customerGroupName" color="indigo" outline :label="cell.row.customerGroupName" /><span v-else class="text-grey-6">—</span></q-td></template>
      <template #body-cell-totalSpent="cell"><q-td :props="cell" class="text-right">{{ formatMoney(cell.row.totalSpent) }}</q-td></template>
      <template #body-cell-emailVerified="cell"><q-td :props="cell"><q-badge :color="cell.row.emailVerified ? 'positive' : 'orange'" :label="cell.row.emailVerified ? 'Verified' : 'Unverified'" /></q-td></template>
      <template #body-cell-isActive="cell"><q-td :props="cell"><q-badge :color="cell.row.isActive ? 'positive' : 'grey'" :label="cell.row.isActive ? 'Active' : 'Inactive'" /></q-td></template>
      <template #body-cell-createdOnUtc="cell"><q-td :props="cell">{{ formatDate(cell.row.createdOnUtc) }}</q-td></template>
      <template #actions="{ row }">
        <q-btn flat round dense icon="o_tune" @click="view(row)"><q-tooltip>View / edit</q-tooltip></q-btn>
      </template>
    </AppDataTable>
  </q-page>
</template>

<script setup>
/* Admin customer list (WO-117): search + advanced filters (single Customer Group, status,
 * email-verified, registration-date range) with server-side paging/sort. The detail page is
 * the only editor — group assignment, activate/deactivate and read-only tax status live there. */
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'
import { customerAdminApi, customerGroupOptionsApi } from 'modules/customers/api'
import { formatDateTime as formatDate } from 'src/utils/datetime'
import { formatMoney } from 'modules/orders/api'
import AppListHeader from 'components/common/AppListHeader.vue'
import AppDataTable from 'components/common/AppDataTable.vue'
import AppFieldLabel from 'components/common/AppFieldLabel.vue'

const notify = useNotify()
const router = useRouter()

function view (row) { router.push({ name: 'admin-customer-detail', params: { id: row.id } }) }

const columns = [
  { name: 'name', label: 'Name', field: 'firstName', align: 'left', sortable: true },
  { name: 'email', label: 'Email', field: 'email', align: 'left', sortable: true },
  { name: 'customerGroupName', label: 'Customer group', field: (r) => r.customerGroupName || '—', align: 'left', sortable: true },
  { name: 'orderCount', label: 'Orders', field: 'orderCount', align: 'center' },
  { name: 'totalSpent', label: 'Total spend', field: (r) => formatMoney(r.totalSpent), align: 'right' },
  { name: 'emailVerified', label: 'Verified', field: 'emailVerified', align: 'center', sortable: true },
  { name: 'isActive', label: 'Active', field: 'isActive', align: 'center', sortable: true },
  { name: 'createdOnUtc', label: 'Joined', field: 'createdOnUtc', align: 'left', sortable: true }
]

// Sentinel for the "No group (base pricing)" filter story → hasCustomerGroup=false.
const NO_GROUP = '__none__'

const verifiedOptions = [
  { label: 'All', value: null },
  { label: 'Verified', value: true },
  { label: 'Unverified', value: false }
]
const activeOptions = [
  { label: 'All', value: null },
  { label: 'Active', value: true },
  { label: 'Inactive', value: false }
]
const groupOptions = ref([]) // active groups as { label, value }
const groupFilterOptions = computed(() => [
  { label: 'All groups', value: null },
  { label: 'No group (base pricing)', value: NO_GROUP },
  ...groupOptions.value
])

const rows = ref([])
const loading = ref(false)
const search = ref('')
const verifiedFilter = ref(null)
const activeFilter = ref(null)
const groupFilter = ref(null)
const regFrom = ref('')
const regTo = ref('')
const filtersOpen = ref(false)
const pagination = ref({ page: 1, rowsPerPage: 20, rowsNumber: 0 })

const activeFilterCount = computed(() =>
  (verifiedFilter.value !== null ? 1 : 0) +
  (activeFilter.value !== null ? 1 : 0) +
  (groupFilter.value !== null ? 1 : 0) +
  (regFrom.value ? 1 : 0) +
  (regTo.value ? 1 : 0)
)

function clearFilters () {
  verifiedFilter.value = null
  activeFilter.value = null
  groupFilter.value = null
  regFrom.value = ''
  regTo.value = ''
  reload()
}

async function fetch (props) {
  const p = props?.pagination || pagination.value
  loading.value = true
  try {
    const r = await customerAdminApi.list({
      page: p.page,
      pageSize: p.rowsPerPage,
      search: search.value || undefined,
      emailVerified: verifiedFilter.value === null ? undefined : verifiedFilter.value,
      isActive: activeFilter.value === null ? undefined : activeFilter.value,
      customerGroupId: (groupFilter.value && groupFilter.value !== NO_GROUP) ? groupFilter.value : undefined,
      hasCustomerGroup: groupFilter.value === NO_GROUP ? false : undefined,
      registeredFromUtc: regFrom.value || undefined,
      registeredToUtc: regTo.value || undefined,
      sortBy: p.sortBy || undefined,
      sortDescending: !!p.descending
    })
    rows.value = Array.isArray(r?.items) ? r.items : []
    pagination.value = { ...p, rowsNumber: r?.totalCount ?? rows.value.length }
  } catch (e) { rows.value = []; notify.error(getApiErrorMessage(e)) } finally { loading.value = false }
}
function onRequest (props) { fetch(props) }
function reload () { fetch({ pagination: { ...pagination.value, page: 1 } }) }

async function loadGroupOptions () {
  try {
    const r = await customerGroupOptionsApi.listActive()
    const items = Array.isArray(r) ? r : r?.items || []
    groupOptions.value = items.map((g) => ({ label: g.name, value: g.id }))
  } catch (e) { groupOptions.value = [] }
}

onMounted(() => { fetch(); loadGroupOptions() })
</script>
