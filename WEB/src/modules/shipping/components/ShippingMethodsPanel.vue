<template>
  <div>
    <AppFilterDrawer v-model="filtersOpen" title="Filter methods" @clear="clearFilters">
      <AppSelect v-model="typeFilter" label="Calculation type" clearable placeholder="Any type" :options="shippingMethodTypeOptions" @update:model-value="reload" />
      <AppSelect v-model="enabledFilter" label="Status" :options="statusOptions" @update:model-value="reload" />
    </AppFilterDrawer>
    <AppDataTable page-key="shipping-methods" row-key="id" title="Shipping methods" :rows="rows" :columns="columns" :loading="loading" :pagination="pagination" show-actions @request="onRequest" @refresh="reload">
      <template #body-cell-name="cell"><q-td :props="cell"><a class="text-primary cursor-pointer text-weight-medium" @click="onManage(cell.row)">{{ cell.row.name }}</a></q-td></template>
      <template #body-cell-isEnabled="cell"><q-td :props="cell"><q-badge :color="cell.row.isEnabled ? 'positive' : 'grey'" :label="cell.row.isEnabled ? 'On' : 'Off'" /></q-td></template>
      <template #actions="{ row }">
        <q-btn v-if="canWrite" flat round dense icon="o_tune" @click="onManage(row)" /><q-btn v-if="canWrite" flat round dense icon="o_delete" color="negative" @click="onDelete(row)" />
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
import { shippingMethodApi, shippingMethodTypeOptions } from 'modules/shipping/api'
import AppDataTable from 'components/common/AppDataTable.vue'

// Toolbar lives in the parent tab page; driven via `search` prop + exposed onAdd/openFilters + filter-count event.
const props = defineProps({ search: { type: String, default: '' } })
const emit = defineEmits(['filter-count'])

const router = useRouter()
const notify = useNotify()
const { has } = usePermissions()
const canWrite = computed(() => has('Stores.Write'))
const columns = [
  { name: 'name', label: 'Name', field: 'name', align: 'left', sortable: true },
  { name: 'methodType', label: 'Type', field: 'methodType', align: 'left', sortable: true },
  { name: 'flatRate', label: 'Flat rate', field: (r) => r.flatRate ?? '—', align: 'right', sortable: true },
  { name: 'displayOrder', label: 'Order', field: 'displayOrder', align: 'right', sortable: true },
  { name: 'isEnabled', label: 'Status', field: 'isEnabled', align: 'center', sortable: true }
]
const statusOptions = [
  { label: 'All', value: null },
  { label: 'On', value: true },
  { label: 'Off', value: false }
]

const rows = ref([]); const loading = ref(false); const pagination = ref({ page: 1, rowsPerPage: 10, rowsNumber: 0 })
const typeFilter = ref(null); const enabledFilter = ref(null); const filtersOpen = ref(false)

const activeFilterCount = computed(() => (typeFilter.value ? 1 : 0) + (enabledFilter.value !== null ? 1 : 0))

watch(activeFilterCount, (v) => emit('filter-count', v), { immediate: true })
watch(() => props.search, () => reload())

function openFilters () { filtersOpen.value = true }
defineExpose({ onAdd, openFilters })

function clearFilters () {
  typeFilter.value = null
  enabledFilter.value = null
  reload()
}

async function fetch (req) {
  const p = req?.pagination || pagination.value; loading.value = true
  try {
    const r = await shippingMethodApi.list({
      page: p.page,
      pageSize: p.rowsPerPage,
      search: props.search || undefined,
      methodType: typeFilter.value || undefined,
      isEnabled: enabledFilter.value === null ? undefined : enabledFilter.value,
      sortBy: p.sortBy || undefined,
      sortDescending: !!p.descending
    })
    const items = Array.isArray(r) ? r : r?.items || []
    rows.value = items; pagination.value = { ...p, rowsNumber: Array.isArray(r) ? items.length : r?.totalCount ?? items.length }
  } catch (e) { rows.value = []; notify.error(getApiErrorMessage(e)) } finally { loading.value = false }
}
function onRequest (req) { fetch(req) }
function reload () { fetch({ pagination: { ...pagination.value, page: 1 } }) }
function onAdd () { router.push({ name: 'shipping-method-new' }) }
function onManage (row) { router.push({ name: 'shipping-method-detail', params: { id: row.id } }) }
async function onDelete (row) {
  if (!(await deleteConfirmation(`the shipping method "${row.name}"`))) return
  try { await shippingMethodApi.remove(row.id); notify.success('Method deleted'); reload() } catch (e) { notify.error(getApiErrorMessage(e)) }
}
onMounted(() => fetch())
</script>
