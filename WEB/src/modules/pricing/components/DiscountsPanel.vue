<template>
  <div>
    <AppFilterDrawer v-model="filtersOpen" title="Filter discounts" @clear="clearFilters">
      <AppSelect v-model="scopeFilter" label="Scope" clearable placeholder="Any scope" :options="discountScopeOptions" @update:model-value="reload" />
      <AppSelect v-model="activeFilter" label="Status" :options="statusOptions" @update:model-value="reload" />
    </AppFilterDrawer>

    <AppDataTable page-key="pricing-discounts" row-key="id" title="Discounts" :rows="rows" :columns="columns" :loading="loading" :pagination="pagination" show-actions @request="onRequest" @refresh="reload">
      <template #body-cell-name="cell">
        <q-td :props="cell">
          <a class="text-primary cursor-pointer text-weight-medium" @click="onManage(cell.row)">{{ cell.row.name }}</a>
          <q-badge v-if="cell.row.requiresCoupon" color="deep-orange" outline class="q-ml-xs" label="Coupon">
            <q-tooltip>Applies only with a linked coupon code</q-tooltip>
          </q-badge>
        </q-td>
      </template>
      <template #body-cell-value="cell">
        <q-td :props="cell">{{ discountValueLabel(cell.row) }}</q-td>
      </template>
      <template #body-cell-isActive="cell">
        <q-td :props="cell"><q-badge :color="cell.row.isActive ? 'positive' : 'grey'" :label="cell.row.isActive ? 'Active' : 'Off'" /></q-td>
      </template>
      <template #actions="{ row }">
        <q-btn v-if="canWrite" flat round dense icon="o_tune" @click="onManage(row)"><q-tooltip>Edit</q-tooltip></q-btn>
        <q-btn v-if="canWrite" flat round dense icon="o_delete" color="negative" @click="onDelete(row)"><q-tooltip>Delete</q-tooltip></q-btn>
      </template>
    </AppDataTable>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, watch } from 'vue'
import { useRouter } from 'vue-router'
import { getApiErrorMessage } from 'services/api'
import { usePermissions } from 'composables/usePermissions'
import { useNotify } from 'composables/useNotify'
import { deleteConfirmation } from 'dialogs/delete_confirmation'
import { discountApi, discountValueLabel, discountScopeOptions } from 'modules/pricing/api'
import AppDataTable from 'components/common/AppDataTable.vue'

// Toolbar lives in the parent tab page; driven via `search` prop + exposed onAdd/openFilters + filter-count event.
const props = defineProps({ search: { type: String, default: '' } })
const emit = defineEmits(['filter-count'])

const router = useRouter()
const notify = useNotify()
const { has } = usePermissions()
const canWrite = computed(() => has('Catalog.Write'))

const columns = [
  { name: 'name', label: 'Name', field: 'name', align: 'left', sortable: true },
  { name: 'scope', label: 'Scope', field: 'scope', align: 'left', sortable: true },
  { name: 'type', label: 'Type', field: 'type', align: 'left', sortable: true },
  { name: 'value', label: 'Value', field: 'value', align: 'right', sortable: true },
  { name: 'isActive', label: 'Status', field: 'isActive', align: 'center', sortable: true }
]
const statusOptions = [
  { label: 'All', value: null },
  { label: 'Active', value: true },
  { label: 'Off', value: false }
]

const rows = ref([])
const loading = ref(false)
const scopeFilter = ref(null)
const activeFilter = ref(null)
const filtersOpen = ref(false)
const pagination = ref({ page: 1, rowsPerPage: 10, rowsNumber: 0 })

const activeFilterCount = computed(() => (scopeFilter.value ? 1 : 0) + (activeFilter.value !== null ? 1 : 0))

watch(activeFilterCount, (v) => emit('filter-count', v), { immediate: true })
watch(() => props.search, () => reload())

function openFilters () { filtersOpen.value = true }
defineExpose({ onAdd, openFilters })

function clearFilters () {
  scopeFilter.value = null
  activeFilter.value = null
  reload()
}

async function fetch (req) {
  const p = req?.pagination || pagination.value
  loading.value = true
  try {
    const result = await discountApi.list({
      page: p.page,
      pageSize: p.rowsPerPage,
      search: props.search || undefined,
      scope: scopeFilter.value || undefined,
      isActive: activeFilter.value === null ? undefined : activeFilter.value,
      sortBy: p.sortBy || undefined,
      sortDescending: !!p.descending
    })
    const items = Array.isArray(result) ? result : result?.items || []
    rows.value = items
    pagination.value = { ...p, rowsNumber: Array.isArray(result) ? items.length : result?.totalCount ?? items.length }
  } catch (err) { rows.value = []; notify.error(getApiErrorMessage(err)) } finally { loading.value = false }
}
function onRequest (req) { fetch(req) }
function reload () { fetch({ pagination: { ...pagination.value, page: 1 } }) }
function onAdd () { router.push({ name: 'discount-new' }) }
function onManage (row) { router.push({ name: 'discount-detail', params: { id: row.id } }) }
async function onDelete (row) {
  if (!(await deleteConfirmation(`the discount "${row.name}"`))) return
  try { await discountApi.remove(row.id); notify.success('Discount deleted'); reload() } catch (err) { notify.error(getApiErrorMessage(err)) }
}
onMounted(() => fetch())
</script>
