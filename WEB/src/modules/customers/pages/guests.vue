<template>
  <q-page class="app-page">
    <AppListHeader
      title="Guest Customers"
      subtitle="Buyers who placed an order without registering, grouped by email."
      :breadcrumbs="[{ label: 'Home', icon: 'o_home', to: '/dashboard' }, { label: 'Customers', to: '/customers' }, { label: 'Guest Customers' }]"
      :show-add="false"
    >
      <template #actions>
        <q-input v-model="search" dense outlined debounce="400" placeholder="Search name, email, phone or order #" style="min-width: 280px" @update:model-value="reload">
          <template #prepend><q-icon name="o_search" /></template>
          <template v-if="search" #append><q-icon name="o_close" class="cursor-pointer" @click="search = ''; reload()" /></template>
        </q-input>
      </template>
    </AppListHeader>

    <AppDataTable
      page-key="admin-guest-customers"
      row-key="email"
      title="Guest customers"
      :rows="rows"
      :columns="columns"
      :loading="loading"
      :pagination="pagination"
      show-actions
      no-data-label="No guest orders yet."
      @request="onRequest"
      @refresh="reload"
    >
      <template #body-cell-name="cell">
        <q-td :props="cell">
          <a class="text-primary cursor-pointer text-weight-medium" @click="openOrders(cell.row)">{{ nameOf(cell.row) || '—' }}</a>
          <q-badge v-if="cell.row.registeredCustomerId" color="indigo" outline label="Registered since" class="q-ml-sm cursor-pointer" @click="viewAccount(cell.row)">
            <q-tooltip>This email now has an account. Click to open it.</q-tooltip>
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
      </template>
    </AppDataTable>

    <q-dialog v-model="ordersOpen">
      <q-card style="width: 720px; max-width: 95vw">
        <q-card-section class="row items-center q-pb-none">
          <div>
            <div class="text-h6">{{ nameOf(selected) || selected?.email }}</div>
            <div class="text-caption text-grey-7">{{ selected?.email }}<span v-if="selected?.phoneNumber"> · {{ selected.phoneNumber }}</span></div>
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
  </q-page>
</template>

<script setup>
/*
 * Guest customers: everyone who placed a storefront order without an account (the order has no customer),
 * one row per contact email, most recent first. Name / phone / location come from their latest order.
 * eBay imports and inquiries are not guest orders and are left out server-side. A guest who later registered
 * keeps their guest orders here and gets a "Registered since" badge linking to the account.
 */
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'
import { customerAdminApi } from 'modules/customers/api'
import { formatDateTime as formatDate } from 'src/utils/datetime'
import { formatMoney } from 'modules/orders/api'

const notify = useNotify()
const router = useRouter()

const columns = [
  { name: 'name', label: 'Name', field: (r) => nameOf(r), align: 'left' },
  { name: 'email', label: 'Email', field: 'email', align: 'left' },
  { name: 'phoneNumber', label: 'Phone', field: (r) => r.phoneNumber || '—', align: 'left' },
  { name: 'location', label: 'Location', field: (r) => locationOf(r), align: 'left' },
  { name: 'orderCount', label: 'Orders', field: 'orderCount', align: 'center' },
  { name: 'totalSpent', label: 'Total spend', field: 'totalSpent', align: 'right' },
  { name: 'lastOrderUtc', label: 'Last order', field: 'lastOrderUtc', align: 'left' }
]
const orderColumns = [
  { name: 'orderNumber', label: 'Order #', field: 'orderNumber', align: 'left' },
  { name: 'placedOnUtc', label: 'Placed', field: (r) => formatDate(r.placedOnUtc), align: 'left' },
  { name: 'status', label: 'Status', field: 'status', align: 'left' },
  { name: 'paymentStatus', label: 'Payment', field: 'paymentStatus', align: 'left' },
  { name: 'totalAmount', label: 'Total', field: (r) => formatMoney(r.totalAmount), align: 'right' }
]

const rows = ref([])
const loading = ref(false)
const search = ref('')
const pagination = ref({ page: 1, rowsPerPage: 20, rowsNumber: 0 })

const ordersOpen = ref(false)
const selected = ref(null)
const orders = ref([])
const loadingOrders = ref(false)

function nameOf (r) {
  return r ? [r.firstName, r.lastName].filter(Boolean).join(' ') : ''
}

function locationOf (r) {
  return [r.city, r.stateProvince, r.countryCode].filter(Boolean).join(', ')
}

async function fetch (props) {
  const p = props?.pagination || pagination.value
  loading.value = true
  try {
    const r = await customerAdminApi.listGuests({ page: p.page, pageSize: p.rowsPerPage, search: search.value || undefined })
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

async function openOrders (row) {
  selected.value = row
  orders.value = []
  ordersOpen.value = true
  loadingOrders.value = true
  try {
    orders.value = (await customerAdminApi.listGuestOrders(row.email)) || []
  } catch (e) {
    notify.error(getApiErrorMessage(e))
  } finally {
    loadingOrders.value = false
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
