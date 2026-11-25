<template>
  <div>
    <AppDataTable
      page-key="tax-logs"
      row-key="id"
      :rows="rows"
      :columns="columns"
      :loading="loading"
      :pagination="pagination"
      show-actions
      no-data-label="Nothing logged yet. Logging is switched on from the Avalara tab."
      @request="onRequest"
      @refresh="reload"
    >
      <template #top>
        <div class="row items-center full-width q-gutter-sm">
          <div class="app-section__title col ellipsis">Avalara log</div>
          <q-select v-model="action" :options="taxLogActionOptions" emit-value map-options dense outlined style="width: 170px" @update:model-value="reload" />
          <q-select v-model="status" :options="statusOptions" emit-value map-options dense outlined style="width: 130px" @update:model-value="reload" />
          <q-input v-model="search" dense outlined clearable debounce="400" placeholder="Order #" style="width: 150px" @update:model-value="reload" />
          <q-btn flat round dense icon="o_refresh" @click="reload"><q-tooltip>Refresh</q-tooltip></q-btn>
          <q-btn flat round dense icon="o_delete_sweep" color="negative" @click="clear"><q-tooltip>Delete entries older than 30 days</q-tooltip></q-btn>
        </div>
      </template>
      <template #body-cell-createdOnUtc="cell">
        <q-td :props="cell">{{ $datetime(cell.row.createdOnUtc) }}</q-td>
      </template>
      <template #body-cell-success="cell">
        <q-td :props="cell">
          <q-badge :color="cell.row.success ? 'positive' : 'negative'" :label="cell.row.success ? 'Success' : 'Failed'" />
        </q-td>
      </template>
      <template #body-cell-errorMessage="cell">
        <q-td :props="cell" class="ellipsis" style="max-width: 320px">{{ cell.row.errorMessage || '' }}</q-td>
      </template>
      <template #actions="{ row }">
        <q-btn flat round dense icon="o_visibility" @click="view(row)"><q-tooltip>View</q-tooltip></q-btn>
      </template>
    </AppDataTable>

    <q-dialog v-model="detailOpen">
      <q-card style="width: 900px; max-width: 96vw">
        <q-card-section class="row items-center q-gutter-sm">
          <q-badge :color="detail?.success ? 'positive' : 'negative'" :label="detail?.success ? 'Success' : 'Failed'" />
          <div class="text-subtitle1 text-weight-medium col ellipsis">{{ detail?.action }} <span v-if="detail?.orderNumber">· {{ detail.orderNumber }}</span></div>
          <q-btn flat round dense icon="o_close" v-close-popup />
        </q-card-section>
        <q-separator />
        <q-card-section v-if="detail" class="scroll" style="max-height: 75vh">
          <div class="text-caption text-grey-7 q-mb-sm">
            {{ $datetime(detail.createdOnUtc) }} · HTTP {{ detail.httpStatus ?? '—' }} · {{ detail.durationMs }} ms
            <div class="ellipsis">{{ detail.url }}</div>
          </div>
          <q-banner v-if="detail.errorMessage" dense rounded class="bg-red-1 text-negative q-mb-sm">{{ detail.errorMessage }}</q-banner>
          <div class="text-weight-medium q-mt-sm">Request</div>
          <pre class="tax-log-json">{{ pretty(detail.requestJson) }}</pre>
          <div class="text-weight-medium q-mt-sm">Response</div>
          <pre class="tax-log-json">{{ pretty(detail.responseJson) }}</pre>
        </q-card-section>
      </q-card>
    </q-dialog>
  </div>
</template>

<script setup>
/* PHP "Avalara Log": each Avalara call (Get Tax / Commit Order / Void Tax / Test Connection) with its request and response. */
import { ref } from 'vue'
import { getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'
import { confirmation } from 'dialogs/confirmation'
import { taxApi, taxLogActionOptions } from 'modules/tax/api'
import AppDataTable from 'components/common/AppDataTable.vue'

const notify = useNotify()
const rows = ref([])
const loading = ref(false)
const pagination = ref({ page: 1, rowsPerPage: 25, rowsNumber: 0 })
const action = ref('')
const status = ref('')
const search = ref('')
const statusOptions = [
  { label: 'Any status', value: '' },
  { label: 'Success', value: 'true' },
  { label: 'Failed', value: 'false' }
]
const columns = [
  { name: 'createdOnUtc', label: 'Date', field: 'createdOnUtc', align: 'left' },
  { name: 'action', label: 'Action', field: 'action', align: 'left' },
  { name: 'orderNumber', label: 'Order', field: (r) => r.orderNumber || '—', align: 'left' },
  { name: 'success', label: 'Status', field: 'success', align: 'left' },
  { name: 'durationMs', label: 'Time (ms)', field: 'durationMs', align: 'right' },
  { name: 'errorMessage', label: 'Error', field: 'errorMessage', align: 'left' }
]

const detailOpen = ref(false)
const detail = ref(null)

async function fetch ({ pagination: p }) {
  loading.value = true
  try {
    const res = await taxApi.logs({
      action: action.value || undefined,
      success: status.value === '' ? undefined : status.value,
      search: search.value || undefined,
      page: p.page,
      pageSize: p.rowsPerPage
    })
    rows.value = res?.items || []
    pagination.value = { ...p, rowsNumber: res?.totalCount || 0 }
  } catch (err) { notify.error(getApiErrorMessage(err)) } finally { loading.value = false }
}

function onRequest (props) { fetch(props) }
function reload () { fetch({ pagination: { ...pagination.value, page: 1 } }) }

async function view (row) {
  try {
    detail.value = await taxApi.log(row.id)
    detailOpen.value = true
  } catch (err) { notify.error(getApiErrorMessage(err)) }
}

async function clear () {
  if (!(await confirmation({ title: 'Clear log', message: 'Delete Avalara log entries older than 30 days?' }))) return
  try {
    const removed = await taxApi.clearLogs(30)
    notify.success(`${removed || 0} log entries deleted`)
    reload()
  } catch (err) { notify.error(getApiErrorMessage(err)) }
}

function pretty (json) {
  if (!json) return '—'
  try { return JSON.stringify(JSON.parse(json), null, 2) } catch { return json }
}

reload()
</script>

<style scoped>
.tax-log-json {
  background: var(--q-grey-2, #f5f5f5);
  border-radius: 6px;
  padding: 8px 12px;
  font-size: 12px;
  max-height: 320px;
  overflow: auto;
  white-space: pre-wrap;
  word-break: break-word;
}
</style>
