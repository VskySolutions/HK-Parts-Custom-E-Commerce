<template>
  <q-page class="app-page">
    <AppListHeader
      title="Guest Customers"
      subtitle="Buyers who placed an order without registering, one row per email."
      :breadcrumbs="[{ label: 'Home', icon: 'o_home', to: '/dashboard' }, { label: 'Customers', to: '/customers' }, { label: 'Guest Customers' }]"
      :show-add="false"
    >
      <template #actions>
        <div class="row items-center q-gutter-sm no-wrap">
          <q-input v-model="filters.search" dense outlined debounce="400" placeholder="Search name, email, phone or order #" style="min-width: 260px" @update:model-value="reload">
            <template #prepend><q-icon name="o_search" /></template>
          </q-input>
          <q-select
            v-model="filters.status"
            dense
            outlined
            emit-value
            map-options
            :options="statusOptions"
            style="min-width: 130px"
            @update:model-value="reload"
          />
          <q-select
            v-model="filters.account"
            dense
            outlined
            emit-value
            map-options
            :options="accountOptions"
            style="min-width: 150px"
            aria-label="Account"
            @update:model-value="reload"
          />
          <q-btn flat no-caps color="primary" label="Clear" :disable="!hasFilters" @click="clearFilters" />
          <q-btn-dropdown outline no-caps color="primary" icon="o_download" label="Export" :loading="exporting">
            <q-list dense>
              <q-item v-close-popup clickable @click="exportList('csv')"><q-item-section>CSV</q-item-section></q-item>
              <q-item v-close-popup clickable @click="exportList('xlsx')"><q-item-section>Excel (.xlsx)</q-item-section></q-item>
            </q-list>
          </q-btn-dropdown>
        </div>
      </template>
    </AppListHeader>

    <AppDataTable
      page-key="admin-guest-customers"
      row-key="guestId"
      title="Guest customers"
      :rows="rows"
      :columns="columns"
      :loading="loading"
      :pagination="pagination"
      show-actions
      no-data-label="No guest customers match."
      @request="onRequest"
      @refresh="reload"
    >
      <template #body-cell-name="cell">
        <q-td :props="cell">
          <a class="text-primary cursor-pointer text-weight-medium" @click="openOrders(cell.row)">{{ nameOf(cell.row) || '—' }}</a>
          <q-badge v-if="cell.row.isBlocked" color="negative" label="Blocked" class="q-ml-sm" />
          <q-badge v-if="cell.row.anonymizedOnUtc" color="grey-6" label="Anonymised" class="q-ml-sm">
            <q-tooltip>Personal data removed on request on {{ formatDate(cell.row.anonymizedOnUtc) }}. Firearm orders are kept as the law requires.</q-tooltip>
          </q-badge>
          <!-- A guest who registered stays listed, marked Registered (click opens the customer account). -->
          <q-badge v-if="cell.row.isRegistered" color="indigo" label="Registered" class="q-ml-sm cursor-pointer" @click="viewAccount(cell.row)">
            <q-tooltip>
              <template v-if="cell.row.linkedOnUtc">Registered an account; orders moved to it on {{ formatDate(cell.row.linkedOnUtc) }}.</template>
              <template v-else>Registered an account with this email. Guest orders not moved to it yet.</template>
              Click to open the account.
            </q-tooltip>
          </q-badge>
        </q-td>
      </template>
      <template #body-cell-location="cell">
        <q-td :props="cell">{{ locationOf(cell.row) || '—' }}</q-td>
      </template>
      <template #body-cell-totalSpent="cell">
        <q-td :props="cell" class="text-right">{{ formatMoney(cell.row.totalSpent) }}</q-td>
      </template>
      <template #body-cell-lastOrderUtc="cell">
        <q-td :props="cell">{{ formatDate(cell.row.lastOrderUtc) }}</q-td>
      </template>
      <template #actions="{ row }">
        <q-btn flat round dense icon="o_receipt_long" @click="openOrders(row)"><q-tooltip>View orders</q-tooltip></q-btn>
        <q-btn v-if="row.registeredCustomerId && row.unlinkedOrderCount > 0" flat round dense icon="o_link" color="indigo" :loading="linkingId === row.guestId" @click="linkOrders(row)">
          <q-tooltip>Move these orders to the account</q-tooltip>
        </q-btn>
        <q-btn v-if="row.isBlocked" flat round dense icon="o_lock_open" color="positive" :loading="unblockingId === row.guestId" @click="unblock(row)">
          <q-tooltip>Unblock</q-tooltip>
        </q-btn>
        <q-btn v-else flat round dense icon="o_block" color="negative" @click="openBlock(row)"><q-tooltip>Block</q-tooltip></q-btn>
        <q-btn v-if="!row.anonymizedOnUtc" flat round dense icon="o_person_off" color="grey-8" :loading="anonymizingId === row.guestId" @click="anonymize(row)">
          <q-tooltip>Anonymise personal data (deletion request)</q-tooltip>
        </q-btn>
      </template>
    </AppDataTable>

    <!-- Guest details and orders (A-14) -->
    <q-dialog v-model="ordersOpen">
      <q-card style="width: 900px; max-width: 95vw">
        <q-card-section class="row items-center q-pb-none">
          <div>
            <div class="text-h6">
              {{ nameOf(selected) || selected?.email }}
              <q-badge v-if="selected?.isBlocked" color="negative" label="Blocked" class="q-ml-sm" />
            </div>
            <div class="text-caption text-grey-7">
              {{ selected?.email }}<span v-if="selected?.phoneNumber"> · {{ selected.phoneNumber }}</span>
              <span v-if="selected?.marketingOptIn"> · Opted in to marketing</span>
            </div>
          </div>
          <q-space />
          <q-btn flat round dense icon="o_close" v-close-popup />
        </q-card-section>
        <q-card-section>
          <q-table
            flat
            dense
            row-key="id"
            :rows="orders"
            :columns="orderColumns"
            :loading="loadingOrders"
            :pagination="{ rowsPerPage: 0 }"
            hide-pagination
            no-data-label="No orders."
          >
            <template #body-cell-orderNumber="cell">
              <q-td :props="cell">
                <a class="text-primary cursor-pointer text-weight-medium" @click="viewOrder(cell.row)">{{ cell.row.orderNumber }}</a>
                <q-badge v-if="cell.row.linkedToAccount" color="indigo" outline label="Moved to account" class="q-ml-xs">
                  <q-tooltip>Placed as a guest, now in the customer's account order history.</q-tooltip>
                </q-badge>
              </q-td>
            </template>
            <template #body-cell-status="cell">
              <q-td :props="cell"><q-badge outline color="primary" :label="cell.row.status" /></q-td>
            </template>
            <template #body-cell-paymentStatus="cell">
              <q-td :props="cell"><q-badge outline :color="cell.row.paymentStatus === 'Captured' ? 'positive' : 'grey-7'" :label="cell.row.paymentStatus" /></q-td>
            </template>
          </q-table>
        </q-card-section>
      </q-card>
    </q-dialog>

    <!-- Block dialog (A-16): prefilled with the guest's name and email; a reason is required. -->
    <q-dialog v-model="blockOpen" persistent>
      <q-card style="width: 480px; max-width: 95vw">
        <q-card-section class="text-h6">Block guest</q-card-section>
        <q-card-section class="q-pt-none">
          <div class="row q-col-gutter-sm">
            <div class="col-6"><AppTextField label="First name" :model-value="block.firstName" @update:model-value="block.firstName = $event" /></div>
            <div class="col-6"><AppTextField label="Last name" :model-value="block.lastName" @update:model-value="block.lastName = $event" /></div>
            <div class="col-12"><AppTextField label="Email" :model-value="block.email" @update:model-value="block.email = $event" /></div>
            <div class="col-12">
              <AppTextField
                label="Reason"
                required
                type="textarea"
                autogrow
                maxlength="1000"
                :model-value="block.reason"
                :v="reasonV"
                @update:model-value="block.reason = $event"
              />
            </div>
          </div>
          <div class="text-caption text-grey-7 q-mt-sm">
            They are stopped at payment on their next checkout. Existing orders are not changed.
          </div>
        </q-card-section>
        <q-card-actions align="right">
          <q-btn flat no-caps label="Cancel" v-close-popup />
          <q-btn unelevated no-caps color="negative" label="Block" :loading="blocking" @click="saveBlock" />
        </q-card-actions>
      </q-card>
    </q-dialog>
  </q-page>
</template>

<script setup>
/*
 * Guest Customers (docs/guest-checkout-requirements.md §4.2): one row per normalized guest email (A-12), newest
 * first, 25 per page (A-10). Search + status filter persist for the browser session and Clear resets them (A-11).
 * Blocked guests stay listed with a badge and an Unblock action (A-13); Block opens the blacklist dialog prefilled
 * with the guest's details and needs a reason (A-16). Orders load by opaque guest id (A-15). Guests whose email
 * now has an account can have their orders moved onto it (A-18); guests who registered stay listed with a
 * Registered badge and an Account filter (All / Guest only / Registered). Export downloads the filtered list (A-17).
 */
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'
import { customerAdminApi } from 'modules/customers/api'
import { blacklistApi } from 'modules/settings/checkout-api'
import { confirmation } from 'dialogs/confirmation'
import { formatDateTime as formatDate } from 'src/utils/datetime'
import { formatMoney } from 'modules/orders/api'

const notify = useNotify()
const router = useRouter()

const FILTER_KEY = 'admin-guest-customers-filters'
// Guests who registered stay listed; this narrows to them or to guest-only buyers.
const accountOptions = [
  { label: 'Any account', value: 'All' },
  { label: 'Guest only', value: 'GuestOnly' },
  { label: 'Registered', value: 'Registered' }
]
const statusOptions = [
  { label: 'All', value: 'All' },
  { label: 'Active', value: 'Active' },
  { label: 'Blocked', value: 'Blocked' }
]

const columns = [
  { name: 'name', label: 'Name', field: (r) => nameOf(r), align: 'left' },
  { name: 'email', label: 'Email', field: 'email', align: 'left' },
  { name: 'phoneNumber', label: 'Phone', field: (r) => r.phoneNumber || '—', align: 'left' },
  { name: 'location', label: 'Location', field: (r) => locationOf(r), align: 'left' },
  { name: 'orderCount', label: 'Orders', field: 'orderCount', align: 'center' },
  { name: 'totalSpent', label: 'Total spent', field: 'totalSpent', align: 'right' },
  { name: 'lastOrderUtc', label: 'Last order', field: 'lastOrderUtc', align: 'left' }
]
const orderColumns = [
  { name: 'orderNumber', label: 'Order #', field: 'orderNumber', align: 'left' },
  { name: 'placedOnUtc', label: 'Date', field: (r) => formatDate(r.placedOnUtc), align: 'left' },
  { name: 'shipTo', label: 'Ship to', field: (r) => r.shipTo || '—', align: 'left' },
  { name: 'fflNumber', label: 'FFL #', field: (r) => r.fflNumber || '—', align: 'left' },
  { name: 'totalAmount', label: 'Total', field: (r) => formatMoney(r.totalAmount), align: 'right' },
  { name: 'paidAmount', label: 'Paid', field: (r) => formatMoney(r.paidAmount), align: 'right' },
  { name: 'status', label: 'Status', field: 'status', align: 'left' },
  { name: 'paymentStatus', label: 'Payment', field: 'paymentStatus', align: 'left' }
]

function loadFilters () {
  try {
    const saved = JSON.parse(sessionStorage.getItem(FILTER_KEY) || 'null')
    if (saved && typeof saved === 'object') return { search: saved.search || '', status: saved.status || 'All', account: saved.account || 'All' }
  } catch (e) { /* storage unavailable: start clean */ }
  return { search: '', status: 'All', account: 'All' }
}
function saveFilters () {
  try { sessionStorage.setItem(FILTER_KEY, JSON.stringify(filters.value)) } catch (e) { /* ignore */ }
}

const rows = ref([])
const loading = ref(false)
const filters = ref(loadFilters())
const pagination = ref({ page: 1, rowsPerPage: 25, rowsNumber: 0 })
const hasFilters = computed(() => !!filters.value.search || filters.value.status !== 'All' || filters.value.account !== 'All')

const ordersOpen = ref(false)
const selected = ref(null)
const orders = ref([])
const loadingOrders = ref(false)
const exporting = ref(false)
const linkingId = ref(null)
const unblockingId = ref(null)
const anonymizingId = ref(null)

const blockOpen = ref(false)
const blocking = ref(false)
const block = ref({ firstName: '', lastName: '', email: '', reason: '' })
const reasonTouched = ref(false)
const reasonV = computed(() => {
  const errors = reasonTouched.value && !String(block.value.reason || '').trim() ? [{ $message: 'A reason is required' }] : []
  return { $error: errors.length > 0, $errors: errors, $touch: () => { reasonTouched.value = true } }
})

function nameOf (r) {
  return r ? [r.firstName, r.lastName].filter(Boolean).join(' ') : ''
}

function locationOf (r) {
  return [r.city, r.stateProvince, r.countryCode].filter(Boolean).join(', ')
}

function queryParams () {
  return { search: filters.value.search || undefined, status: filters.value.status, account: filters.value.account }
}

async function fetch (props) {
  const p = props?.pagination || pagination.value
  saveFilters()
  loading.value = true
  try {
    const r = await customerAdminApi.listGuests({ page: p.page, pageSize: p.rowsPerPage, ...queryParams() })
    rows.value = Array.isArray(r?.items) ? r.items : []
    pagination.value = { ...p, rowsNumber: r?.totalCount ?? rows.value.length }
  } catch (e) {
    rows.value = []
    pagination.value = { ...p, rowsNumber: 0 }
    notify.error(getApiErrorMessage(e))
  } finally {
    loading.value = false
  }
}
function onRequest (props) { fetch(props) }
function reload () { fetch({ pagination: { ...pagination.value, page: 1 } }) }
function clearFilters () {
  filters.value = { search: '', status: 'All', account: 'All' }
  reload()
}

async function exportList (format) {
  exporting.value = true
  try {
    const blob = await customerAdminApi.exportGuests({ ...queryParams(), format })
    const type = format === 'xlsx' ? 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' : 'text/csv'
    const url = window.URL.createObjectURL(new Blob([blob], { type }))
    const a = document.createElement('a')
    a.href = url
    a.download = `guest-customers-${new Date().toISOString().slice(0, 10)}.${format}`
    document.body.appendChild(a)
    a.click()
    a.remove()
    window.URL.revokeObjectURL(url)
  } catch (e) {
    notify.error(getApiErrorMessage(e))
  } finally {
    exporting.value = false
  }
}

async function openOrders (row) {
  selected.value = row
  orders.value = []
  ordersOpen.value = true
  loadingOrders.value = true
  try {
    orders.value = (await customerAdminApi.listGuestOrders(row.guestId)) || []
  } catch (e) {
    notify.error(getApiErrorMessage(e))
  } finally {
    loadingOrders.value = false
  }
}

function openBlock (row) {
  block.value = { firstName: row.firstName || '', lastName: row.lastName || '', email: row.email, reason: '' }
  reasonTouched.value = false
  blockOpen.value = true
}

async function saveBlock () {
  reasonTouched.value = true
  if (reasonV.value.$error) return
  blocking.value = true
  try {
    await blacklistApi.create({
      firstName: block.value.firstName || null,
      lastName: block.value.lastName || null,
      email: block.value.email || null,
      reason: block.value.reason.trim(),
      status: 'Blocked',
      source: 'Guest'
    })
    blockOpen.value = false
    notify.success('Guest blocked.')
    fetch()
  } catch (e) {
    notify.error(getApiErrorMessage(e))
  } finally {
    blocking.value = false
  }
}

async function unblock (row) {
  const ok = await confirmation({
    title: 'Unblock guest',
    message: `Let ${nameOf(row) || row.email} place orders again?`,
    okLabel: 'Unblock'
  })
  if (!ok || !row.blacklistEntryId) return
  unblockingId.value = row.guestId
  try {
    // The entry may have matched on email or on name: find it either way, then save it back as Inactive unchanged.
    const find = async (search) => search
      ? ((await blacklistApi.list({ search, page: 1, pageSize: 100 }))?.items || []).find((x) => x.id === row.blacklistEntryId)
      : null
    const entry = (await find(row.email)) || (await find(row.lastName))
    if (!entry) throw new Error('The blacklist entry could not be found. Unblock it from the Blacklist page.')
    await blacklistApi.update(entry.id, {
      firstName: entry.firstName ?? null,
      lastName: entry.lastName ?? null,
      email: entry.email ?? null,
      reason: entry.reason ?? null,
      status: 'Inactive',
      source: entry.source ?? null
    })
    notify.success('Guest unblocked.')
    fetch()
  } catch (e) {
    notify.error(getApiErrorMessage(e))
  } finally {
    unblockingId.value = null
  }
}

// N-05: a deletion request. Non-firearm orders lose name, email, phone and street address; firearm orders stay.
async function anonymize (row) {
  const ok = await confirmation({
    title: 'Anonymise guest',
    message: `Remove ${nameOf(row) || row.email}'s name, email, phone and street address from their guest orders? ` +
      'Firearm orders are kept as the law requires. This cannot be undone.',
    okLabel: 'Anonymise',
    color: 'negative'
  })
  if (!ok) return
  anonymizingId.value = row.guestId
  try {
    const r = await customerAdminApi.anonymizeGuest(row.guestId)
    const kept = r?.keptFirearmOrders ? ` ${r.keptFirearmOrders} firearm order(s) kept.` : ''
    notify.success(`${r?.anonymizedOrders ?? 0} order(s) anonymised.${kept}`)
    fetch()
  } catch (e) {
    notify.error(getApiErrorMessage(e))
  } finally {
    anonymizingId.value = null
  }
}

async function linkOrders (row) {
  const ok = await confirmation({
    title: 'Move orders to account',
    message: `Move ${row.unlinkedOrderCount} guest order(s) for ${row.email} onto the account registered with that email? They will then show in the customer's order history.`,
    okLabel: 'Move orders'
  })
  if (!ok) return
  linkingId.value = row.guestId
  try {
    const r = await customerAdminApi.linkGuestOrders(row.guestId)
    notify.success(`${r?.linkedOrders ?? 0} order(s) moved to the account.`)
    fetch()
  } catch (e) {
    notify.error(getApiErrorMessage(e))
  } finally {
    linkingId.value = null
  }
}

function viewOrder (order) {
  router.push({ name: 'admin-order-detail', params: { id: order.id } })
}

function viewAccount (row) {
  router.push({ name: 'admin-customer-detail', params: { id: row.registeredCustomerId } })
}

onMounted(fetch)
</script>
