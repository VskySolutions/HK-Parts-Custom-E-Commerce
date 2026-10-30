<template>
  <div>
    <AppFilterDrawer v-model="filtersOpen" title="Filter zones" @clear="clearFilters">
      <AppSelect v-model="enabledFilter" label="Status" :options="statusOptions" @update:model-value="reload" />
    </AppFilterDrawer>
    <AppDataTable page-key="shipping-zones" row-key="id" title="Shipping zones" :rows="rows" :columns="columns" :loading="loading" :pagination="pagination" show-actions @request="onRequest" @refresh="reload">
      <template #body-cell-name="cell"><q-td :props="cell"><a class="text-primary cursor-pointer text-weight-medium" @click="onManage(cell.row)">{{ cell.row.name }}</a></q-td></template>
      <template #body-cell-postal="cell"><q-td :props="cell">{{ cell.row.postalCodeStart ? `${cell.row.postalCodeStart}–${cell.row.postalCodeEnd || ''}` : '—' }}</q-td></template>
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
import { shippingZoneApi } from 'modules/shipping/api'
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
  { name: 'countryCode', label: 'Country', field: 'countryCode', align: 'left', sortable: true },
  { name: 'region', label: 'Region', field: (r) => r.region || '—', align: 'left', sortable: true },
  { name: 'postal', label: 'Postal range', field: 'postalCodeStart', align: 'left' },
  { name: 'isEnabled', label: 'Status', field: 'isEnabled', align: 'center', sortable: true }
]
const statusOptions = [
  { label: 'All', value: null },
  { label: 'On', value: true },
  { label: 'Off', value: false }
]

const rows = ref([]); const loading = ref(false); const pagination = ref({ page: 1, rowsPerPage: 10, rowsNumber: 0 })
const enabledFilter = ref(null); const filtersOpen = ref(false)

const activeFilterCount = computed(() => (enabledFilter.value !== null ? 1 : 0))

watch(activeFilterCount, (v) => emit('filter-count', v), { immediate: true })
watch(() => props.search, () => reload())

function openFilters () { filtersOpen.value = true }
defineExpose({ onAdd, openFilters })

function clearFilters () {
  enabledFilter.value = null
  reload()
}

async function fetch (req) {
  const p = req?.pagination || pagination.value; loading.value = true
  try {
    const r = await shippingZoneApi.list({
      page: p.page,
      pageSize: p.rowsPerPage,
      search: props.search || undefined,
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
function onAdd () { router.push({ name: 'shipping-zone-new' }) }
function onManage (row) { router.push({ name: 'shipping-zone-detail', params: { id: row.id } }) }
async function onDelete (row) {
  if (!(await deleteConfirmation(`the shipping zone "${row.name}"`))) return
  try { await shippingZoneApi.remove(row.id); notify.success('Zone deleted'); reload() } catch (e) { notify.error(getApiErrorMessage(e)) }
}
onMounted(() => fetch())
</script>
