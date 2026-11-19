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
        <q-btn v-if="canWrite" color="primary" unelevated no-caps icon="o_add" label="Add customer" class="q-ml-sm" @click="addCustomer" />
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

    <!-- Bulk selection bar: the header checkbox ticks this page; "Select all" widens it to every matching customer. -->
    <q-banner v-if="canBlacklist && (selected.length || allMatching)" dense class="bg-blue-1 text-blue-10 rounded-borders q-mb-md">
      <div class="row items-center q-gutter-x-sm">
        <span v-if="allMatching" class="text-weight-medium">All {{ totalCount }} customers matching the current filters are selected.</span>
        <template v-else>
          <span class="text-weight-medium">{{ selected.length }} customer{{ selected.length === 1 ? '' : 's' }} selected.</span>
          <q-btn
            v-if="totalCount > selected.length"
            flat dense no-caps color="primary"
            :label="`Select all ${totalCount} customers`"
            @click="selectAllMatching"
          />
        </template>
        <q-btn flat dense no-caps color="grey-8" label="Clear selection" @click="clearSelection" />
        <q-space />
        <q-btn
          unelevated no-caps color="negative" icon="o_block"
          :label="`Block ${bulkCount} customer${bulkCount === 1 ? '' : 's'}`"
          @click="openBulkBlacklist"
        />
      </div>
    </q-banner>

    <AppDataTable v-model:selected="selected" page-key="admin-customers" row-key="id" title="All customers" :rows="rows" :columns="columns" :loading="loading" :pagination="pagination" :selectable="canBlacklist" show-actions @request="onRequest" @refresh="reload">
      <template #body-cell-name="cell"><q-td :props="cell"><a class="text-primary cursor-pointer text-weight-medium" @click="view(cell.row)">{{ cell.row.firstName }} {{ cell.row.lastName }}</a></q-td></template>
      <template #body-cell-customerGroupName="cell"><q-td :props="cell"><q-badge v-if="cell.row.customerGroupName" color="indigo" outline :label="cell.row.customerGroupName" /><span v-else class="text-grey-6">—</span></q-td></template>
      <template #body-cell-totalSpent="cell"><q-td :props="cell" class="text-right">{{ formatMoney(cell.row.totalSpent) }}</q-td></template>
      <template #body-cell-emailVerified="cell"><q-td :props="cell"><q-badge :color="cell.row.emailVerified ? 'positive' : 'orange'" :label="cell.row.emailVerified ? 'Verified' : 'Unverified'" /></q-td></template>
      <template #body-cell-isActive="cell"><q-td :props="cell"><q-badge :color="cell.row.isActive ? 'positive' : 'grey'" :label="cell.row.isActive ? 'Active' : 'Inactive'" /></q-td></template>
      <template #body-cell-createdOnUtc="cell"><q-td :props="cell">{{ formatDate(cell.row.createdOnUtc) }}</q-td></template>
      <template #actions="{ row }">
        <q-btn flat round dense icon="o_tune" @click="view(row)"><q-tooltip>View / edit</q-tooltip></q-btn>
        <q-btn v-if="canBlacklist" flat round dense icon="o_block" color="negative" @click="openBlacklist(row)"><q-tooltip>Blacklist</q-tooltip></q-btn>
        <q-btn v-if="canWrite" flat round dense icon="o_delete" color="negative" :loading="deletingId === row.id" :disable="!!deletingId" @click="onDelete(row)"><q-tooltip>Delete</q-tooltip></q-btn>
      </template>
    </AppDataTable>

    <BlacklistCustomerDialog v-model="blacklistOpen" :customer="blacklistRow" @blocked="onBlocked" />
    <BlacklistCustomerDialog v-model="bulkBlacklistOpen" :bulk="bulkPayload" @blocked="onBulkBlocked" />
  </q-page>
</template>

<script setup>
/* Admin customer list (WO-117) + "Add customer" button (opens the customer page in create mode): search + advanced filters (single Customer Group, status,
 * email-verified, registration-date range) with server-side paging/sort. The detail page is
 * the only editor — group assignment, activate/deactivate and read-only tax status live there.
 * Actions → Blacklist (Customers.Blacklist) opens BlacklistCustomerDialog; blacklisted customers are excluded server-side. */
import { ref, computed, watch, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'
import { usePermissions } from 'composables/usePermissions'
import { customerAdminApi, customerGroupOptionsApi } from 'modules/customers/api'
import { formatDateTime as formatDate } from 'src/utils/datetime'
import { formatMoney } from 'modules/orders/api'
import AppListHeader from 'components/common/AppListHeader.vue'
import AppDataTable from 'components/common/AppDataTable.vue'
import AppFieldLabel from 'components/common/AppFieldLabel.vue'
import BlacklistCustomerDialog from 'modules/customers/components/BlacklistCustomerDialog.vue'
import { deleteConfirmation } from 'dialogs/delete_confirmation'

const notify = useNotify()
const router = useRouter()
const { has, Permissions } = usePermissions()
const canWrite = computed(() => has('Users.Write'))
const canBlacklist = computed(() => has(Permissions.CustomersBlacklist))

// Opens the customer page in create mode (only the General tab is enabled until saved).
function addCustomer () { router.push({ name: 'admin-customer-create' }) }

function view (row) { router.push({ name: 'admin-customer-detail', params: { id: row.id } }) }

// Soft delete (Users.Write): record is kept server-side, hidden from this list, and the login is deactivated.
const deletingId = ref(null)
async function onDelete (row) {
  if (deletingId.value) return
  const name = `${row.firstName || ''} ${row.lastName || ''}`.trim() || 'this customer'
  const ok = await deleteConfirmation(name, {
    title: 'Delete customer',
    message: `Delete "${name}" (${row.email})? Their account will be deactivated and they will no longer be able to sign in. Orders and history are kept.`
  })
  if (!ok) return
  deletingId.value = row.id
  try {
    await customerAdminApi.remove(row.id)
    notify.success(`Customer "${name}" deleted`)
    await refreshCurrentPage()
  } catch (e) { notify.error(getApiErrorMessage(e)) } finally { deletingId.value = null }
}

// Blacklist (Customers.Blacklist): modal captures the reason; on success the row drops out of the list.
const blacklistOpen = ref(false)
const blacklistRow = ref(null)
function openBlacklist (row) {
  blacklistRow.value = row
  blacklistOpen.value = true
}
async function onBlocked (row) {
  notify.success('Customer has been added to the blacklist')
  selected.value = selected.value.filter((r) => r.id !== row?.id)
  await refreshCurrentPage()
}

// Bulk block: ticked rows (kept across pages), or — after "Select all" — every customer matching the filters.
const selected = ref([])
const allMatching = ref(false)
const totalCount = computed(() => pagination.value.rowsNumber || 0)
const bulkCount = computed(() => (allMatching.value ? totalCount.value : selected.value.length))
const bulkBlacklistOpen = ref(false)
const bulkPayload = ref(null)
function clearSelection () {
  selected.value = []
  allMatching.value = false
}
function selectAllMatching () {
  selected.value = [...rows.value]
  allMatching.value = true
}
function openBulkBlacklist () {
  bulkPayload.value = {
    count: bulkCount.value,
    allMatching: allMatching.value,
    customerIds: allMatching.value ? null : selected.value.map((r) => r.id),
    filter: allMatching.value ? currentFilter() : null
  }
  bulkBlacklistOpen.value = true
}
async function onBulkBlocked (result) {
  const blocked = result?.blocked ?? 0
  const skipped = result?.skipped ?? 0
  notify.success(`${blocked} customer${blocked === 1 ? '' : 's'} added to the blacklist` +
    (skipped ? ` (${skipped} skipped: already blocked or not a customer account)` : ''))
  clearSelection()
  await fetch({ pagination: { ...pagination.value, page: 1 } })
}
// Unticking a row while "all matching" is on drops back to an explicit selection.
watch(selected, () => { if (allMatching.value && selected.value.length < rows.value.length) allMatching.value = false })

// Re-fetch with the current page/sort/filters; step back a page if the removed row was the last one on it.
async function refreshCurrentPage () {
  const p = pagination.value
  const page = rows.value.length === 1 && p.page > 1 ? p.page - 1 : p.page
  await fetch({ pagination: { ...p, page } })
}

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

// The search + Advanced filters, as the list endpoint and the bulk "all matching" block both take them.
function currentFilter () {
  return {
    search: search.value || undefined,
    emailVerified: verifiedFilter.value === null ? undefined : verifiedFilter.value,
    isActive: activeFilter.value === null ? undefined : activeFilter.value,
    customerGroupId: (groupFilter.value && groupFilter.value !== NO_GROUP) ? groupFilter.value : undefined,
    hasCustomerGroup: groupFilter.value === NO_GROUP ? false : undefined,
    registeredFromUtc: regFrom.value || undefined,
    registeredToUtc: regTo.value || undefined
  }
}

async function fetch (props) {
  const p = props?.pagination || pagination.value
  loading.value = true
  try {
    const r = await customerAdminApi.list({
      page: p.page,
      pageSize: p.rowsPerPage,
      ...currentFilter(),
      sortBy: p.sortBy || undefined,
      sortDescending: !!p.descending
    })
    rows.value = Array.isArray(r?.items) ? r.items : []
    pagination.value = { ...p, rowsNumber: r?.totalCount ?? rows.value.length }
  } catch (e) { rows.value = []; notify.error(getApiErrorMessage(e)) } finally { loading.value = false }
}
function onRequest (props) { fetch(props) }
// A new search / filter changes which customers "selected" means, so it starts a fresh selection.
function reload () { clearSelection(); fetch({ pagination: { ...pagination.value, page: 1 } }) }

async function loadGroupOptions () {
  try {
    const r = await customerGroupOptionsApi.listActive()
    const items = Array.isArray(r) ? r : r?.items || []
    groupOptions.value = items.map((g) => ({ label: g.name, value: g.id }))
  } catch (e) { groupOptions.value = [] }
}

onMounted(() => { fetch(); loadGroupOptions() })
</script>