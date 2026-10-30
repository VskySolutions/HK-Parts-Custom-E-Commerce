<template>
  <q-page class="app-page">
    <AppListHeader
      title="Blog"
      :breadcrumbs="[{ label: 'Home', icon: 'o_home', to: '/dashboard' }, { label: 'CMS' }, { label: 'Blog' }]"
      :show-add="canWrite"
      add-label="New post"
      @add="onAdd"
    >
      <template #actions>
        <q-input
          v-model="search"
          dense
          outlined
          debounce="400"
          placeholder="Search posts"
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

    <AppFilterDrawer v-model="filtersOpen" title="Filter posts" @clear="clearFilters">
      <AppSelect v-model="statusFilter" label="Status" :options="statusFilterOptions" @update:model-value="reload" />
    </AppFilterDrawer>

    <AppDataTable
      page-key="cms-blog"
      row-key="id"
      title="All posts"
      :rows="rows"
      :columns="columns"
      :loading="loading"
      :pagination="pagination"
      show-actions
      no-data-label="No blog posts yet."
      @request="onRequest"
      @refresh="reload"
    >
      <template #body-cell-title="cell">
        <q-td :props="cell">
          <div class="row items-center no-wrap">
            <q-avatar rounded size="40px" class="cms-blog-thumb q-mr-sm">
              <img v-if="cell.row.featuredImageUrl" :src="$media(cell.row.featuredImageUrl)" :alt="cell.row.title">
              <q-icon v-else name="o_article" size="20px" color="grey-5" />
            </q-avatar>
            <a class="text-primary cursor-pointer text-weight-medium" @click="onManage(cell.row)">{{ cell.row.title || '(untitled)' }}</a>
          </div>
        </q-td>
      </template>

      <template #body-cell-author="cell">
        <q-td :props="cell">
          <span v-if="cell.row.author">{{ cell.row.author }}</span>
          <span v-else class="text-grey-6">—</span>
        </q-td>
      </template>

      <template #body-cell-status="cell">
        <q-td :props="cell"><q-badge :color="statusColor(cell.row.status)" :label="cell.row.status" /></q-td>
      </template>

      <template #body-cell-publishedOnUtc="cell">
        <q-td :props="cell">
          <span v-if="cell.row.publishedOnUtc">{{ $datetime(cell.row.publishedOnUtc) }}</span>
          <span v-else class="text-grey-6">—</span>
        </q-td>
      </template>

      <template #actions="{ row }">
        <q-btn flat round dense icon="o_tune" @click="onManage(row)">
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
 * Blog Posts list (WO-54): AppListHeader + AppFilterDrawer + AppDataTable with server-side paging
 * (page/pageSize/search/status/sortBy/sortDescending). Featured-image thumbnails resolve through $media;
 * published dates through the $datetime global. Create/edit open the full-page blog detail
 * (`cms-blog-new` / `cms-blog-detail`).
 */
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { blogPostApi, statusFilterOptions, statusColor } from '../api'
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
  { name: 'author', label: 'Author', field: 'author', align: 'left', sortable: true },
  { name: 'status', label: 'Status', field: 'status', align: 'center', sortable: true },
  { name: 'publishedOnUtc', label: 'Published', field: 'publishedOnUtc', align: 'left', sortable: true }
]

const rows = ref([])
const loading = ref(false)
const search = ref('')
const statusFilter = ref(null)
const filtersOpen = ref(false)
const pagination = ref({ page: 1, rowsPerPage: 10, rowsNumber: 0 })

const activeFilterCount = computed(() => (statusFilter.value !== null ? 1 : 0))

function clearFilters () {
  statusFilter.value = null
  reload()
}

async function fetch (props) {
  const p = props?.pagination || pagination.value
  loading.value = true
  try {
    const result = await blogPostApi.list({
      page: p.page,
      pageSize: p.rowsPerPage,
      search: search.value || undefined,
      status: statusFilter.value || undefined,
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

function onAdd () { router.push({ name: 'cms-blog-new' }) }
function onManage (row) { router.push({ name: 'cms-blog-detail', params: { id: row.id } }) }

async function onDelete (row) {
  if (!(await deleteConfirmation(`the blog post "${row.title || 'untitled'}"`))) return
  try {
    await blogPostApi.remove(row.id)
    notify.success('Blog post deleted')
    reload()
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  }
}

onMounted(() => fetch())
</script>

<style scoped lang="scss">
.cms-blog-thumb {
  border: 1px solid rgba(0, 0, 0, 0.12);
  background: #fafafa;
  img { object-fit: cover; }
}
</style>
