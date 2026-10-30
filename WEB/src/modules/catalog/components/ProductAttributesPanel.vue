<template>
  <div>
    <AppFilterDrawer v-model="filtersOpen" title="Filter attributes" @clear="clearFilters">
      <AppSelect v-model="displayTypeFilter" label="Display type" clearable placeholder="Any type" :options="attributeDisplayTypeOptions" @update:model-value="reload" />
    </AppFilterDrawer>

    <AppDataTable
      page-key="catalog-product-attributes"
      row-key="id"
      title="Product attributes"
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
          <a class="text-primary cursor-pointer text-weight-medium" @click="onManage(cell.row)">{{ cell.row.name }}</a>
        </q-td>
      </template>
      <template #body-cell-displayType="cell">
        <q-td :props="cell"><q-badge outline color="primary" :label="attributeDisplayTypeLabel(cell.row.displayType)" /></q-td>
      </template>
      <template #body-cell-valuesCount="cell">
        <q-td :props="cell">
          <span v-if="cell.row.displayType === 'CustomInput'" class="text-grey-6">—</span>
          <template v-else>{{ (cell.row.values || []).length }}</template>
        </q-td>
      </template>
      <template #body-cell-inUseCount="cell">
        <q-td :props="cell">
          <span :class="cell.row.inUseCount > 0 ? 'text-weight-medium' : 'text-grey-6'">{{ cell.row.inUseCount || 0 }}</span>
        </q-td>
      </template>

      <template #actions="{ row }">
        <q-btn v-if="canWrite" flat round dense icon="o_tune" @click="onManage(row)"><q-tooltip>Edit</q-tooltip></q-btn>
        <q-btn v-if="canWrite" flat round dense icon="o_delete" color="negative" @click="onDelete(row)"><q-tooltip>Delete</q-tooltip></q-btn>
      </template>
    </AppDataTable>
  </div>
</template>

<script setup>
/*
 * Product Attributes panel (WO-15): the global product-attribute library list.
 * Create/edit open the full-page product-attribute detail; delete has in-use protection.
 */
import { ref, computed, onMounted, watch } from 'vue'
import { useRouter } from 'vue-router'
import { getApiErrorMessage } from 'services/api'
import { productAttributeApi, attributeDisplayTypeOptions, attributeDisplayTypeLabel } from 'modules/catalog/api'
import { usePermissions } from 'composables/usePermissions'
import { useNotify } from 'composables/useNotify'
import { deleteConfirmation } from 'dialogs/delete_confirmation'
import AppDataTable from 'components/common/AppDataTable.vue'

// The parent tab page owns the search box + Advanced/Add buttons (shared toolbar next to the
// tabs) and drives this panel via the `search` prop + the exposed onAdd/openFilters and the
// `filter-count` event.
const props = defineProps({ search: { type: String, default: '' } })
const emit = defineEmits(['filter-count'])

const router = useRouter()
const notify = useNotify()
const { has } = usePermissions()
const canWrite = computed(() => has('Catalog.Write'))

const columns = [
  { name: 'name', label: 'Attribute Name', field: 'name', align: 'left', sortable: true },
  { name: 'displayType', label: 'Display Type', field: 'displayType', align: 'left', sortable: true },
  { name: 'valuesCount', label: 'Values', field: (r) => (r.values || []).length, align: 'center' },
  { name: 'inUseCount', label: 'In Use', field: 'inUseCount', align: 'center' }
]

const rows = ref([])
const loading = ref(false)
const displayTypeFilter = ref(null)
const filtersOpen = ref(false)
const pagination = ref({ page: 1, rowsPerPage: 10, rowsNumber: 0 })

const activeFilterCount = computed(() => (displayTypeFilter.value ? 1 : 0))

// Keep the parent toolbar's filter badge in sync.
watch(activeFilterCount, (v) => emit('filter-count', v), { immediate: true })
// Re-query when the shared search box changes.
watch(() => props.search, () => reload())

function openFilters () { filtersOpen.value = true }
defineExpose({ onAdd, openFilters })

function clearFilters () {
  displayTypeFilter.value = null
  reload()
}

async function fetch (req) {
  const p = req?.pagination || pagination.value
  loading.value = true
  try {
    const result = await productAttributeApi.list({
      page: p.page,
      pageSize: p.rowsPerPage,
      search: props.search || undefined,
      displayType: displayTypeFilter.value || undefined,
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

function onRequest (req) { fetch(req) }
function reload () { fetch({ pagination: { ...pagination.value, page: 1 } }) }

function onAdd () { router.push({ name: 'product-attribute-new' }) }
function onManage (row) { router.push({ name: 'product-attribute-detail', params: { id: row.id } }) }

async function onDelete (row) {
  const inUse = row.inUseCount || 0
  const message = inUse > 0
    ? `the attribute "${row.name}". It is assigned to ${inUse} product${inUse === 1 ? '' : 's'} — deleting it will remove it from ${inUse === 1 ? 'that product' : 'those products'}`
    : `the attribute "${row.name}"`
  if (!(await deleteConfirmation(message))) return
  try {
    await productAttributeApi.remove(row.id)
    notify.success('Attribute deleted')
    reload()
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  }
}

onMounted(() => fetch())
</script>
