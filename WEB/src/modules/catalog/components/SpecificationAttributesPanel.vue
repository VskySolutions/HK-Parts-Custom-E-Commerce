<template>
  <div>
    <AppFilterDrawer v-model="filtersOpen" title="Filter attributes" @clear="clearFilters">
      <AppSelect v-model="filterableFilter" label="Filterable" :options="filterableOptions" @update:model-value="reload" />
    </AppFilterDrawer>

    <AppDataTable
      page-key="catalog-specification-attributes"
      row-key="id"
      title="Specification attributes"
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
      <template #body-cell-isFilterable="cell">
        <q-td :props="cell">
          <q-badge :color="cell.row.isFilterable ? 'positive' : 'grey'" :label="cell.row.isFilterable ? 'Filterable' : 'Not filterable'" />
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
 * Specification Attributes panel (WO-15): the global specification-attribute library
 * list + CRUD. Filterable attributes drive the storefront faceted navigation.
 */
import { ref, computed, onMounted, watch } from 'vue'
import { useRouter } from 'vue-router'
import { getApiErrorMessage } from 'services/api'
import { specificationAttributeApi } from 'modules/catalog/api'
import { usePermissions } from 'composables/usePermissions'
import { useNotify } from 'composables/useNotify'
import { deleteConfirmation } from 'dialogs/delete_confirmation'
import AppDataTable from 'components/common/AppDataTable.vue'

// Toolbar (search/Advanced/Add) lives in the parent tab page; this panel is driven via the
// `search` prop + exposed onAdd/openFilters and the `filter-count` event.
const props = defineProps({ search: { type: String, default: '' } })
const emit = defineEmits(['filter-count'])

const router = useRouter()
const notify = useNotify()
const { has } = usePermissions()
const canWrite = computed(() => has('Catalog.Write'))

const columns = [
  { name: 'name', label: 'Attribute Name', field: 'name', align: 'left', sortable: true },
  { name: 'isFilterable', label: 'Filterable', field: 'isFilterable', align: 'center', sortable: true },
  { name: 'optionsCount', label: 'Values', field: (r) => (r.options || []).length, align: 'center' }
]

const filterableOptions = [
  { label: 'All', value: null },
  { label: 'Filterable', value: true },
  { label: 'Not filterable', value: false }
]

const rows = ref([])
const loading = ref(false)
const filterableFilter = ref(null)
const filtersOpen = ref(false)
const pagination = ref({ page: 1, rowsPerPage: 10, rowsNumber: 0 })

const activeFilterCount = computed(() => (filterableFilter.value !== null ? 1 : 0))

watch(activeFilterCount, (v) => emit('filter-count', v), { immediate: true })
watch(() => props.search, () => reload())

function openFilters () { filtersOpen.value = true }
defineExpose({ onAdd, openFilters })

function clearFilters () {
  filterableFilter.value = null
  reload()
}

async function fetch (req) {
  const p = req?.pagination || pagination.value
  loading.value = true
  try {
    const result = await specificationAttributeApi.list({
      page: p.page,
      pageSize: p.rowsPerPage,
      search: props.search || undefined,
      isFilterable: filterableFilter.value === null ? undefined : filterableFilter.value,
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

function onAdd () { router.push({ name: 'spec-attribute-new' }) }
function onManage (row) { router.push({ name: 'spec-attribute-detail', params: { id: row.id } }) }

async function onDelete (row) {
  if (!(await deleteConfirmation(`the specification attribute "${row.name}"`))) return
  try {
    await specificationAttributeApi.remove(row.id)
    notify.success('Attribute deleted')
    reload()
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  }
}

onMounted(() => fetch())
</script>
