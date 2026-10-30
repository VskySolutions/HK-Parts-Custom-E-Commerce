<template>
  <q-page class="app-page">
    <AppListHeader
      title="Product Collections"
      subtitle="Curated, ordered product lists you can surface in home sections, category pages and more."
      :breadcrumbs="[{ label: 'Home', icon: 'o_home', to: '/dashboard' }, { label: 'Product Collections' }]"
      show-add
      add-label="New collection"
      @add="onAdd"
    >
      <template #actions>
        <q-input
          v-model="search"
          dense
          outlined
          debounce="400"
          placeholder="Search"
          style="min-width: 240px"
          @update:model-value="reload"
        >
          <template #prepend><q-icon name="o_search" /></template>
          <template v-if="search" #append><q-icon name="o_close" class="cursor-pointer" @click="search = ''; reload()" /></template>
        </q-input>
        <q-btn outline color="primary" no-caps icon="o_tune" label="Advanced" class="q-ml-sm" @click="filtersOpen = true">
          <q-badge v-if="activeFilterCount" color="red" floating>{{ activeFilterCount }}</q-badge>
        </q-btn>
      </template>
    </AppListHeader>

    <AppFilterDrawer v-model="filtersOpen" title="Filter collections" @clear="clearFilters">
      <AppSelect v-model="enabledFilter" label="Status" :options="enabledOptions" @update:model-value="reload" />
    </AppFilterDrawer>

    <AppDataTable
      page-key="cms-collections"
      row-key="id"
      title="All collections"
      :rows="rows"
      :columns="columns"
      :loading="loading"
      :pagination="pagination"
      show-actions
      no-data-label="No collections yet."
      @request="onRequest"
      @refresh="reload"
    >
      <template #body-cell-name="cell">
        <q-td :props="cell">
          <a class="text-primary cursor-pointer text-weight-medium" @click="onManage(cell.row)">{{ cell.row.name }}</a>
        </q-td>
      </template>

      <template #body-cell-productCount="cell">
        <q-td :props="cell">
          <q-badge color="blue-1" text-color="primary" :label="`${cell.row.productCount ?? 0} products`" />
        </q-td>
      </template>

      <template #body-cell-updatedOnUtc="cell">
        <q-td :props="cell">{{ $datetime(cell.row.updatedOnUtc) }}</q-td>
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
 * Product Collections list page (WO-97): AppListHeader + AppDataTable with server-side pagination,
 * a quick search and an Advanced status filter (isEnabled). Rows link to the full-page collection
 * editor (create/manage).
 */
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { collectionApi } from '../api'
import { getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'
import { deleteConfirmation } from 'dialogs/delete_confirmation'

const router = useRouter()
const notify = useNotify()

const columns = [
  { name: 'name', label: 'Name', field: 'name', align: 'left', sortable: true },
  { name: 'productCount', label: 'Products', field: 'productCount', align: 'left', sortable: true },
  { name: 'updatedOnUtc', label: 'Last updated', field: 'updatedOnUtc', align: 'left', sortable: true }
]

const enabledOptions = [
  { label: 'All', value: null },
  { label: 'Enabled', value: true },
  { label: 'Disabled', value: false }
]

const rows = ref([])
const loading = ref(false)
const search = ref('')
const enabledFilter = ref(null)
const filtersOpen = ref(false)
const pagination = ref({ page: 1, rowsPerPage: 10, rowsNumber: 0 })

const activeFilterCount = computed(() => (enabledFilter.value !== null ? 1 : 0))

function clearFilters () {
  enabledFilter.value = null
  reload()
}

async function fetch (props) {
  const p = props?.pagination || pagination.value
  loading.value = true
  try {
    const result = await collectionApi.list({
      page: p.page,
      pageSize: p.rowsPerPage,
      search: search.value || undefined,
      isEnabled: enabledFilter.value === null ? undefined : enabledFilter.value,
      sortBy: p.sortBy || undefined,
      sortDescending: !!p.descending
    })
    const items = Array.isArray(result) ? result : result?.items || result?.data || []
    const total = Array.isArray(result)
      ? result.length
      : result?.totalCount ?? result?.total ?? items.length
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

function onRequest (props) {
  fetch(props)
}

function reload () {
  fetch({ pagination: { ...pagination.value, page: 1 } })
}

function onAdd () { router.push({ name: 'cms-collection-new' }) }
function onManage (row) { router.push({ name: 'cms-collection-detail', params: { id: row.id } }) }

async function onDelete (row) {
  if (!(await deleteConfirmation(`the collection “${row.name}”`))) return
  try {
    await collectionApi.remove(row.id)
    notify.success('Collection deleted')
    reload()
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  }
}

onMounted(() => fetch())
</script>
