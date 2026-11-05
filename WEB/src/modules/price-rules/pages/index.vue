<template>
  <q-page class="app-page">
    <AppListHeader
      title="Price Rules"
      subtitle="Automatic catalog discounts by category and customer group — no coupon needed."
      :breadcrumbs="[{ label: 'Home', icon: 'o_home', to: '/dashboard' }, { label: 'Catalog' }, { label: 'Price Rules' }]"
      :show-add="canWrite"
      add-label="Add New Price Rule"
      @add="onAdd"
    >
      <template #actions>
        <q-input
          v-model="search"
          dense
          outlined
          debounce="400"
          placeholder="Search by name"
          style="min-width: 220px"
          @update:model-value="reload"
        >
          <template #prepend><q-icon name="o_search" /></template>
          <template v-if="search" #append><q-icon name="o_close" class="cursor-pointer" @click="search = ''; reload()" /></template>
        </q-input>
        <q-select
          v-model="status"
          dense
          outlined
          emit-value
          map-options
          :options="statusOptions"
          style="min-width: 140px"
          @update:model-value="reload"
        />
        <q-btn
          v-if="canWrite"
          outline
          no-caps
          color="primary"
          icon="o_published_with_changes"
          label="Re-apply All Rules"
          :loading="applyingAll"
          @click="onApplyAll"
        />
      </template>
    </AppListHeader>

    <AppDataTable
      page-key="catalog-price-rules"
      row-key="id"
      title="All price rules"
      :rows="rows"
      :columns="columns"
      :loading="loading"
      :pagination="pagination"
      show-actions
      @request="onRequest"
      @refresh="reload"
    >
      <template #body-cell-name="cell">
        <q-td :props="cell">
          <a class="text-primary cursor-pointer text-weight-medium" @click="onEdit(cell.row)">{{ cell.row.name }}</a>
          <div v-if="cell.row.storeName" class="text-caption text-grey-7">{{ cell.row.storeName }}</div>
        </q-td>
      </template>

      <template #body-cell-isActive="cell">
        <q-td :props="cell">
          <q-badge :color="cell.row.isActive ? 'positive' : 'grey'" :label="cell.row.isActive ? 'Active' : 'Inactive'" />
        </q-td>
      </template>

      <template #body-cell-effectiveState="cell">
        <q-td :props="cell">
          <q-badge outline :color="EFFECTIVE_STATE_COLORS[cell.row.effectiveState] || 'grey'" :label="cell.row.effectiveState" />
        </q-td>
      </template>

      <template #body-cell-priority="cell">
        <q-td :props="cell">
          {{ cell.row.priority }}
          <q-icon v-if="cell.row.stopFurtherRules" name="o_block" size="16px" color="warning" class="q-ml-xs">
            <q-tooltip>Stops lower-priority rules</q-tooltip>
          </q-icon>
        </q-td>
      </template>

      <template #actions="{ row }">
        <q-btn v-if="canWrite" flat round dense icon="o_edit" @click="onEdit(row)">
          <q-tooltip>Edit</q-tooltip>
        </q-btn>
        <q-btn v-if="canWrite" flat round dense icon="o_delete" color="negative" @click="onDelete(row)">
          <q-tooltip>Delete</q-tooltip>
        </q-btn>
      </template>
    </AppDataTable>
  </q-page>
</template>

<script setup>
/*
 * Price Rules list (Catalog → Price Rules). Server-side paging (25/page, name ascending), search + status
 * filters. Prices are pre-calculated in the background, so "Products affected" / "Last applied" can lag a
 * save by a moment — "Re-apply All Rules" queues a rebuild of every rule.
 */
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { date } from 'quasar'
import { getApiErrorMessage } from 'services/api'
import { priceRuleApi, formatDiscount, EFFECTIVE_STATE_COLORS } from 'modules/price-rules/api'
import { usePermissions } from 'composables/usePermissions'
import { useNotify } from 'composables/useNotify'
import { deleteConfirmation } from 'dialogs/delete_confirmation'
import { confirmation } from 'dialogs/confirmation'

const router = useRouter()
const notify = useNotify()
const { has } = usePermissions()
const canWrite = computed(() => has('Catalog.Write'))

const statusOptions = [
  { label: 'All statuses', value: null },
  { label: 'Active', value: true },
  { label: 'Inactive', value: false }
]

const formatDate = (v) => v || '—'
const formatDateTime = (v) => (v ? date.formatDate(v, 'YYYY-MM-DD HH:mm') : 'Never')

const columns = [
  { name: 'id', label: 'ID', field: 'id', align: 'left', format: (v) => v.slice(0, 8), classes: 'text-grey-7 text-caption' },
  { name: 'name', label: 'Name', field: 'name', align: 'left', sortable: true },
  { name: 'isActive', label: 'Status', field: 'isActive', align: 'center', sortable: true },
  { name: 'effectiveState', label: 'Effective State', field: 'effectiveState', align: 'center' },
  { name: 'discountAmount', label: 'Discount', field: 'discountAmount', align: 'right', sortable: true, format: (_, row) => formatDiscount(row.discountType, row.discountAmount) },
  { name: 'priority', label: 'Priority', field: 'priority', align: 'right', sortable: true },
  { name: 'startDate', label: 'Start', field: 'startDate', align: 'left', sortable: true, format: formatDate },
  { name: 'endDate', label: 'End', field: 'endDate', align: 'left', sortable: true, format: formatDate },
  { name: 'productsAffected', label: 'Products affected', field: 'productsAffected', align: 'right' },
  { name: 'lastAppliedAtUtc', label: 'Last applied', field: 'lastAppliedAtUtc', align: 'left', sortable: true, format: formatDateTime }
]

const rows = ref([])
const loading = ref(false)
const applyingAll = ref(false)
const search = ref('')
const status = ref(null)
const pagination = ref({ page: 1, rowsPerPage: 25, rowsNumber: 0, sortBy: 'name', descending: false })

async function fetch (props) {
  const p = props?.pagination || pagination.value
  loading.value = true
  try {
    const result = await priceRuleApi.list({
      page: p.page,
      pageSize: p.rowsPerPage,
      search: search.value || undefined,
      isActive: status.value ?? undefined,
      sort: p.sortBy || undefined,
      dir: p.sortBy ? (p.descending ? 'desc' : 'asc') : undefined
    })
    rows.value = result?.items || []
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

function onAdd () { router.push({ name: 'catalog-price-rule-new' }) }
function onEdit (row) { router.push({ name: 'catalog-price-rule-detail', params: { id: row.id } }) }

async function onDelete (row) {
  if (!(await deleteConfirmation(`the price rule "${row.name}"`))) return
  try {
    await priceRuleApi.remove(row.id)
    notify.success('Price Rule deleted successfully')
    reload()
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  }
}

async function onApplyAll () {
  const ok = await confirmation({
    title: 'Re-apply all rules?',
    message: 'Every price rule will be recalculated in the background. Storefront prices update as each rule finishes.',
    okLabel: 'Re-apply'
  })
  if (!ok) return
  applyingAll.value = true
  try {
    const result = await priceRuleApi.applyAll()
    notify.success(`Queued ${result?.queued ?? 0} rule(s) for re-apply`)
    // Give the background rebuild a moment, then refresh the counts.
    setTimeout(() => fetch(), 2500)
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    applyingAll.value = false
  }
}

onMounted(() => fetch())
</script>
