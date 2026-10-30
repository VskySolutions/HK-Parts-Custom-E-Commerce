<template>
  <q-page class="app-page">
    <AppListHeader
      title="Pages"
      :breadcrumbs="[{ label: 'Home', icon: 'o_home', to: '/dashboard' }, { label: 'CMS' }, { label: 'Pages' }]"
      :show-add="canWrite"
      add-label="New page"
      @add="onAdd"
    >
      <template #actions>
        <q-input
          v-model="search"
          dense
          outlined
          debounce="400"
          placeholder="Search pages"
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

    <AppFilterDrawer v-model="filtersOpen" title="Filter pages" @clear="clearFilters">
      <AppSelect v-model="statusFilter" label="Status" :options="statusFilterOptions" @update:model-value="reload" />
      <AppSelect v-model="groupFilter" label="Page group" :options="groupFilterOptions" @update:model-value="reload" />
    </AppFilterDrawer>

    <AppDataTable
      page-key="cms-pages"
      row-key="id"
      title="All pages"
      :rows="rows"
      :columns="columns"
      :loading="loading"
      :pagination="pagination"
      show-actions
      no-data-label="No pages yet."
      @request="onRequest"
      @refresh="reload"
    >
      <template #body-cell-title="cell">
        <q-td :props="cell">
          <a class="text-primary cursor-pointer text-weight-medium" @click="onManage(cell.row)">{{ cell.row.title || '(untitled)' }}</a>
          <q-badge v-if="cell.row.isSystemPage" color="blue-grey" outline class="q-ml-sm" label="System" />
        </q-td>
      </template>

      <template #body-cell-slug="cell">
        <q-td :props="cell"><span class="text-grey-8">/{{ cell.row.slug }}</span></q-td>
      </template>

      <template #body-cell-pageGroupId="cell">
        <q-td :props="cell">
          <span v-if="groupName(cell.row.pageGroupId)">{{ groupName(cell.row.pageGroupId) }}</span>
          <span v-else class="text-grey-6">—</span>
        </q-td>
      </template>

      <template #body-cell-status="cell">
        <q-td :props="cell"><q-badge :color="statusColor(cell.row.status)" :label="cell.row.status" /></q-td>
      </template>

      <template #body-cell-shownIn="cell">
        <q-td :props="cell">
          <span v-if="cell.row.showInTopBar || cell.row.showInFooter" class="row inline items-center q-gutter-xs">
            <q-badge v-if="cell.row.showInTopBar" color="indigo" outline label="Top bar">
              <q-tooltip>Shown as a quick-access link in the storefront navigation bar</q-tooltip>
            </q-badge>
            <q-badge v-if="cell.row.showInFooter" color="teal" outline label="Bottom bar">
              <q-tooltip>Shown in the storefront footer links</q-tooltip>
            </q-badge>
          </span>
          <span v-else class="text-grey-6">—</span>
        </q-td>
      </template>

      <template #actions="{ row }">
        <q-btn flat round dense icon="o_tune" @click="onManage(row)">
          <q-tooltip>Edit</q-tooltip>
        </q-btn>
        <q-btn v-if="canWrite && !row.isSystemPage" flat round dense icon="o_delete" color="negative" @click="onDelete(row)">
          <q-tooltip>Delete</q-tooltip>
        </q-btn>
        <q-btn v-else-if="row.isSystemPage" flat round dense icon="o_lock" color="grey-5">
          <q-tooltip>System pages are protected and can't be deleted</q-tooltip>
        </q-btn>
      </template>
    </AppDataTable>
  </q-page>
</template>

<script setup>
/*
 * CMS Pages list (WO-54): AppListHeader + AppFilterDrawer + AppDataTable with server-side
 * paging (page/pageSize/search/status/pageGroupId/sortBy/sortDescending). The page group name
 * is resolved client-side from the loaded groups (the page DTO only carries pageGroupId). Delete
 * is hidden for system pages, which are protected server-side. Create/edit open the full-page
 * page detail (`cms-page-new` / `cms-page-detail`).
 */
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { cmsPageApi, cmsPageGroupApi, statusFilterOptions, statusColor } from '../api'
import { getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'
import { usePermissions } from 'composables/usePermissions'
import { deleteConfirmation } from 'dialogs/delete_confirmation'

const router = useRouter()
const notify = useNotify()
const { has } = usePermissions()
const canWrite = computed(() => has('Cms.Write'))

const columns = [
  { name: 'title', label: 'Title', field: 'title', align: 'left', sortable: true },
  { name: 'slug', label: 'Slug', field: 'slug', align: 'left', sortable: true },
  { name: 'pageGroupId', label: 'Group', field: 'pageGroupId', align: 'left' },
  { name: 'status', label: 'Status', field: 'status', align: 'center', sortable: true },
  { name: 'shownIn', label: 'Shows in', field: 'showInTopBar', align: 'center' },
  { name: 'displayOrder', label: 'Order', field: 'displayOrder', align: 'right', sortable: true }
]

const rows = ref([])
const loading = ref(false)
const search = ref('')
const statusFilter = ref(null)
const groupFilter = ref(null)
const filtersOpen = ref(false)
const pagination = ref({ page: 1, rowsPerPage: 10, rowsNumber: 0 })

// Page groups (loaded once) — power the filter dropdown and resolve group names in the table.
const groups = ref([])
const groupFilterOptions = computed(() => [
  { label: 'All groups', value: null },
  ...groups.value.map((g) => ({ label: g.name, value: g.id }))
])
function groupName (id) {
  if (!id) return ''
  const match = groups.value.find((g) => g.id === id)
  return match ? match.name : ''
}

const activeFilterCount = computed(() =>
  (statusFilter.value !== null ? 1 : 0) + (groupFilter.value !== null ? 1 : 0)
)

function clearFilters () {
  statusFilter.value = null
  groupFilter.value = null
  reload()
}

async function loadGroups () {
  try {
    const result = await cmsPageGroupApi.list()
    groups.value = Array.isArray(result) ? result : result?.items || result?.data || []
  } catch {
    groups.value = []
  }
}

async function fetch (props) {
  const p = props?.pagination || pagination.value
  loading.value = true
  try {
    const result = await cmsPageApi.list({
      page: p.page,
      pageSize: p.rowsPerPage,
      search: search.value || undefined,
      status: statusFilter.value || undefined,
      pageGroupId: groupFilter.value || undefined,
      sortBy: p.sortBy || undefined,
      sortDescending: !!p.descending
    })
    const items = Array.isArray(result) ? result : result?.items || result?.data || []
    const total = Array.isArray(result) ? result.length : result?.totalCount ?? result?.total ?? items.length
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

function onAdd () { router.push({ name: 'cms-page-new' }) }
function onManage (row) { router.push({ name: 'cms-page-detail', params: { id: row.id } }) }

async function onDelete (row) {
  if (!(await deleteConfirmation(`the page "${row.title || 'untitled'}"`))) return
  try {
    await cmsPageApi.remove(row.id)
    notify.success('Page deleted')
    reload()
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  }
}

onMounted(() => {
  loadGroups()
  fetch()
})
</script>
