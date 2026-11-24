<template>
  <q-page class="app-page">
    <AppListHeader
      title="Blocked customers"
      subtitle="Customers matching an active entry cannot place orders, whatever the payment method."
      :breadcrumbs="[{ label: 'Home', icon: 'o_home', to: '/dashboard' }, { label: 'Customers', to: '/customers' }, { label: 'Blocked customers' }]"
    >
      <template #actions>
        <q-input v-model="search" dense outlined debounce="300" placeholder="Search" style="min-width: 240px">
          <template #prepend><q-icon name="o_search" /></template>
          <template v-if="search" #append><q-icon name="o_close" class="cursor-pointer" @click="search = ''" /></template>
        </q-input>
      </template>
    </AppListHeader>

    <q-tabs v-model="tab" dense align="left" no-caps class="q-mb-md text-primary">
      <q-tab name="entries" label="Blocked customers" />
      <q-tab name="history" label="Block / Unblock History" />
    </q-tabs>

    <AppDataTable
      v-if="tab === 'entries'"
      page-key="admin-customer-blacklist"
      row-key="id"
      title="Blocked customers"
      :rows="rows"
      :columns="columns"
      :loading="loading"
      :pagination="pagination"
      show-actions
      no-data-label="Nobody is blocked."
      @refresh="load"
    >
      <template #body-cell-name="cell">
        <q-td :props="cell">
          <span class="text-weight-medium">{{ nameOf(cell.row) || '—' }}</span>
        </q-td>
      </template>
      <template #body-cell-status="cell">
        <q-td :props="cell">
          <q-badge :color="cell.row.status === 'Blocked' ? 'negative' : 'grey'" :label="cell.row.status" />
        </q-td>
      </template>
      <template #actions="{ row }">
        <q-btn v-if="row.status === 'Blocked'" flat round dense icon="o_lock_open" color="positive" @click="onUnblock(row)"><q-tooltip>Unblock customer</q-tooltip></q-btn>
      </template>
    </AppDataTable>

    <AppDataTable
      v-else
      page-key="admin-customer-blacklist-history"
      row-key="subjectKey"
      title="Block / Unblock History"
      :rows="history"
      :columns="historyColumns"
      :loading="loadingHistory"
      :pagination="pagination"
      show-actions
      no-data-label="No customer has been blocked yet."
      @refresh="loadHistory"
    >
      <template #body-cell-name="cell">
        <q-td :props="cell">
          <a class="text-primary cursor-pointer text-weight-medium" @click="openTimeline(cell.row)">{{ nameOf(cell.row) || cell.row.email || '—' }}</a>
        </q-td>
      </template>
      <template #body-cell-timesBlocked="cell">
        <q-td :props="cell"><q-badge color="negative" outline :label="cell.row.timesBlocked" /></q-td>
      </template>
      <template #body-cell-timesUnblocked="cell">
        <q-td :props="cell"><q-badge color="positive" outline :label="cell.row.timesUnblocked" /></q-td>
      </template>
      <template #body-cell-lastAction="cell">
        <q-td :props="cell">
          <q-badge :color="cell.row.lastAction === 'Blocked' ? 'negative' : 'positive'" :label="cell.row.lastAction === 'Blocked' ? 'Blocked' : 'Unblocked'" />
        </q-td>
      </template>
      <template #body-cell-lastActionUtc="cell">
        <q-td :props="cell">{{ formatDate(cell.row.lastActionUtc) }}</q-td>
      </template>
      <template #actions="{ row }">
        <q-btn flat round dense icon="o_history" @click="openTimeline(row)"><q-tooltip>View history</q-tooltip></q-btn>
      </template>
    </AppDataTable>

    <q-dialog v-model="timelineOpen">
      <q-card style="width: 560px; max-width: 95vw">
        <q-card-section class="row items-center q-pb-none">
          <div>
            <div class="text-h6">{{ nameOf(timelineRow || {}) || timelineRow?.email }}</div>
            <div class="text-caption text-grey-7">
              {{ timelineRow?.email }}
              <span v-if="timelineRow"> · Blocked {{ timelineRow.timesBlocked }}× · Unblocked {{ timelineRow.timesUnblocked }}×</span>
            </div>
          </div>
          <q-space />
          <q-btn flat round dense icon="o_close" v-close-popup />
        </q-card-section>
        <q-card-section style="max-height: 60vh" class="scroll">
          <div v-if="loadingTimeline" class="text-center q-pa-lg"><q-spinner size="32px" color="primary" /></div>
          <div v-else-if="!timeline.length" class="text-grey-7 q-pa-md">No history.</div>
          <q-timeline v-else color="primary" layout="dense">
            <q-timeline-entry
              v-for="ev in timeline"
              :key="ev.id"
              :color="ev.action === 'Blocked' ? 'negative' : 'positive'"
              :icon="ev.action === 'Blocked' ? 'o_block' : 'o_lock_open'"
              :title="ev.action === 'Blocked' ? 'Blocked' : 'Unblocked'"
              :subtitle="formatDate(ev.atUtc)"
            >
              <div v-if="ev.reason" class="text-body2"><span class="text-grey-7">Reason:</span> {{ ev.reason }}</div>
              <div v-if="ev.changedBy" class="text-caption text-grey-7">By {{ ev.changedBy }}</div>
            </q-timeline-entry>
          </q-timeline>
        </q-card-section>
      </q-card>
    </q-dialog>

    <AppFormDrawer
      v-model="drawerOpen"
      title="Block a customer"
      :saving="saving"
      @submit="onSubmit"
    >
      <div class="column q-gutter-md">
        <div class="text-body2 text-muted">
          Enter an email, a full name, or both. How entries match is set on Checkout Settings.
        </div>
        <AppTextField v-model="editing.email" label="Email" maxlength="256" />
        <div class="row q-col-gutter-md">
          <div class="col-6"><AppTextField v-model="editing.firstName" label="First name" maxlength="100" /></div>
          <div class="col-6"><AppTextField v-model="editing.lastName" label="Last name" maxlength="100" /></div>
        </div>
        <AppTextField v-model="editing.reason" label="Reason" type="textarea" autogrow maxlength="1000" hint="Included in the alert email." />
      </div>
    </AppFormDrawer>

    <UnblockCustomerDialog v-model="unblockOpen" :entry="unblockRow" :saving="unblocking" @confirm="confirmUnblock" />
  </q-page>
</template>

<script setup>
/*
 * Customer blacklist (PY-10 / PY-15). A placement whose billing name and / or email matches an active
 * entry is refused before any gateway is contacted, an alert email is sent and the attempt is logged.
 * The history tab lists every person ever blocked with how many
 * times they were blocked / unblocked; a row opens the dated timeline. Lists load up to 200 rows.
 */
import { ref, computed, watch, onMounted } from 'vue'
import { date } from 'quasar'
import { blacklistApi } from 'modules/settings/checkout-api'
import { getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'
import UnblockCustomerDialog from 'modules/customers/components/UnblockCustomerDialog.vue'

const notify = useNotify()

const columns = [
  { name: 'name', label: 'Name', field: (r) => nameOf(r), align: 'left', sortable: true },
  { name: 'email', label: 'Email', field: 'email', align: 'left' },
  { name: 'reason', label: 'Reason', field: 'reason', align: 'left' },
  { name: 'status', label: 'Status', field: 'status', align: 'center' }
]
const historyColumns = [
  { name: 'name', label: 'Customer', field: (r) => nameOf(r), align: 'left' },
  { name: 'email', label: 'Email', field: 'email', align: 'left' },
  { name: 'timesBlocked', label: 'Times blocked', field: 'timesBlocked', align: 'center' },
  { name: 'timesUnblocked', label: 'Times unblocked', field: 'timesUnblocked', align: 'center' },
  { name: 'lastAction', label: 'Current status', field: 'lastAction', align: 'center' },
  { name: 'lastActionUtc', label: 'Last change', field: 'lastActionUtc', align: 'left' }
]
const pagination = ref({ rowsPerPage: 25 })

const tab = ref('entries')
const search = ref('')
const rows = ref([])
const history = ref([])
const loading = ref(false)
const loadingHistory = ref(false)
const timelineOpen = ref(false)
const timelineRow = ref(null)
const timeline = ref([])
const loadingTimeline = ref(false)
const drawerOpen = ref(false)
const saving = ref(false)
const editing = ref(blank())

function blank () {
  return { id: null, firstName: '', lastName: '', email: '', reason: '', status: 'Blocked' }
}

function nameOf (r) {
  return [r.firstName, r.lastName].filter(Boolean).join(' ')
}

function formatDate (v) {
  if (!v) return ''
  const iso = /(Z|[+-]\d\d:\d\d)$/.test(v) ? v : v + 'Z'
  return date.formatDate(iso, 'MMM D, YYYY h:mm A')
}

const searchTerm = computed(() => search.value.trim() || undefined)

async function load () {
  loading.value = true
  try {
    // Only currently blocked entries: an unblocked (Inactive) entry is kept server-side for its attempt history
    // but drops off this list, and its customer reappears in the Customers list.
    const res = await blacklistApi.list({ 
      search: searchTerm.value, 
      status: 'Blocked', 
      page: 1, 
      pageSize: 200
    })
    rows.value = res?.items || []
  } catch (err) {
    rows.value = []
    notify.error(getApiErrorMessage(err))
  } finally {
    loading.value = false
  }
}

async function loadHistory () {
  loadingHistory.value = true
  try {
    const res = await blacklistApi.history({ search: searchTerm.value, page: 1, pageSize: 200 })
    history.value = res?.items || []
  } catch (err) {
    history.value = []
    notify.error(getApiErrorMessage(err))
  } finally {
    loadingHistory.value = false
  }
}

async function openTimeline (row) {
  timelineRow.value = row
  timeline.value = []
  timelineOpen.value = true
  loadingTimeline.value = true
  try {
    timeline.value = (await blacklistApi.historyEvents(row.subjectKey)) || []
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    loadingTimeline.value = false
  }
}

watch(search, () => (tab.value === 'history' ? loadHistory() : load()))
watch(tab, (t) => (t === 'history' ? loadHistory() : load()))

function onAdd () {
  editing.value = blank()
  drawerOpen.value = true
}

// Unblock: the entry is kept (status Inactive) so its blocked-attempt history survives; a customer blocked
// from the Customers list reappears there (server keeps both in step).
const unblockOpen = ref(false)
const unblockRow = ref(null)
const unblocking = ref(false)

function onUnblock (row) {
  unblockRow.value = row
  unblockOpen.value = true
}

async function confirmUnblock (row) {
  if (!row) return
  unblocking.value = true
  try {
    await blacklistApi.update(row.id, {
      firstName: row.firstName || null,
      lastName: row.lastName || null,
      email: row.email || null,
      reason: row.reason || null,
      status: 'Inactive'
    })
    notify.success(`${nameOf(row) || row.email} has been unblocked`)
    unblockOpen.value = false
    await load()
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    unblocking.value = false
  }
}

async function onSubmit () {
  const e = editing.value
  if (!e.email.trim() && !(e.firstName.trim() && e.lastName.trim())) {
    notify.error('Enter an email address, or both a first and last name.')
    return
  }
  saving.value = true
  try {
    const payload = {
      firstName: e.firstName || null,
      lastName: e.lastName || null,
      email: e.email || null,
      reason: e.reason || null,
      status: e.status
    }
    await blacklistApi.create(payload)
    notify.success('Customer blocked')
    drawerOpen.value = false
    await load()
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    saving.value = false
  }
}

onMounted(load)
</script>
