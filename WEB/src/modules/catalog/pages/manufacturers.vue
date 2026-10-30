<template>
  <q-page class="app-page">
    <AppListHeader
      title="Manufacturers"
      subtitle="Manage brands / manufacturers."
      :breadcrumbs="[{ label: 'Home', icon: 'o_home', to: '/dashboard' }, { label: 'Manufacturers' }]"
      :show-add="canWrite"
      add-label="New manufacturer"
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
      </template>
    </AppListHeader>

    <AppDataTable
      page-key="catalog-manufacturers"
      row-key="id"
      title="All manufacturers"
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

      <template #body-cell-isEnabled="cell">
        <q-td :props="cell">
          <q-badge
            :color="cell.row.isEnabled ? 'positive' : 'grey'"
            :label="cell.row.isEnabled ? 'Enabled' : 'Disabled'"
          />
        </q-td>
      </template>

      <template #actions="{ row }">
        <q-btn v-if="canWrite" flat round dense icon="o_tune" @click="onManage(row)">
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
 * Manufacturers list page. AppListHeader (search) + AppDataTable (server pagination). Create/edit open
 * the full-page manufacturer detail (`catalog-manufacturer-new` / `catalog-manufacturer-detail`).
 */
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { getApiErrorMessage } from 'services/api'
import { manufacturerApi } from 'modules/catalog/api'
import { usePermissions } from 'composables/usePermissions'
import { useNotify } from 'composables/useNotify'
import { deleteConfirmation } from 'dialogs/delete_confirmation'

const router = useRouter()
const notify = useNotify()
const { has } = usePermissions()
const canWrite = computed(() => has('Catalog.Write'))

const columns = [
  { name: 'name', label: 'Name', field: 'name', align: 'left', sortable: true },
  { name: 'slug', label: 'Slug', field: 'slug', align: 'left', sortable: true },
  { name: 'displayOrder', label: 'Order', field: 'displayOrder', align: 'right', sortable: true },
  { name: 'isEnabled', label: 'Status', field: 'isEnabled', align: 'center', sortable: true }
]

const rows = ref([])
const loading = ref(false)
const search = ref('')
const pagination = ref({ page: 1, rowsPerPage: 10, rowsNumber: 0 })

async function fetch (props) {
  const p = props?.pagination || pagination.value
  loading.value = true
  try {
    const result = await manufacturerApi.list({
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

function onAdd () { router.push({ name: 'catalog-manufacturer-new' }) }
function onManage (row) { router.push({ name: 'catalog-manufacturer-detail', params: { id: row.id } }) }

async function onDelete (row) {
  if (!(await deleteConfirmation(`the manufacturer "${row.name}"`))) return
  try {
    await manufacturerApi.remove(row.id)
    notify.success('Manufacturer deleted')
    reload()
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  }
}

onMounted(() => fetch())
</script>
