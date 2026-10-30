<template>
  <q-page class="app-page">
    <AppListHeader
      title="Email Log"
      :breadcrumbs="[{ label: 'Home', icon: 'o_home', to: '/dashboard' }, { label: 'Email Log' }]"
      :show-add="false"
    >
      <template #actions>
        <q-input
          v-model="search"
          dense
          outlined
          debounce="400"
          placeholder="Search recipient or subject"
          style="min-width: 240px"
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

    <AppFilterDrawer v-model="filtersOpen" title="Filter emails" @clear="clearFilters">
      <AppSelect v-model="statusFilter" label="Delivery status" :options="statusOptions" @update:model-value="reload" />
      <AppSelect v-model="categoryFilter" label="Category" :options="categoryOptions" @update:model-value="reload" />
    </AppFilterDrawer>

    <AppDataTable
      page-key="admin-email-log"
      row-key="id"
      title="Email history"
      :rows="rows"
      :columns="columns"
      :loading="loading"
      :pagination="pagination"
      show-actions
      @request="onRequest"
      @refresh="reload"
    >
      <template #body-cell-recipient="cell">
        <q-td :props="cell">
          <a class="text-primary cursor-pointer text-weight-medium" @click="view(cell.row)">{{ cell.row.recipientEmail }}</a>
          <div v-if="cell.row.recipientName" class="text-caption text-grey-6">{{ cell.row.recipientName }}</div>
        </q-td>
      </template>
      <template #body-cell-status="cell">
        <q-td :props="cell">
          <q-badge :color="statusColor(cell.row.status)" :label="cell.row.status" />
          <q-icon v-if="cell.row.hasError" name="o_error" color="negative" size="16px" class="q-ml-xs">
            <q-tooltip>Delivery error — open to view</q-tooltip>
          </q-icon>
        </q-td>
      </template>
      <template #body-cell-createdOnUtc="cell"><q-td :props="cell">{{ formatDateTime(cell.row.createdOnUtc) }}</q-td></template>
      <template #actions="{ row }">
        <q-btn flat round dense icon="o_visibility" @click="view(row)"><q-tooltip>View</q-tooltip></q-btn>
        <q-btn flat round dense icon="o_send" @click="confirmResend(row)"><q-tooltip>Resend</q-tooltip></q-btn>
      </template>
    </AppDataTable>

    <!-- Detail dialog -->
    <q-dialog v-model="detail.open">
      <q-card style="width: 720px; max-width: 95vw">
        <q-card-section class="row items-center justify-between">
          <div class="text-subtitle1 text-weight-medium">Email details</div>
          <q-badge v-if="detail.data" :color="statusColor(detail.data.status)" :label="detail.data.status" />
        </q-card-section>
        <q-separator />
        <q-card-section class="scroll" style="max-height: 70vh">
          <q-inner-loading :showing="detail.loading" />
          <template v-if="detail.data">
            <div class="row q-col-gutter-md q-mb-md">
              <div class="col-12 col-sm-6"><AppFieldLabel label="To" /><div class="text-body2">{{ detail.data.recipientEmail }}<span v-if="detail.data.recipientName"> ({{ detail.data.recipientName }})</span></div></div>
              <div class="col-12 col-sm-6"><AppFieldLabel label="Category" /><div class="text-body2">{{ detail.data.category }}</div></div>
              <div class="col-12 col-sm-6"><AppFieldLabel label="Template" /><div class="text-body2">{{ detail.data.templateKey }}</div></div>
              <div class="col-12 col-sm-6"><AppFieldLabel label="Attempts" /><div class="text-body2">{{ detail.data.attemptCount }}</div></div>
              <div class="col-12 col-sm-6"><AppFieldLabel label="Created" /><div class="text-body2">{{ formatDateTime(detail.data.createdOnUtc) }}</div></div>
              <div class="col-12 col-sm-6"><AppFieldLabel label="Last attempt" /><div class="text-body2">{{ formatDateTime(detail.data.lastAttemptOnUtc) }}</div></div>
            </div>

            <AppFieldLabel label="Subject" />
            <div class="text-body2 text-weight-medium q-mb-md">{{ detail.data.subject }}</div>

            <q-banner v-if="detail.data.errorMessage" dense class="bg-red-1 text-red-9 rounded-borders q-mb-md">
              <template #avatar><q-icon name="o_error" color="red-9" /></template>
              {{ detail.data.errorMessage }}
            </q-banner>

            <AppFieldLabel label="Body" />
            <pre class="email-body">{{ detail.data.body }}</pre>
          </template>
        </q-card-section>
        <q-separator />
        <q-card-actions align="right">
          <q-btn flat no-caps label="Close" v-close-popup />
          <q-btn
            color="primary"
            unelevated
            no-caps
            icon="o_send"
            label="Resend"
            :loading="resending"
            :disable="!detail.data"
            @click="confirmResend(detail.data)"
          />
        </q-card-actions>
      </q-card>
    </q-dialog>
  </q-page>
</template>

<script setup>
/* Admin Email Log: browse the full email send history and re-send any entry. */
import { ref, reactive, computed, onMounted } from 'vue'
import { useQuasar } from 'quasar'
import { getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'
import { emailLogApi } from 'modules/email-log/api'
import { formatDateTime } from 'src/utils/datetime'
import AppListHeader from 'components/common/AppListHeader.vue'
import AppDataTable from 'components/common/AppDataTable.vue'
import AppFilterDrawer from 'components/common/AppFilterDrawer.vue'
import AppSelect from 'components/common/AppSelect.vue'
import AppFieldLabel from 'components/common/AppFieldLabel.vue'

const $q = useQuasar()
const notify = useNotify()

const columns = [
  { name: 'recipient', label: 'Recipient', field: 'recipientEmail', align: 'left', sortable: true },
  { name: 'subject', label: 'Subject', field: 'subject', align: 'left', sortable: true },
  { name: 'templateKey', label: 'Template', field: 'templateKey', align: 'left', sortable: true },
  { name: 'category', label: 'Category', field: 'category', align: 'left', sortable: true },
  { name: 'attemptCount', label: 'Attempts', field: 'attemptCount', align: 'center', sortable: true },
  { name: 'createdOnUtc', label: 'Created', field: 'createdOnUtc', align: 'left', sortable: true },
  { name: 'status', label: 'Status', field: 'status', align: 'center', sortable: true }
]

const statusOptions = [
  { label: 'All', value: null },
  { label: 'Pending', value: 'Pending' },
  { label: 'Sent', value: 'Sent' },
  { label: 'Retry', value: 'Retry' },
  { label: 'Failed', value: 'Failed' },
  { label: 'Suppressed', value: 'Suppressed' }
]
const categoryOptions = [
  { label: 'All', value: null },
  { label: 'Transactional', value: 'Transactional' },
  { label: 'Marketing', value: 'Marketing' }
]

const rows = ref([])
const loading = ref(false)
const search = ref('')
const statusFilter = ref(null)
const categoryFilter = ref(null)
const filtersOpen = ref(false)
const pagination = ref({ page: 1, rowsPerPage: 20, rowsNumber: 0 })
const resending = ref(false)

const detail = reactive({ open: false, loading: false, data: null })

const activeFilterCount = computed(() =>
  (statusFilter.value !== null ? 1 : 0) + (categoryFilter.value !== null ? 1 : 0)
)

function statusColor (status) {
  return {
    Sent: 'positive',
    Pending: 'blue-grey',
    Retry: 'orange',
    Failed: 'negative',
    Suppressed: 'grey'
  }[status] || 'grey'
}


function clearFilters () {
  statusFilter.value = null
  categoryFilter.value = null
  reload()
}

async function fetch (props) {
  const p = props?.pagination || pagination.value
  loading.value = true
  try {
    const r = await emailLogApi.list({
      page: p.page,
      pageSize: p.rowsPerPage,
      search: search.value || undefined,
      status: statusFilter.value || undefined,
      category: categoryFilter.value || undefined,
      sortBy: p.sortBy || undefined,
      sortDescending: !!p.descending
    })
    rows.value = Array.isArray(r?.items) ? r.items : []
    pagination.value = { ...p, rowsNumber: r?.totalCount ?? rows.value.length }
  } catch (e) { rows.value = []; notify.error(getApiErrorMessage(e)) } finally { loading.value = false }
}
function onRequest (props) { fetch(props) }
function reload () { fetch({ pagination: { ...pagination.value, page: 1 } }) }

async function view (row) {
  detail.open = true
  detail.loading = true
  detail.data = null
  try {
    detail.data = await emailLogApi.get(row.id)
  } catch (e) { notify.error(getApiErrorMessage(e)) } finally { detail.loading = false }
}

function confirmResend (row) {
  if (!row) return
  $q.dialog({
    title: 'Resend email',
    message: `Send a fresh copy of "${row.subject}" to ${row.recipientEmail}?`,
    cancel: true,
    ok: { label: 'Resend', color: 'primary', unelevated: true, noCaps: true }
  }).onOk(() => resend(row))
}

async function resend (row) {
  resending.value = true
  try {
    await emailLogApi.resend(row.id)
    notify.success('Email re-queued — it will be sent shortly.')
    detail.open = false
    reload()
  } catch (e) { notify.error(getApiErrorMessage(e)) } finally { resending.value = false }
}

onMounted(() => fetch())
</script>

<style scoped lang="scss">
.email-body {
  white-space: pre-wrap;
  word-break: break-word;
  font-family: inherit;
  font-size: 13px;
  background: rgba(0, 0, 0, 0.03);
  border-radius: 6px;
  padding: 12px;
  margin: 0;
}
body.body--dark .email-body {
  background: rgba(255, 255, 255, 0.06);
}
</style>
