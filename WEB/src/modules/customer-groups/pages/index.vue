<template>
  <q-page class="app-page">
    <AppListHeader
      title="Customer groups"
      subtitle="Buyer segments and the pricing rule each one gets."
      :breadcrumbs="[{ label: 'Home', icon: 'o_home', to: '/dashboard' }, { label: 'Customer groups' }]"
      show-add
      add-label="New group"
      @add="onAdd"
    >
      <template #actions>
        <q-input
          v-model="search"
          dense
          outlined
          debounce="300"
          placeholder="Search"
          style="min-width: 240px"
          @update:model-value="reload"
        >
          <template #prepend><q-icon name="o_search" /></template>
          <template v-if="search" #append><q-icon name="o_close" class="cursor-pointer" @click="search = ''; reload()" /></template>
        </q-input>
      </template>
    </AppListHeader>

    <AppDataTable
      page-key="admin-customer-groups"
      row-key="id"
      title="All groups"
      :rows="rows"
      :columns="columns"
      :loading="loading"
      :pagination="pagination"
      show-actions
      no-data-label="No customer groups yet."
      @request="onRequest"
      @refresh="reload"
    >
      <template #body-cell-name="cell">
        <q-td :props="cell">
          <a class="text-primary cursor-pointer text-weight-medium" @click="onManage(cell.row)">{{ cell.row.name }}</a>
        </q-td>
      </template>

      <template #body-cell-pricingRuleType="cell">
        <q-td :props="cell">{{ pricingRuleLabel(cell.row.pricingRuleType) }}</q-td>
      </template>

      <template #body-cell-discountPercent="cell">
        <q-td :props="cell">
          <span v-if="cell.row.pricingRuleType === 'PercentageDiscount' && cell.row.discountPercent != null">{{ cell.row.discountPercent }}%</span>
          <span v-else class="text-grey-6">—</span>
        </q-td>
      </template>

      <template #body-cell-isActive="cell">
        <q-td :props="cell">
          <q-badge
            :color="cell.row.isActive ? 'positive' : 'grey'"
            :label="cell.row.isActive ? 'Active' : 'Inactive'"
          />
        </q-td>
      </template>

      <template #actions="{ row }">
        <q-btn flat round dense icon="o_tune" @click="onManage(row)">
          <q-tooltip>Edit</q-tooltip>
        </q-btn>
        <q-btn flat round dense icon="o_delete" color="negative" @click="onDelete(row)">
          <q-tooltip>Delete</q-tooltip>
        </q-btn>
      </template>
    </AppDataTable>
  </q-page>
</template>

<script setup>
/*
 * Customer Groups list page (WO-22): AppListHeader + AppDataTable. The list endpoint returns a
 * plain (unpaged) array, so the table runs client-side — rowsPerPage 0 shows every group on one
 * page and the search box filters the loaded rows in memory. Create/edit open the full-page detail
 * (`admin-customer-group-new` / `admin-customer-group-detail`).
 */
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { customerGroupApi, pricingRuleOptions } from '../api'
import { getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'
import { deleteConfirmation } from 'dialogs/delete_confirmation'

const router = useRouter()
const notify = useNotify()

const columns = [
  { name: 'name', label: 'Name', field: 'name', align: 'left', sortable: true },
  { name: 'pricingRuleType', label: 'Pricing rule', field: 'pricingRuleType', align: 'left', sortable: true },
  { name: 'discountPercent', label: 'Discount %', field: 'discountPercent', align: 'left', sortable: true },
  { name: 'isActive', label: 'Active', field: 'isActive', align: 'center', sortable: true },
  { name: 'displayOrder', label: 'Display order', field: 'displayOrder', align: 'right', sortable: true }
]

// Client-side table: customer-group lists are small, so show every group on one page (no rowsNumber).
const rows = ref([])
const loading = ref(false)
const search = ref('')
const pagination = ref({ page: 1, rowsPerPage: 10, rowsNumber: 0 })

async function fetch (props) {
  const p = props?.pagination || pagination.value
  loading.value = true
  try {
    const result = await customerGroupApi.list({
      page: p.page,
      pageSize: p.rowsPerPage,
      search: search.value || undefined,
      sortBy: p.sortBy || undefined,
      sortDescending: !!p.descending
    })
    const items = Array.isArray(result) ? result : result?.items || []
    const total = Array.isArray(result) ? result.length : result?.totalCount ?? items.length
    rows.value = items
    pagination.value = { ...p, rowsNumber: total }
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

// Humanize CustomerGroupPricingRuleType (None | PercentageDiscount | FixedGroupPrice) via the shared options.
function pricingRuleLabel (value) {
  const match = pricingRuleOptions.find((o) => o.value === value)
  return match ? match.label : (value || '—')
}

function onAdd () { router.push({ name: 'admin-customer-group-new' }) }
function onManage (row) { router.push({ name: 'admin-customer-group-detail', params: { id: row.id } }) }

async function onDelete (row) {
  if (!(await deleteConfirmation(`the customer group "${row.name}"`))) return
  try {
    await customerGroupApi.remove(row.id)
    notify.success('Customer group deleted')
    await reload()
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  }
}

onMounted(() => fetch())
</script>
