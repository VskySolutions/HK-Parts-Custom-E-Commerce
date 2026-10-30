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
        >
          <template #prepend><q-icon name="o_search" /></template>
          <template v-if="search" #append><q-icon name="o_close" class="cursor-pointer" @click="search = ''" /></template>
        </q-input>
      </template>
    </AppListHeader>

    <AppDataTable
      page-key="admin-customer-groups"
      row-key="id"
      title="All groups"
      :rows="filteredRows"
      :columns="columns"
      :loading="loading"
      :pagination="pagination"
      show-actions
      no-data-label="No customer groups yet."
      @refresh="load"
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
  { name: 'name', label: 'Name', field: 'name', align: 'left' },
  { name: 'pricingRuleType', label: 'Pricing rule', field: 'pricingRuleType', align: 'left' },
  { name: 'discountPercent', label: 'Discount %', field: 'discountPercent', align: 'left' },
  { name: 'isActive', label: 'Active', field: 'isActive', align: 'center' },
  { name: 'displayOrder', label: 'Display order', field: 'displayOrder', align: 'left' }
]

// Client-side table: customer-group lists are small, so show every group on one page (no rowsNumber).
const pagination = ref({ rowsPerPage: 0 })

const rows = ref([])
const loading = ref(false)
const search = ref('')

const filteredRows = computed(() => {
  const q = search.value.trim().toLowerCase()
  if (!q) return rows.value
  return rows.value.filter((g) =>
    (g.name || '').toLowerCase().includes(q) ||
    (g.description || '').toLowerCase().includes(q)
  )
})

// Humanize CustomerGroupPricingRuleType (None | PercentageDiscount | FixedGroupPrice) via the shared options.
function pricingRuleLabel (value) {
  const match = pricingRuleOptions.find((o) => o.value === value)
  return match ? match.label : (value || '—')
}

async function load () {
  loading.value = true
  try {
    const result = await customerGroupApi.list()
    rows.value = Array.isArray(result) ? result : result?.items || result?.data || []
  } catch (err) {
    rows.value = []
    notify.error(getApiErrorMessage(err))
  } finally {
    loading.value = false
  }
}

function onAdd () { router.push({ name: 'admin-customer-group-new' }) }
function onManage (row) { router.push({ name: 'admin-customer-group-detail', params: { id: row.id } }) }

async function onDelete (row) {
  if (!(await deleteConfirmation(`the customer group "${row.name}"`))) return
  try {
    await customerGroupApi.remove(row.id)
    notify.success('Customer group deleted')
    await load()
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  }
}

onMounted(load)
</script>
