<template>
  <q-page class="app-page">
    <AppListHeader
      title="Categories"
      subtitle="Organise the catalog category tree."
      :breadcrumbs="[{ label: 'Home', icon: 'o_home', to: '/dashboard' }, { label: 'Categories' }]"
      :show-add="canWrite"
      add-label="New category"
      @add="onAdd"
    >
      <template #actions>
        <q-input
          v-model="search"
          dense
          outlined
          debounce="400"
          placeholder="Search by name"
          class="q-mr-sm"
          style="min-width: 240px"
          @update:model-value="reload"
        >
          <template #prepend><q-icon name="o_search" /></template>
          <template v-if="search" #append>
            <q-icon name="o_close" class="cursor-pointer" @click="search = ''; reload()" />
          </template>
        </q-input>
      </template>
    </AppListHeader>

    <AppDataTable
      page-key="catalog-categories"
      row-key="id"
      title="All categories"
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

      <template #body-cell-slug="cell">
        <q-td :props="cell">
          <span class="text-grey-7">/{{ cell.row.slug || '—' }}</span>
        </q-td>
      </template>

      <template #body-cell-parent="cell">
        <q-td :props="cell">{{ parentName(cell.row.parentId) }}</q-td>
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
        <q-btn
          v-if="canWrite"
          flat
          round
          dense
          :icon="row.isEnabled ? 'o_toggle_on' : 'o_toggle_off'"
          :color="row.isEnabled ? 'positive' : 'grey'"
          @click="toggleEnabled(row)"
        >
          <q-tooltip>{{ row.isEnabled ? 'Disable' : 'Enable' }}</q-tooltip>
        </q-btn>
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
 * Categories list page. AppListHeader toolbar (search) + AppDataTable (server pagination via
 * GET /categories, card title + refresh). Create/edit open the full-page category detail
 * (`catalog-category-new` / `catalog-category-detail`) — no drawer. The category tree
 * (GET /categories/tree) is fetched to resolve parent names for the Parent column.
 */
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { getApiErrorMessage } from 'services/api'
import { categoryApi } from 'modules/catalog/api'
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
  { name: 'parent', label: 'Parent', field: 'parentId', align: 'left', sortable: true },
  { name: 'displayOrder', label: 'Order', field: 'displayOrder', align: 'right', sortable: true },
  { name: 'isEnabled', label: 'Status', field: 'isEnabled', align: 'center', sortable: true }
]

const rows = ref([])
const loading = ref(false)
const search = ref('')
const pagination = ref({ page: 1, rowsPerPage: 10, rowsNumber: 0 })

// Parent lookup, derived from the category tree: an id -> name map for the Parent column.
const parentNames = ref({})

function indexNames (list, map = {}) {
  for (const node of list || []) {
    map[node.id] = node.name
    if (node.children && node.children.length) indexNames(node.children, map)
  }
  return map
}

function parentName (parentId) {
  if (!parentId) return '—'
  return parentNames.value[parentId] || '—'
}

async function loadTree () {
  try {
    const tree = await categoryApi.tree()
    parentNames.value = indexNames(Array.isArray(tree) ? tree : [])
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  }
}

async function fetch (props) {
  const p = props?.pagination || pagination.value
  loading.value = true
  try {
    const result = await categoryApi.list({
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

function onRequest (props) {
  fetch(props)
}

function reload () {
  fetch({ pagination: { ...pagination.value, page: 1 } })
}

function onAdd () {
  router.push({ name: 'catalog-category-new' })
}

function onManage (row) {
  router.push({ name: 'catalog-category-detail', params: { id: row.id } })
}

async function toggleEnabled (row) {
  try {
    await categoryApi.update(row.id, {
      name: row.name,
      parentId: row.parentId || null,
      slug: row.slug || null,
      description: row.description || null,
      metaTitle: row.metaTitle || null,
      metaDescription: row.metaDescription || null,
      metaKeywords: row.metaKeywords || null,
      canonicalUrl: row.canonicalUrl || null,
      displayOrder: row.displayOrder || 0,
      isEnabled: !row.isEnabled
    })
    notify.success(row.isEnabled ? 'Category disabled' : 'Category enabled')
    await fetch()
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  }
}

async function onDelete (row) {
  if (!(await deleteConfirmation(`the category "${row.name}"`))) return
  try {
    await categoryApi.remove(row.id)
    notify.success('Category deleted')
    await Promise.all([loadTree(), fetch()])
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  }
}

onMounted(() => {
  loadTree()
  fetch()
})
</script>
