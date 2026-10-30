<template>
  <q-page class="app-page">
    <AppListHeader
      title="Audit Trail"
      :breadcrumbs="[{ label: 'Home', icon: 'o_home', to: '/dashboard' }, { label: 'Audit Trail' }]"
    >
      <template #actions>
        <q-input
          v-model="search"
          dense
          outlined
          debounce="400"
          placeholder="Search actor, entity, id"
          style="min-width: 260px"
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

    <AppFilterDrawer v-model="filtersOpen" title="Filter audit trail" @clear="clearFilters">
      <div>
        <AppFieldLabel label="Action" />
        <q-input v-model="action" dense outlined clearable debounce="500" hide-bottom-space placeholder="e.g. Create, Update, Delete" @update:model-value="reload" />
      </div>
      <div>
        <AppFieldLabel label="Entity type" />
        <q-input v-model="entityType" dense outlined clearable debounce="500" hide-bottom-space placeholder="e.g. Product, Order" @update:model-value="reload" />
      </div>
      <AppDateField v-model="dateFrom" label="From date" @update:model-value="reload" />
      <AppDateField v-model="dateTo" label="To date" @update:model-value="reload" />
    </AppFilterDrawer>

    <AppDataTable
      page-key="admin-audit-trail"
      row-key="id"
      title="Activity"
      :rows="rows"
      :columns="columns"
      :loading="loading"
      :pagination="pagination"
      no-data-label="No audit entries match these filters."
      @request="onRequest"
      @refresh="reload"
    >
      <template #body-cell-timestampUtc="cell">
        <q-td :props="cell" class="text-no-wrap">{{ $datetime(cell.row.timestampUtc) }}</q-td>
      </template>
      <template #body-cell-actorName="cell">
        <q-td :props="cell"><span class="text-weight-medium">{{ cell.row.actorName || '—' }}</span></q-td>
      </template>
      <template #body-cell-action="cell">
        <q-td :props="cell"><q-badge :color="auditActionColor(cell.row.action)" :label="cell.row.action || '—'" /></q-td>
      </template>
      <template #body-cell-entityType="cell">
        <q-td :props="cell">{{ cell.row.entityType || '—' }}</q-td>
      </template>
      <template #body-cell-entityId="cell">
        <q-td :props="cell">
          <span v-if="cell.row.entityId" class="entity-id">
            {{ shortId(cell.row.entityId) }}
            <q-tooltip>{{ cell.row.entityId }}</q-tooltip>
          </span>
          <span v-else class="text-grey-5">—</span>
        </q-td>
      </template>
      <template #body-cell-ipAddress="cell">
        <q-td :props="cell" class="text-grey-8">{{ cell.row.ipAddress || '—' }}</q-td>
      </template>
    </AppDataTable>
  </q-page>
</template>

<script setup>
/*
 * Audit Trail viewer (WO-61): a read-only, server-paginated log of who did what to which entity.
 * Quick search in the header; action / entity-type / date-range filters in the advanced drawer.
 * Entity ids are shortened with a full-value hover tooltip.
 */
import { ref, computed, onMounted } from 'vue'
import { getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'
import { auditTrailApi, auditActionColor } from 'modules/audit-trail/api'

const notify = useNotify()

const columns = [
  { name: 'timestampUtc', label: 'Time', field: 'timestampUtc', align: 'left', sortable: true },
  { name: 'actorName', label: 'Actor', field: 'actorName', align: 'left', sortable: true },
  { name: 'action', label: 'Action', field: 'action', align: 'left', sortable: true },
  { name: 'entityType', label: 'Entity', field: 'entityType', align: 'left', sortable: true },
  { name: 'entityId', label: 'Entity id', field: 'entityId', align: 'left' },
  { name: 'ipAddress', label: 'IP address', field: 'ipAddress', align: 'left' }
]

const rows = ref([])
const loading = ref(false)
const search = ref('')
const action = ref('')
const entityType = ref('')
const dateFrom = ref('')
const dateTo = ref('')
const filtersOpen = ref(false)
const pagination = ref({ page: 1, rowsPerPage: 20, rowsNumber: 0, sortBy: 'timestampUtc', descending: true })

const activeFilterCount = computed(() =>
  (action.value ? 1 : 0) + (entityType.value ? 1 : 0) + (dateFrom.value ? 1 : 0) + (dateTo.value ? 1 : 0)
)

function clearFilters () {
  action.value = ''
  entityType.value = ''
  dateFrom.value = ''
  dateTo.value = ''
  reload()
}

async function fetch (props) {
  const p = props?.pagination || pagination.value
  loading.value = true
  try {
    const result = await auditTrailApi.list({
      page: p.page,
      pageSize: p.rowsPerPage,
      dateFrom: dateFrom.value || undefined,
      dateTo: dateTo.value || undefined,
      action: action.value || undefined,
      entityType: entityType.value || undefined,
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
  return s.length > 12 ? `${s.slice(0, 8)}…${s.slice(-4)}` : s
}

onMounted(() => fetch())
</script>

<style scoped lang="scss">
.entity-id {
  font-family: 'Roboto Mono', monospace;
  font-size: 12px;
}
</style>
