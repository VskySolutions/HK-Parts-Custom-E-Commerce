<template>
  <q-page class="app-page">
    <AppListHeader
      title="Blocked buyers"
      subtitle="Buyers matching an active entry cannot place orders, whatever the payment method."
      :breadcrumbs="[{ label: 'Home', icon: 'o_home', to: '/dashboard' }, { label: 'Customers', to: '/customers' }, { label: 'Blocked buyers' }]"
      show-add
      add-label="Block a buyer"
      @add="onAdd"
    >
      <template #actions>
        <q-input v-model="search" dense outlined debounce="300" placeholder="Search" style="min-width: 240px">
          <template #prepend><q-icon name="o_search" /></template>
          <template v-if="search" #append><q-icon name="o_close" class="cursor-pointer" @click="search = ''" /></template>
        </q-input>
      </template>
    </AppListHeader>

    <q-tabs v-model="tab" dense align="left" no-caps class="q-mb-md text-primary">
      <q-tab name="entries" label="Blocked buyers" />
      <q-tab name="hits" label="Blocked attempts" />
    </q-tabs>

    <AppDataTable
      v-if="tab === 'entries'"
      page-key="admin-customer-blacklist"
      row-key="id"
      title="Blocked buyers"
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
          <a class="text-primary cursor-pointer text-weight-medium" @click="onEdit(cell.row)">{{ nameOf(cell.row) || '—' }}</a>
        </q-td>
      </template>
      <template #body-cell-status="cell">
        <q-td :props="cell">
          <q-badge :color="cell.row.status === 'Blocked' ? 'negative' : 'grey'" :label="cell.row.status" />
        </q-td>
      </template>
      <template #body-cell-hitCount="cell">
        <q-td :props="cell">
          {{ cell.row.hitCount }}
          <span v-if="cell.row.lastHitUtc" class="text-caption text-grey-7">(last {{ formatDate(cell.row.lastHitUtc) }})</span>
        </q-td>
      </template>
      <template #actions="{ row }">
        <q-btn flat round dense icon="o_edit" @click="onEdit(row)"><q-tooltip>Edit</q-tooltip></q-btn>
        <q-btn flat round dense icon="o_delete" color="negative" @click="onDelete(row)"><q-tooltip>Delete</q-tooltip></q-btn>
      </template>
    </AppDataTable>

    <AppDataTable
      v-else
      page-key="admin-customer-blacklist-hits"
      row-key="id"
      title="Blocked attempts"
      :rows="hits"
      :columns="hitColumns"
      :loading="loadingHits"
      :pagination="pagination"
      no-data-label="No blocked attempts yet."
      @refresh="loadHits"
    >
      <template #body-cell-atUtc="cell">
        <q-td :props="cell">{{ formatDate(cell.row.atUtc) }}</q-td>
      </template>
      <template #body-cell-name="cell">
        <q-td :props="cell">{{ nameOf(cell.row) || '—' }}</q-td>
      </template>
    </AppDataTable>

    <AppFormDrawer
      v-model="drawerOpen"
      :title="editing.id ? 'Edit blocked buyer' : 'Block a buyer'"
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
        <q-toggle
          :model-value="editing.status === 'Blocked'"
          label="Block this buyer"
          @update:model-value="editing.status = $event ? 'Blocked' : 'Inactive'"
        />
      </div>
    </AppFormDrawer>
  </q-page>
</template>

<script setup>
/*
 * Customer blacklist (PY-10 / PY-15). A placement whose billing name and / or email matches an active
 * entry is refused before any gateway is contacted, an alert email is sent and the attempt is logged
 * (the "Blocked attempts" tab). Lists are small, so both tables load up to 200 rows and filter locally.
 */
import { ref, computed, watch, onMounted } from 'vue'
import { date } from 'quasar'
import { blacklistApi } from 'modules/settings/checkout-api'
import { getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'
import { deleteConfirmation } from 'dialogs/delete_confirmation'

const notify = useNotify()

const columns = [
  { name: 'name', label: 'Name', field: (r) => nameOf(r), align: 'left' },
  { name: 'email', label: 'Email', field: 'email', align: 'left' },
  { name: 'reason', label: 'Reason', field: 'reason', align: 'left' },
  { name: 'status', label: 'Status', field: 'status', align: 'center' },
  { name: 'hitCount', label: 'Blocked attempts', field: 'hitCount', align: 'left' }
]
const hitColumns = [
  { name: 'atUtc', label: 'When', field: 'atUtc', align: 'left' },
  { name: 'name', label: 'Name', field: (r) => nameOf(r), align: 'left' },
  { name: 'email', label: 'Email', field: 'email', align: 'left' },
  { name: 'matchedOn', label: 'Matched on', field: 'matchedOn', align: 'left' },
  { name: 'ipAddress', label: 'IP address', field: 'ipAddress', align: 'left' }
]
const pagination = ref({ rowsPerPage: 25 })

const tab = ref('entries')
const search = ref('')
const rows = ref([])
const hits = ref([])
const loading = ref(false)
const loadingHits = ref(false)
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
    const res = await blacklistApi.list({ search: searchTerm.value, page: 1, pageSize: 200 })
    rows.value = res?.items || []
  } catch (err) {
    rows.value = []
    notify.error(getApiErrorMessage(err))
  } finally {
    loading.value = false
  }
}

async function loadHits () {
  loadingHits.value = true
  try {
    const res = await blacklistApi.hits({ page: 1, pageSize: 200 })
    hits.value = res?.items || []
  } catch (err) {
    hits.value = []
    notify.error(getApiErrorMessage(err))
  } finally {
    loadingHits.value = false
  }
}

watch(search, load)
watch(tab, (t) => { if (t === 'hits') loadHits() })

function onAdd () {
  editing.value = blank()
  drawerOpen.value = true
}

function onEdit (row) {
  editing.value = {
    id: row.id,
    firstName: row.firstName || '',
    lastName: row.lastName || '',
    email: row.email || '',
    reason: row.reason || '',
    status: row.status || 'Blocked'
  }
  drawerOpen.value = true
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
    if (e.id) await blacklistApi.update(e.id, payload)
    else await blacklistApi.create(payload)
    notify.success(e.id ? 'Entry updated' : 'Buyer blocked')
    drawerOpen.value = false
    await load()
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    saving.value = false
  }
}

async function onDelete (row) {
  if (!(await deleteConfirmation(`the blacklist entry for "${nameOf(row) || row.email}"`))) return
  try {
    await blacklistApi.remove(row.id)
    notify.success('Entry deleted')
    await load()
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  }
}

onMounted(load)
</script>
