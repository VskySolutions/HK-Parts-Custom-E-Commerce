<template>
  <q-page class="app-page">
    <AppListHeader
      title="Newsletter subscribers"
      :breadcrumbs="[{ label: 'Home', icon: 'o_home', to: '/dashboard' }, { label: 'Newsletter' }]"
      :show-add="false"
    >
      <template #actions>
        <q-input
          v-model="search"
          dense
          outlined
          debounce="400"
          placeholder="Search email"
          style="min-width: 240px"
          @update:model-value="reload"
        >
          <template #prepend><q-icon name="o_search" /></template>
          <template v-if="search" #append><q-icon name="o_close" class="cursor-pointer" @click="search = ''; reload()" /></template>
        </q-input>
        <q-btn outline color="primary" no-caps icon="o_tune" label="Advanced" class="q-ml-sm" @click="filtersOpen = true">
          <q-badge v-if="activeFilterCount" color="red" floating>{{ activeFilterCount }}</q-badge>
        </q-btn>
        <q-btn color="primary" unelevated no-caps icon="o_download" label="Export CSV" class="q-ml-sm" :loading="exporting" @click="onExport" />
      </template>
    </AppListHeader>

    <AppFilterDrawer v-model="filtersOpen" title="Filter subscribers" @clear="clearFilters">
      <AppSelect v-model="statusFilter" label="Status" :options="statusFilterOptions" @update:model-value="reload" />
    </AppFilterDrawer>

    <AppDataTable
      page-key="cms-newsletter"
      row-key="id"
      title="All subscribers"
      :rows="rows"
      :columns="columns"
      :loading="loading"
      :pagination="pagination"
      @request="onRequest"
      @refresh="reload"
    >
      <template #body-cell-status="cell">
        <q-td :props="cell">
          <q-badge :color="newsletterStatusColor(cell.row.status)" :label="cell.row.status" />
          <q-badge v-if="cell.row.isSuppressed" color="negative" outline label="Suppressed" class="q-ml-xs" />
        </q-td>
      </template>

      <template #body-cell-source="cell">
        <q-td :props="cell"><span :class="{ 'text-grey-6': !cell.row.source }">{{ cell.row.source || '—' }}</span></q-td>
      </template>

      <template #body-cell-createdOnUtc="cell">
        <q-td :props="cell">{{ $datetime(cell.row.createdOnUtc) }}</q-td>
      </template>

      <template #body-cell-confirmedOnUtc="cell">
        <q-td :props="cell"><span :class="{ 'text-grey-6': !cell.row.confirmedOnUtc }">{{ cell.row.confirmedOnUtc ? $datetime(cell.row.confirmedOnUtc) : '—' }}</span></q-td>
      </template>
    </AppDataTable>
  </q-page>
</template>

<script setup>
/*
 * Newsletter subscribers (WO-56): a read-only list (status/source/dates + a suppressed chip) with
 * search + status filter and an "Export CSV" header action that downloads the server-generated blob.
 */
import { ref, computed, onMounted } from 'vue'
import { newsletterApi, newsletterStatusOptions, newsletterStatusColor } from 'modules/cms/api'
import { getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'

const notify = useNotify()

const columns = [
  { name: 'email', label: 'Email', field: 'email', align: 'left', sortable: true },
  { name: 'status', label: 'Status', field: 'status', align: 'left', sortable: true },
  { name: 'source', label: 'Source', field: 'source', align: 'left' },
  { name: 'createdOnUtc', label: 'Subscribed on', field: 'createdOnUtc', align: 'left', sortable: true },
  { name: 'confirmedOnUtc', label: 'Confirmed on', field: 'confirmedOnUtc', align: 'left', sortable: true }
]

const statusFilterOptions = [{ label: 'All', value: null }, ...newsletterStatusOptions]

const rows = ref([])
const loading = ref(false)
const exporting = ref(false)
const search = ref('')
const statusFilter = ref(null)
const filtersOpen = ref(false)
const pagination = ref({ page: 1, rowsPerPage: 20, rowsNumber: 0 })

const activeFilterCount = computed(() => (statusFilter.value !== null ? 1 : 0))

function clearFilters () {
  statusFilter.value = null
  reload()
}

function queryParams () {
  return {
    search: search.value || undefined,
    status: statusFilter.value || undefined
  }
}

async function fetch (props) {
  const p = props?.pagination || pagination.value
  loading.value = true
  try {
    const result = await newsletterApi.listSubscribers({
      page: p.page,
      pageSize: p.rowsPerPage,
      ...queryParams(),
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

// Trigger a browser download for a Blob under the given filename.
function saveBlob (blob, filename) {
  const url = window.URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = filename
  document.body.appendChild(a)
  a.click()
  a.remove()
  window.URL.revokeObjectURL(url)
}

async function onExport () {
  exporting.value = true
  try {
    const res = await newsletterApi.exportSubscribersCsv(queryParams())
    saveBlob(res.data, 'newsletter-subscribers.csv')
    notify.success('Export ready')
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    exporting.value = false
  }
}

onMounted(() => fetch())
</script>
