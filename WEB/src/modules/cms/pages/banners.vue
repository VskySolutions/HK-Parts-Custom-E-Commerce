<template>
  <q-page class="app-page">
    <AppListHeader
      title="Banners"
      :breadcrumbs="[{ label: 'Home', icon: 'o_home', to: '/dashboard' }, { label: 'Banners' }]"
      :show-add="canWrite"
      add-label="New banner"
      @add="onAdd"
    >
      <template #actions>
        <q-input
          v-model="search"
          dense
          outlined
          debounce="400"
          placeholder="Search banners"
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

    <AppFilterDrawer v-model="filtersOpen" title="Filter banners" @clear="clearFilters">
      <AppSelect v-model="locationFilter" label="Display location" :options="locationFilterOptions" @update:model-value="reload" />
      <AppSelect v-model="enabledFilter" label="Status" :options="enabledOptions" @update:model-value="reload" />
    </AppFilterDrawer>

    <AppDataTable
      page-key="cms-banners"
      row-key="id"
      title="All banners"
      :rows="rows"
      :columns="columns"
      :loading="loading"
      :pagination="pagination"
      show-actions
      @request="onRequest"
      @refresh="reload"
    >
      <template #body-cell-image="cell">
        <q-td :props="cell">
          <q-avatar rounded size="48px" class="cms-banner-thumb">
            <img v-if="cell.row.imageUrl" :src="$media(cell.row.imageUrl)" :alt="cell.row.title">
            <q-icon v-else name="o_image" size="22px" color="grey-5" />
          </q-avatar>
        </q-td>
      </template>

      <template #body-cell-title="cell">
        <q-td :props="cell">
          <a class="text-primary cursor-pointer text-weight-medium" @click="onManage(cell.row)">{{ cell.row.title || '(untitled)' }}</a>
          <div v-if="cell.row.subtitle" class="text-caption text-grey-6 ellipsis" style="max-width: 260px">{{ cell.row.subtitle }}</div>
        </q-td>
      </template>

      <template #body-cell-displayLocation="cell">
        <q-td :props="cell"><q-badge color="indigo" outline :label="bannerLocationLabel(cell.row.displayLocation)" /></q-td>
      </template>

      <template #body-cell-window="cell">
        <q-td :props="cell">{{ windowText(cell.row) }}</q-td>
      </template>

      <template #body-cell-isEnabled="cell">
        <q-td :props="cell"><q-badge :color="cell.row.isEnabled ? 'positive' : 'grey'" :label="cell.row.isEnabled ? 'Enabled' : 'Disabled'" /></q-td>
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
 * Banners list (WO-55): AppListHeader + AppFilterDrawer + AppDataTable with server-side
 * paging. Thumbnails resolve through $media; create/edit open the full-page banner detail.
 */
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { bannerApi, bannerLocationLabel, bannerLocationOptions } from 'modules/cms/api'
import { getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'
import { usePermissions } from 'composables/usePermissions'
import { deleteConfirmation } from 'dialogs/delete_confirmation'

const router = useRouter()
const notify = useNotify()
const { has } = usePermissions()
const canWrite = computed(() => has('Cms.Write'))

const columns = [
  { name: 'image', label: 'Image', field: 'imageUrl', align: 'left' },
  { name: 'title', label: 'Title', field: 'title', align: 'left', sortable: true },
  { name: 'displayLocation', label: 'Location', field: 'displayLocation', align: 'left', sortable: true },
  { name: 'window', label: 'Active window', field: 'startsOnUtc', align: 'left' },
  { name: 'isEnabled', label: 'Status', field: 'isEnabled', align: 'center', sortable: true },
  { name: 'displayOrder', label: 'Order', field: 'displayOrder', align: 'right', sortable: true }
]

const locationFilterOptions = [{ label: 'All locations', value: null }, ...bannerLocationOptions]
const enabledOptions = [
  { label: 'All', value: null },
  { label: 'Enabled', value: true },
  { label: 'Disabled', value: false }
]

const rows = ref([])
const loading = ref(false)
const search = ref('')
const locationFilter = ref(null)
const enabledFilter = ref(null)
const filtersOpen = ref(false)
const pagination = ref({ page: 1, rowsPerPage: 10, rowsNumber: 0 })

const activeFilterCount = computed(() =>
  (locationFilter.value !== null ? 1 : 0) + (enabledFilter.value !== null ? 1 : 0)
)

function clearFilters () {
  locationFilter.value = null
  enabledFilter.value = null
  reload()
}

function windowText (row) {
  const start = row.startsOnUtc ? formatDay(row.startsOnUtc) : null
  const end = row.endsOnUtc ? formatDay(row.endsOnUtc) : null
  if (start && end) return `${start} – ${end}`
  if (start) return `From ${start}`
  if (end) return `Until ${end}`
  return 'Always on'
}

// Local calendar-date formatter (mirrors the $date global for use inside script).
function formatDay (value) {
  if (!value) return ''
  const d = new Date(value)
  if (Number.isNaN(d.getTime()) || d.getFullYear() <= 1) return ''
  return d.toLocaleDateString(undefined, { day: '2-digit', month: 'short', year: 'numeric' })
}

async function fetch (props) {
  const p = props?.pagination || pagination.value
  loading.value = true
  try {
    const result = await bannerApi.list({
      page: p.page,
      pageSize: p.rowsPerPage,
      search: search.value || undefined,
      displayLocation: locationFilter.value || undefined,
      isEnabled: enabledFilter.value === null ? undefined : enabledFilter.value,
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

function onAdd () { router.push({ name: 'cms-banner-new' }) }
function onManage (row) { router.push({ name: 'cms-banner-detail', params: { id: row.id } }) }

async function onDelete (row) {
  if (!(await deleteConfirmation(`the banner "${row.title || 'untitled'}"`))) return
  try {
    await bannerApi.remove(row.id)
    notify.success('Banner deleted')
    reload()
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  }
}

onMounted(() => fetch())
</script>

<style scoped lang="scss">
.cms-banner-thumb {
  border: 1px solid rgba(0, 0, 0, 0.12);
  background: #fafafa;
  img { object-fit: cover; }
}
</style>
