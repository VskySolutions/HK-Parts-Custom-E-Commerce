<template>
  <q-page class="app-page">
    <AppListHeader
      title="Admin Alerts"
      subtitle="Operational alerts raised by the platform (delivery failures, tax fallback, storage, low stock…)."
      :breadcrumbs="[{ label: 'Home', icon: 'o_home', to: '/dashboard' }, { label: 'Admin Alerts' }]"
      :show-add="false"
    >
      <template #actions>
        <q-input
          v-model="search"
          dense
          outlined
          debounce="400"
          placeholder="Search title, type, message or source"
          style="min-width: 260px"
          @update:model-value="reload"
        >
          <template #prepend><q-icon name="o_search" /></template>
          <template v-if="search" #append><q-icon name="o_close" class="cursor-pointer" @click="search = ''; reload()" /></template>
        </q-input>
        <q-btn outline color="primary" no-caps icon="o_tune" label="Advanced" class="q-ml-sm" @click="filtersOpen = true">
          <q-badge v-if="activeFilterCount" color="red" floating>{{ activeFilterCount }}</q-badge>
        </q-btn>
        <q-btn flat round dense icon="o_refresh" class="q-ml-xs" @click="reload"><q-tooltip>Refresh</q-tooltip></q-btn>
      </template>
    </AppListHeader>

    <AppFilterDrawer v-model="filtersOpen" title="Filter alerts" @clear="clearFilters">
      <AppSelect v-model="severityFilter" label="Severity" :options="severityOptions" @update:model-value="reload" />
      <AppSelect v-model="resolvedFilter" label="Status" :options="statusOptions" @update:model-value="reload" />
    </AppFilterDrawer>

    <AppDataTable
      page-key="admin-alerts"
      row-key="id"
      title="Alerts"
      :rows="rows"
      :columns="columns"
      :loading="loading"
      :pagination="pagination"
      show-actions
      no-data-label="No alerts match the current filters."
      @request="onRequest"
      @refresh="reload"
    >
      <template #body-cell-severity="cell">
        <q-td :props="cell" class="text-center">
          <q-badge :color="severityColor(cell.row.severity)" :label="cell.row.severity" />
        </q-td>
      </template>

      <template #body-cell-title="cell">
        <q-td :props="cell">
          <a class="text-primary cursor-pointer text-weight-medium" @click="view(cell.row)">{{ cell.row.title }}</a>
        </q-td>
      </template>

      <template #body-cell-source="cell">
        <q-td :props="cell">{{ cell.row.source || '—' }}</q-td>
      </template>

      <template #body-cell-createdOnUtc="cell">
        <q-td :props="cell">{{ $datetime(cell.row.createdOnUtc) }}</q-td>
      </template>

      <template #body-cell-status="cell">
        <q-td :props="cell" class="text-center">
          <q-badge :color="cell.row.isResolved ? 'positive' : 'orange-7'" :label="cell.row.isResolved ? 'Resolved' : 'Open'" outline />
        </q-td>
      </template>

      <template #actions="{ row }">
        <q-btn flat round dense icon="o_visibility" @click="view(row)"><q-tooltip>View</q-tooltip></q-btn>
        <q-btn
          v-if="!row.isResolved && canWrite"
          flat round dense
          icon="o_check_circle"
          color="positive"
          :loading="resolvingId === row.id"
          @click="confirmResolve(row)"
        >
          <q-tooltip>Mark resolved</q-tooltip>
        </q-btn>
      </template>
    </AppDataTable>

    <!-- Detail dialog -->
    <q-dialog v-model="detailOpen">
      <q-card style="width: 640px; max-width: 95vw" v-if="selected">
        <q-card-section class="row items-center q-gutter-sm">
          <q-badge :color="severityColor(selected.severity)" :label="selected.severity" />
          <div class="text-subtitle1 text-weight-medium col ellipsis">{{ selected.title }}</div>
          <q-badge :color="selected.isResolved ? 'positive' : 'orange-7'" :label="selected.isResolved ? 'Resolved' : 'Open'" outline />
        </q-card-section>
        <q-separator />
        <q-card-section class="scroll" style="max-height: 70vh">
          <div class="row q-col-gutter-md q-mb-md">
            <div class="col-12 col-sm-6"><AppFieldLabel label="Type" /><div class="text-body2">{{ selected.alertType || '—' }}</div></div>
            <div class="col-12 col-sm-6"><AppFieldLabel label="Source" /><div class="text-body2">{{ selected.source || '—' }}</div></div>
            <div class="col-12 col-sm-6"><AppFieldLabel label="Raised" /><div class="text-body2">{{ $datetime(selected.createdOnUtc) }}</div></div>
            <div class="col-12 col-sm-6"><AppFieldLabel label="Resolved" /><div class="text-body2">{{ selected.resolvedOnUtc ? $datetime(selected.resolvedOnUtc) : '—' }}</div></div>
          </div>
          <AppFieldLabel label="Message" />
          <pre class="alert-message">{{ selected.message || '—' }}</pre>
        </q-card-section>
        <q-separator />
        <q-card-actions align="right">
          <q-btn flat no-caps label="Close" v-close-popup />
          <q-btn
            v-if="!selected.isResolved && canWrite"
            color="primary"
            unelevated
            no-caps
            icon="o_check_circle"
            label="Mark resolved"
            :loading="resolvingId === selected.id"
            @click="confirmResolve(selected)"
          />
        </q-card-actions>
      </q-card>
    </q-dialog>
  </q-page>
</template>

<script setup>
/* Admin Alerts: browse operational alerts raised across the platform, filter by severity/status/search,
 * and mark them resolved. Backed by AdminAlertsController (/api/admin/alerts). */
import { ref, computed, onMounted } from 'vue'
import { useQuasar } from 'quasar'
import { getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'
import { usePermissions, Permissions } from 'composables/usePermissions'
import { adminAlertApi } from 'modules/admin-alerts/api'
import AppListHeader from 'components/common/AppListHeader.vue'
import AppDataTable from 'components/common/AppDataTable.vue'
import AppFilterDrawer from 'components/common/AppFilterDrawer.vue'
import AppSelect from 'components/common/AppSelect.vue'
import AppFieldLabel from 'components/common/AppFieldLabel.vue'

const $q = useQuasar()
const notify = useNotify()
const { has } = usePermissions()
const canWrite = computed(() => has(Permissions.AlertsWrite))

const columns = [
  { name: 'severity', label: 'Severity', field: 'severity', align: 'center', sortable: true },
  { name: 'alertType', label: 'Type', field: 'alertType', align: 'left', sortable: true },
  { name: 'title', label: 'Title', field: 'title', align: 'left', sortable: true },
  { name: 'source', label: 'Source', field: 'source', align: 'left', sortable: true },
  { name: 'createdOnUtc', label: 'Raised', field: 'createdOnUtc', align: 'left', sortable: true },
  { name: 'status', label: 'Status', field: 'isResolved', align: 'center', sortable: true }
]

const severityOptions = [
  { label: 'All', value: null },
  { label: 'Error', value: 'Error' },
  { label: 'Warning', value: 'Warning' },
  { label: 'Info', value: 'Info' }
]
const statusOptions = [
  { label: 'All', value: null },
  { label: 'Open', value: false },
  { label: 'Resolved', value: true }
]

const rows = ref([])
const loading = ref(false)
const search = ref('')
const severityFilter = ref(null)
const resolvedFilter = ref(null)
const filtersOpen = ref(false)
const pagination = ref({ page: 1, rowsPerPage: 20, rowsNumber: 0 })
const resolvingId = ref(null)

const detailOpen = ref(false)
const selected = ref(null)

const activeFilterCount = computed(() =>
  (severityFilter.value !== null ? 1 : 0) + (resolvedFilter.value !== null ? 1 : 0)
)

function severityColor (s) {
  return { Error: 'negative', Warning: 'orange', Info: 'blue-grey' }[s] || 'grey'
}

function clearFilters () {
  severityFilter.value = null
  resolvedFilter.value = null
  reload()
}

async function fetch (props) {
  const p = props?.pagination || pagination.value
  loading.value = true
  try {
    const r = await adminAlertApi.list({
      page: p.page,
      pageSize: p.rowsPerPage,
      search: search.value || undefined,
      severity: severityFilter.value || undefined,
      resolved: resolvedFilter.value === null ? undefined : resolvedFilter.value,
      sortBy: p.sortBy || undefined,
      sortDescending: !!p.descending
    })
    rows.value = Array.isArray(r?.items) ? r.items : []
    pagination.value = { ...p, rowsNumber: r?.totalCount ?? rows.value.length }
  } catch (e) {
    rows.value = []
    notify.error(getApiErrorMessage(e))
  } finally {
    loading.value = false
  }
}
function onRequest (props) { fetch(props) }
function reload () { fetch({ pagination: { ...pagination.value, page: 1 } }) }

function view (row) {
  selected.value = row
  detailOpen.value = true
}

function confirmResolve (row) {
  if (!row) return
  $q.dialog({
    title: 'Resolve alert',
    message: `Mark "${row.title}" as resolved?`,
    cancel: true,
    ok: { label: 'Resolve', color: 'primary', unelevated: true, noCaps: true }
  }).onOk(() => resolve(row))
}

async function resolve (row) {
  resolvingId.value = row.id
  try {
    await adminAlertApi.resolve(row.id)
    notify.success('Alert marked resolved')
    detailOpen.value = false
    await fetch()
  } catch (e) {
    notify.error(getApiErrorMessage(e))
  } finally {
    resolvingId.value = null
  }
}

onMounted(() => fetch())
</script>

<style scoped lang="scss">
.alert-message {
  white-space: pre-wrap;
  word-break: break-word;
  font-family: inherit;
  font-size: 13px;
  background: rgba(0, 0, 0, 0.03);
  border-radius: 6px;
  padding: 12px;
  margin: 0;
}
body.body--dark .alert-message {
  background: rgba(255, 255, 255, 0.06);
}
</style>
