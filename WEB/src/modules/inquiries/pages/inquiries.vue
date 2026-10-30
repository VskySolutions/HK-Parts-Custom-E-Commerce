<template>
  <q-page class="app-page">
    <AppListHeader
      title="Inquiries"
      :breadcrumbs="[{ label: 'Home', icon: 'o_home', to: '/dashboard' }, { label: 'Inquiries' }]"
    >
      <template #actions>
        <q-input
          v-model="search"
          dense
          outlined
          debounce="400"
          placeholder="Search reference, name, email or company"
          class="q-mr-sm"
          style="min-width: 300px"
          @update:model-value="reload"
        >
          <template #prepend><q-icon name="o_search" /></template>
          <template v-if="search" #append>
            <q-icon name="o_close" class="cursor-pointer" @click="search = ''; reload()" />
          </template>
        </q-input>
        <q-btn outline color="primary" no-caps icon="o_tune" label="Advanced" @click="filtersOpen = true">
          <q-badge v-if="activeFilterCount" color="red" floating>{{ activeFilterCount }}</q-badge>
        </q-btn>
      </template>
    </AppListHeader>

    <AppFilterDrawer v-model="filtersOpen" title="Filter inquiries" @clear="clearFilters">
      <AppSelect
        v-model="statusFilter"
        label="Status"
        clearable
        placeholder="Any status"
        :options="inquiryStatusOptions"
        @update:model-value="reload"
      />
      <AppSelect
        v-model="storeFilter"
        label="Assigned store"
        clearable
        placeholder="Any store"
        :options="storeOptions"
        @update:model-value="reload"
      />
      <AppSelect
        v-model="quotedFilter"
        label="Quote sent"
        clearable
        placeholder="Any"
        :options="quotedOptions"
        @update:model-value="reload"
      />
      <AppDateField v-model="fromFilter" label="Submitted from" clearable @update:model-value="reload" />
      <AppDateField v-model="toFilter" label="Submitted to" clearable @update:model-value="reload" />
    </AppFilterDrawer>

    <AppDataTable
      page-key="admin-inquiries"
      row-key="id"
      title="All inquiries"
      :rows="rows"
      :columns="columns"
      :loading="loading"
      :pagination="pagination"
      show-actions
      @request="onRequest"
      @refresh="reload"
    >
      <template #body-cell-referenceNumber="cell">
        <q-td :props="cell">
          <a class="text-primary cursor-pointer text-weight-medium" @click="open(cell.row)">
            {{ cell.row.referenceNumber }}
          </a>
        </q-td>
      </template>
      <template #body-cell-contactName="cell">
        <q-td :props="cell">
          <div class="text-weight-medium">{{ cell.row.contactName || '—' }}</div>
          <div class="text-caption text-muted">{{ cell.row.companyName || cell.row.contactEmail }}</div>
        </q-td>
      </template>
      <template #body-cell-contactPhone="cell">
        <q-td :props="cell">{{ cell.row.contactPhone || '—' }}</q-td>
      </template>
      <template #body-cell-submittedOnUtc="cell">
        <q-td :props="cell">{{ formatDate(cell.row.submittedOnUtc) }}</q-td>
      </template>
      <template #body-cell-estimatedValue="cell">
        <q-td :props="cell">{{ formatMoney(cell.row.estimatedValue) }}</q-td>
      </template>
      <template #body-cell-inquiryStatus="cell">
        <q-td :props="cell">
          <q-badge
            :color="inquiryStatusColor(cell.row.inquiryStatus)"
            :label="humanStatus(cell.row.inquiryStatus)"
          />
          <q-icon v-if="cell.row.hasBeenQuoted" name="o_mark_email_read" size="16px" class="q-ml-xs text-green-7">
            <q-tooltip>A quote has been sent</q-tooltip>
          </q-icon>
        </q-td>
      </template>
      <template #actions="{ row }">
        <q-btn flat round dense icon="o_visibility" @click="open(row)"><q-tooltip>Manage</q-tooltip></q-btn>
      </template>
    </AppDataTable>
  </q-page>
</template>

<script setup>
/*
 * Admin inquiry list (REQ-INQ-001). Every filter is applied server-side (status, store, quote-sent,
 * date window and the search term), so the paging counts are true rather than page-local.
 *
 * "Value" is what the request was priced at, not revenue — inquiries are excluded from every sales
 * report by ExcludeInquiries().
 */
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'
import { storeApi } from 'modules/stores/api'
import { formatMoney, formatDate } from 'modules/orders/api'
import { inquiryApi, inquiryStatusOptions, inquiryStatusColor, humanStatus } from 'modules/inquiries/api'

const router = useRouter()
const notify = useNotify()

const columns = [
  { name: 'referenceNumber', label: 'Reference', field: 'referenceNumber', align: 'left', sortable: true },
  { name: 'contactName', label: 'Customer', field: 'contactName', align: 'left' },
  { name: 'contactPhone', label: 'Phone', field: 'contactPhone', align: 'left' },
  { name: 'submittedOnUtc', label: 'Submitted', field: 'submittedOnUtc', align: 'left', sortable: true },
  { name: 'itemCount', label: 'Items', field: 'itemCount', align: 'center' },
  { name: 'estimatedValue', label: 'Value', field: 'estimatedValue', align: 'right', sortable: true },
  { name: 'assignedStoreName', label: 'Store', field: 'assignedStoreName', align: 'left' },
  { name: 'inquiryStatus', label: 'Status', field: 'inquiryStatus', align: 'left', sortable: true }
]

const quotedOptions = [
  { label: 'Quote sent', value: true },
  { label: 'Not yet quoted', value: false }
]

const rows = ref([])
const stores = ref([])
const loading = ref(false)
const search = ref('')
const statusFilter = ref(null)
const storeFilter = ref(null)
const quotedFilter = ref(null)
const fromFilter = ref(null)
const toFilter = ref(null)
const filtersOpen = ref(false)
const pagination = ref({ page: 1, rowsPerPage: 20, rowsNumber: 0 })

const storeOptions = computed(() => stores.value.map((s) => ({ label: s.name, value: s.id })))
const activeFilterCount = computed(
  () => [statusFilter.value, storeFilter.value, quotedFilter.value, fromFilter.value, toFilter.value]
    .filter((v) => v !== null && v !== undefined && v !== '').length
)

async function fetch (props) {
  const p = props?.pagination || pagination.value
  loading.value = true
  try {
    const result = await inquiryApi.list({
      page: p.page,
      pageSize: p.rowsPerPage,
      status: statusFilter.value || undefined,
      storeId: storeFilter.value || undefined,
      quoted: quotedFilter.value === null ? undefined : quotedFilter.value,
      fromUtc: fromFilter.value || undefined,
      toUtc: toFilter.value || undefined,
      search: search.value || undefined,
      sortBy: p.sortBy || undefined,
      sortDescending: !!p.descending
    })
    rows.value = Array.isArray(result?.items) ? result.items : []
    pagination.value = { ...p, rowsNumber: result?.totalCount ?? rows.value.length }
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
function clearFilters () {
  statusFilter.value = null
  storeFilter.value = null
  quotedFilter.value = null
  fromFilter.value = null
  toFilter.value = null
  reload()
}
function open (row) { router.push({ name: 'admin-inquiry-detail', params: { id: row.id } }) }

onMounted(async () => {
  // The store filter is a convenience; a failure must not stop the list rendering.
  stores.value = await storeApi.list({ page: 1, pageSize: 200 })
    .then((r) => (r && (r.items || r)) || [])
    .catch(() => [])
  fetch()
})
</script>
