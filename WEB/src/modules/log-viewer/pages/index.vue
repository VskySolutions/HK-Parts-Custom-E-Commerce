<template>
  <q-page class="app-page">
    <AppListHeader
      title="Logs"
      :breadcrumbs="[{ label: 'Home', icon: 'o_home', to: '/dashboard' }, { label: 'Logs' }]"
    >
      <template #actions>
        <q-input
          v-model="search"
          dense
          outlined
          debounce="400"
          placeholder="Search messages"
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

    <AppFilterDrawer v-model="filtersOpen" title="Filter logs" @clear="clearFilters">
      <AppSelect v-model="level" label="Level" :options="logLevelOptions" @update:model-value="reload" />
      <div>
        <AppFieldLabel label="Correlation ID" />
        <q-input v-model="correlationId" dense outlined clearable debounce="500" hide-bottom-space placeholder="Match a correlation id" @update:model-value="reload" />
      </div>
      <AppDateField v-model="dateFrom" label="From date" @update:model-value="reload" />
      <AppDateField v-model="dateTo" label="To date" @update:model-value="reload" />
    </AppFilterDrawer>

    <AppDataTable
      page-key="admin-logs"
      row-key="id"
      title="Application logs"
      :rows="rows"
      :columns="columns"
      :loading="loading"
      :pagination="pagination"
      no-data-label="No log entries match these filters."
      @request="onRequest"
      @refresh="reload"
    >
      <template #body-cell-timeStamp="cell">
        <q-td :props="cell" class="text-no-wrap">{{ $datetime(cell.row.timeStamp) }}</q-td>
      </template>
      <template #body-cell-level="cell">
        <q-td :props="cell"><q-badge :color="logLevelColor(cell.row.level)" :label="cell.row.level || '—'" /></q-td>
      </template>
      <template #body-cell-message="cell">
        <q-td :props="cell" style="max-width: 420px">
          <div class="ellipsis log-message">
            {{ cell.row.message }}
            <q-tooltip v-if="cell.row.message" max-width="520px" anchor="bottom left" self="top left" class="log-tooltip">{{ cell.row.message }}</q-tooltip>
          </div>
        </q-td>
      </template>
      <template #body-cell-source="cell">
        <q-td :props="cell" class="text-grey-8">{{ cell.row.source || '—' }}</q-td>
      </template>
      <template #body-cell-route="cell">
        <q-td :props="cell" class="text-grey-8">{{ cell.row.route || '—' }}</q-td>
      </template>
      <template #body-cell-correlationId="cell">
        <q-td :props="cell">
          <span v-if="cell.row.correlationId" class="corr-id cursor-pointer text-primary" @click="copyId(cell.row.correlationId)">
            {{ shortId(cell.row.correlationId) }}
            <q-tooltip>Click to copy · {{ cell.row.correlationId }}</q-tooltip>
          </span>
          <span v-else class="text-grey-5">—</span>
        </q-td>
      </template>
    </AppDataTable>
  </q-page>
</template>

<script setup>
/*
 * Log Viewer (WO-70): a read-only, server-paginated view of the application log store. Quick
 * message search in the header; level / correlation-id / date-range filters in the advanced drawer.
 * Messages truncate with a hover tooltip; a correlation id copies to the clipboard on click.
 */
import { ref, computed, onMounted } from 'vue'
import { getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'
import { logApi, logLevelColor, logLevelOptions } from 'modules/log-viewer/api'

const notify = useNotify()

const columns = [
  { name: 'timeStamp', label: 'Time', field: 'timeStamp', align: 'left', sortable: true },
  { name: 'level', label: 'Level', field: 'level', align: 'left', sortable: true },
  { name: 'message', label: 'Message', field: 'message', align: 'left' },
  { name: 'source', label: 'Source', field: 'source', align: 'left' },
  { name: 'route', label: 'Route', field: 'route', align: 'left' },
  { name: 'correlationId', label: 'Correlation', field: 'correlationId', align: 'left' }
]

const rows = ref([])
const loading = ref(false)
const search = ref('')
const level = ref(null)
const correlationId = ref('')
const dateFrom = ref('')
const dateTo = ref('')
const filtersOpen = ref(false)
const pagination = ref({ page: 1, rowsPerPage: 20, rowsNumber: 0, sortBy: 'timeStamp', descending: true })

const activeFilterCount = computed(() =>
  (level.value ? 1 : 0) + (correlationId.value ? 1 : 0) + (dateFrom.value ? 1 : 0) + (dateTo.value ? 1 : 0)
)

function clearFilters () {
  level.value = null
  correlationId.value = ''
  dateFrom.value = ''
  dateTo.value = ''
  reload()
}

async function fetch (props) {
  const p = props?.pagination || pagination.value
  loading.value = true
  try {
    const result = await logApi.list({
      page: p.page,
      pageSize: p.rowsPerPage,
      dateFrom: dateFrom.value || undefined,
      dateTo: dateTo.value || undefined,
      level: level.value || undefined,
      correlationId: correlationId.value || undefined,
      search: search.value || undefined,
      sortBy: p.sortBy || undefined,
      sortDescending: !!p.descending
    })
    const items = Array.isArray(result) ? result : result?.items || []
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

function shortId (id) {
  const s = String(id)
  return s.length > 10 ? `${s.slice(0, 8)}…` : s
}

async function copyId (id) {
  try {
    await navigator.clipboard.writeText(String(id))
    notify.success('Correlation id copied')
  } catch (e) {
    notify.warning('Could not copy to clipboard')
  }
}

onMounted(() => fetch())
</script>

<style scoped lang="scss">
.log-message {
  max-width: 420px;
}
.corr-id {
  font-family: 'Roboto Mono', monospace;
  font-size: 12px;
}
</style>
