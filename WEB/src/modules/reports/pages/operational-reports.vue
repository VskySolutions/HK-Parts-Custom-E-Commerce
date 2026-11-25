<template>
  <q-page class="app-page">
    <AppListHeader
      title="Reports"
      :breadcrumbs="[{ label: 'Home', icon: 'o_home', to: '/dashboard' }, { label: 'Reports' }]"
    />

    <q-card flat bordered class="app-section">
      <q-tabs
        v-model="tab"
        no-caps
        dense
        align="left"
        active-color="primary"
        indicator-color="primary"
        class="text-grey-8"
      >
        <q-tab name="best-sellers" icon="o_trending_up" label="Best Sellers" />
        <q-tab name="low-stock" icon="o_inventory_2" label="Low Stock" />
        <q-tab name="customers" icon="o_groups" label="Customers" />
        <q-tab name="sales-by-state" icon="o_map" label="Sales by State" />
        <q-tab name="taxes" icon="o_receipt" label="Taxes" />
        <q-tab name="abandoned-carts" icon="o_remove_shopping_cart" label="Abandoned Carts" />
      </q-tabs>
      <q-separator />

      <q-tab-panels v-model="tab" animated>
        <!-- ============ Best Sellers ============ -->
        <q-tab-panel name="best-sellers" class="q-pa-md">
          <div class="row items-center q-gutter-sm q-mb-md">
            <AppSelect
              v-model="bestPeriod"
              label=""
              :options="periodOptions"
              hide-bottom-space
              style="min-width: 150px"
              @update:model-value="onBestPeriod"
            />
            <template v-if="bestPeriod === 'custom'">
              <AppDateField v-model="bestFrom" label="" placeholder="From" style="min-width: 140px" @update:model-value="onBestCustom" />
              <AppDateField v-model="bestTo" label="" placeholder="To" style="min-width: 140px" @update:model-value="onBestCustom" />
            </template>
            <q-space />
            <q-btn outline color="primary" no-caps icon="o_download" label="Export CSV" :loading="bestExporting" :disable="bestLoading" @click="exportBest" />
          </div>

          <AppDataTable
            page-key="report-best-sellers"
            row-key="productId"
            title="Best-selling products"
            :rows="bestRows"
            :columns="bestColumns"
            :loading="bestLoading"
            :pagination="bestPagination"
            :column-tools="false"
            no-data-label="No sales in this period."
            @refresh="loadBest"
          >
            <template #body-cell-revenue="cell">
              <q-td :props="cell" class="text-right">{{ money(cell.row.revenue) }}</q-td>
            </template>
          </AppDataTable>
        </q-tab-panel>

        <!-- ============ Low Stock ============ -->
        <q-tab-panel name="low-stock" class="q-pa-md">
          <div class="row items-center q-mb-md">
            <div class="text-caption text-grey-7">
              Products at or below their low-stock threshold, per store.
            </div>
            <q-space />
            <q-btn outline color="primary" no-caps icon="o_download" label="Export CSV" :loading="lowExporting" :disable="lowLoading" @click="exportLow" />
          </div>

          <AppDataTable
            page-key="report-low-stock"
            row-key="_key"
            title="Low stock"
            :rows="lowRows"
            :columns="lowColumns"
            :loading="lowLoading"
            :pagination="lowPagination"
            :column-tools="false"
            no-data-label="No products are low on stock."
            @refresh="loadLow"
          >
            <template #body-cell-variantSku="cell">
              <q-td :props="cell">{{ cell.row.variantSku || '—' }}</q-td>
            </template>
            <template #body-cell-stockQuantity="cell">
              <q-td :props="cell" class="text-right">
                <q-badge :color="cell.row.stockQuantity <= 0 ? 'negative' : 'orange'" :label="cell.row.stockQuantity" />
              </q-td>
            </template>
            <template #body-cell-lowStockThreshold="cell">
              <q-td :props="cell" class="text-right text-grey-8">{{ cell.row.lowStockThreshold ?? '—' }}</q-td>
            </template>
          </AppDataTable>
        </q-tab-panel>

        <!-- ============ Customers ============ -->
        <q-tab-panel name="customers" class="q-pa-md">
          <div class="row items-center q-gutter-sm q-mb-md">
            <AppSelect
              v-model="custPeriod"
              label=""
              :options="periodOptions"
              hide-bottom-space
              style="min-width: 150px"
              @update:model-value="onCustPeriod"
            />
            <template v-if="custPeriod === 'custom'">
              <AppDateField v-model="custFrom" label="" placeholder="From" style="min-width: 140px" @update:model-value="onCustCustom" />
              <AppDateField v-model="custTo" label="" placeholder="To" style="min-width: 140px" @update:model-value="onCustCustom" />
            </template>
            <q-space />
            <q-btn outline color="primary" no-caps icon="o_download" label="Export CSV" :loading="custExporting" :disable="custLoading" @click="exportCust" />
          </div>

          <div class="row q-col-gutter-md q-mb-md">
            <div class="col-12 col-sm-6">
              <q-card flat bordered class="stat-tile">
                <div class="stat-tile__label">New registrations</div>
                <div class="stat-tile__value">
                  <q-skeleton v-if="custLoading" type="text" width="70px" />
                  <template v-else>{{ customers ? customers.newRegistrations : '—' }}</template>
                </div>
              </q-card>
            </div>
            <div class="col-12 col-sm-6">
              <q-card flat bordered class="stat-tile">
                <div class="stat-tile__label">Total active customers</div>
                <div class="stat-tile__value">
                  <q-skeleton v-if="custLoading" type="text" width="70px" />
                  <template v-else>{{ customers ? customers.totalActiveCustomers : '—' }}</template>
                </div>
              </q-card>
            </div>
          </div>

          <AppDataTable
            page-key="report-top-customers"
            row-key="email"
            title="Top customers"
            :rows="topCustomers"
            :columns="custColumns"
            :loading="custLoading"
            :pagination="custPagination"
            :column-tools="false"
            no-data-label="No customer activity in this period."
            @refresh="loadCustomers"
          >
            <template #body-cell-totalSpent="cell">
              <q-td :props="cell" class="text-right">{{ money(cell.row.totalSpent) }}</q-td>
            </template>
          </AppDataTable>
        </q-tab-panel>

        <!-- ============ Sales by State (guest checkout A-40/A-41) ============ -->
        <q-tab-panel name="sales-by-state" class="q-pa-md">
          <div class="row items-center q-gutter-sm q-mb-md">
            <AppSelect v-model="statePeriod" label="" :options="periodOptions" hide-bottom-space style="min-width: 150px" @update:model-value="onStatePeriod" />
            <template v-if="statePeriod === 'custom'">
              <AppDateField v-model="stateFrom" label="" placeholder="From" style="min-width: 140px" @update:model-value="onStateCustom" />
              <AppDateField v-model="stateTo" label="" placeholder="To" style="min-width: 140px" @update:model-value="onStateCustom" />
            </template>
            <AppSelect v-model="stateCustomerType" label="" :options="customerTypeOptions" hide-bottom-space style="min-width: 150px" aria-label="Customer type" @update:model-value="loadStates" />
            <q-space />
            <q-btn outline color="primary" no-caps icon="o_download" label="Export CSV" :loading="stateExporting" :disable="stateLoading" @click="exportStates" />
          </div>
          <div class="text-caption text-grey-7 q-mb-sm">
            Captured, non-cancelled orders by ship-to state. Guest orders are those placed through guest checkout,
            including ones later moved to an account.
          </div>

          <AppDataTable
            page-key="report-sales-by-state"
            row-key="_key"
            title="Sales by state"
            :rows="stateRows"
            :columns="stateColumns"
            :loading="stateLoading"
            :pagination="statePagination"
            :column-tools="false"
            no-data-label="No sales in this period."
            @refresh="loadStates"
          >
            <template #body-cell-revenue="cell"><q-td :props="cell" class="text-right">{{ money(cell.row.revenue) }}</q-td></template>
            <template #body-cell-guestRevenue="cell"><q-td :props="cell" class="text-right">{{ money(cell.row.guestRevenue) }}</q-td></template>
            <template #body-cell-registeredRevenue="cell"><q-td :props="cell" class="text-right">{{ money(cell.row.registeredRevenue) }}</q-td></template>
          </AppDataTable>
        </q-tab-panel>

        <!-- ============ Taxes (PHP "Tax Report") ============ -->
        <q-tab-panel name="taxes" class="q-pa-md">
          <div class="row items-center q-gutter-sm q-mb-md">
            <AppSelect v-model="taxPeriod" label="" :options="periodOptions" hide-bottom-space style="min-width: 150px" @update:model-value="onTaxPeriod" />
            <template v-if="taxPeriod === 'custom'">
              <AppDateField v-model="taxFrom" label="" placeholder="From" style="min-width: 140px" @update:model-value="onTaxCustom" />
              <AppDateField v-model="taxTo" label="" placeholder="To" style="min-width: 140px" @update:model-value="onTaxCustom" />
            </template>
            <q-space />
            <q-btn outline color="primary" no-caps icon="o_download" label="Export CSV" :loading="taxExporting" :disable="taxLoading" @click="exportTaxes" />
          </div>

          <div class="row q-col-gutter-md q-mb-md">
            <div class="col-12 col-sm-4">
              <q-card flat bordered class="stat-tile">
                <div class="stat-tile__label">Tax collected</div>
                <div class="stat-tile__value">
                  <q-skeleton v-if="taxLoading" type="text" width="90px" />
                  <template v-else>{{ taxReport ? money(taxReport.totalTax) : '—' }}</template>
                </div>
                <div v-if="taxReport?.totalTaxRefunded > 0" class="text-caption text-grey-7">after {{ money(taxReport.totalTaxRefunded) }} refunded</div>
              </q-card>
            </div>
            <div class="col-12 col-sm-4">
              <q-card flat bordered class="stat-tile">
                <div class="stat-tile__label">Orders (taxed)</div>
                <div class="stat-tile__value">
                  <q-skeleton v-if="taxLoading" type="text" width="70px" />
                  <template v-else>{{ taxReport ? `${taxReport.orderCount} (${taxReport.taxedOrderCount})` : '—' }}</template>
                </div>
              </q-card>
            </div>
            <div class="col-12 col-sm-4">
              <q-card flat bordered class="stat-tile">
                <div class="stat-tile__label">Sales before tax</div>
                <div class="stat-tile__value">
                  <q-skeleton v-if="taxLoading" type="text" width="90px" />
                  <template v-else>{{ taxReport ? money(taxReport.totalSales) : '—' }}</template>
                </div>
              </q-card>
            </div>
          </div>
          <div class="text-caption text-grey-7 q-mb-sm">
            Tax on orders placed in the period, every status except cancelled (as the old Tax Report), by ship-to state. Tax handed back by refunds and returns is taken off.
          </div>

          <AppDataTable
            page-key="report-taxes"
            row-key="_key"
            title="Tax by state"
            :rows="taxRows"
            :columns="taxColumns"
            :loading="taxLoading"
            :pagination="taxPagination"
            :column-tools="false"
            no-data-label="No orders in this period."
            @refresh="loadTaxes"
          >
            <template #body-cell-sales="cell"><q-td :props="cell" class="text-right">{{ money(cell.row.sales) }}</q-td></template>
            <template #body-cell-taxRefunded="cell"><q-td :props="cell" class="text-right">{{ cell.row.taxRefunded > 0 ? money(cell.row.taxRefunded) : '—' }}</q-td></template>
            <template #body-cell-tax="cell"><q-td :props="cell" class="text-right">{{ money(cell.row.tax) }}</q-td></template>
          </AppDataTable>
        </q-tab-panel>

        <!-- ============ Abandoned Carts (guest checkout A-40/A-43) ============ -->
        <q-tab-panel name="abandoned-carts" class="q-pa-md">
          <div class="row items-center q-gutter-sm q-mb-md">
            <AppSelect v-model="cartIdle" label="" :options="idleOptions" hide-bottom-space style="min-width: 170px" aria-label="Idle for" @update:model-value="reloadCarts" />
            <AppSelect v-model="cartCustomerType" label="" :options="customerTypeOptions" hide-bottom-space style="min-width: 150px" aria-label="Customer type" @update:model-value="reloadCarts" />
            <q-input v-model="cartSearch" dense outlined debounce="400" placeholder="Search email or name" style="min-width: 220px" @update:model-value="reloadCarts">
              <template #prepend><q-icon name="o_search" /></template>
            </q-input>
            <q-checkbox v-model="cartOnlyWithEmail" dense label="Only with an email" @update:model-value="reloadCarts" />
            <q-space />
            <q-btn outline color="primary" no-caps icon="o_download" label="Export CSV" :loading="cartExporting" :disable="cartLoading" @click="exportCarts" />
          </div>
          <div class="text-caption text-grey-7 q-mb-sm">
            Open carts with items and no activity. A guest's email shows once they typed it at checkout. Recovery emails go
            only to guests who ticked "Email me news and offers".
          </div>

          <AppDataTable
            page-key="report-abandoned-carts"
            row-key="cartId"
            title="Abandoned carts"
            :rows="cartRows"
            :columns="cartColumns"
            :loading="cartLoading"
            :pagination="cartPagination"
            :column-tools="false"
            no-data-label="No abandoned carts."
            @request="onCartRequest"
            @refresh="reloadCarts"
          >
            <template #body-cell-customer="cell">
              <q-td :props="cell">
                <div class="row items-center no-wrap">
                  <q-badge :color="cell.row.isGuest ? 'grey-7' : 'indigo'" outline :label="cell.row.isGuest ? 'Guest' : 'Registered'" class="q-mr-sm" />
                  <span>{{ (cell.row.name || '').trim() || '—' }}</span>
                </div>
              </q-td>
            </template>
            <template #body-cell-email="cell">
              <q-td :props="cell">
                <a v-if="cell.row.email" :href="`mailto:${cell.row.email}`">{{ cell.row.email }}</a>
                <span v-else class="text-grey-6">Not captured</span>
                <q-badge v-if="cell.row.isGuest && cell.row.marketingOptIn" color="positive" outline label="Opted in" class="q-ml-xs" />
              </q-td>
            </template>
            <template #body-cell-subtotal="cell"><q-td :props="cell" class="text-right">{{ money(cell.row.subtotal, cell.row.currencyCode) }}</q-td></template>
            <template #body-cell-lastActivityUtc="cell"><q-td :props="cell">{{ $datetime(cell.row.lastActivityUtc) }}</q-td></template>
            <template #body-cell-recoveryEmailSentUtc="cell">
              <q-td :props="cell">{{ cell.row.recoveryEmailSentUtc ? $datetime(cell.row.recoveryEmailSentUtc) : '—' }}</q-td>
            </template>
          </AppDataTable>
        </q-tab-panel>
      </q-tab-panels>
    </q-card>
  </q-page>
</template>

<script setup>
/*
 * Operational Reports (WO-60): report views under one page (q-tabs) — Best Sellers
 * (period-scoped, CSV export), Low Stock (CSV export) and Customers (new/active KPIs +
 * top-customers table, CSV export). Each table is client-sorted/paginated; each Export button
 * downloads the matching `.csv` blob. Guest checkout adds Sales by State (guest / registered split, A-41) and
 * Abandoned Carts (guest email when captured, A-43). Reads /api/admin/reports/{best-sellers,low-stock,customers,
 * sales-by-state,abandoned-carts}.
 */
import { ref, computed, onMounted } from 'vue'
import { getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'
import { operationalReportsApi, periodOptions, periodParams, money, saveBlob, customerTypeOptions } from 'modules/reports/operationalApi'

const notify = useNotify()
const tab = ref('best-sellers')
const asArray = (r) => (Array.isArray(r) ? r : r?.items || [])

// ---- Best sellers -----------------------------------------------------------
const bestPeriod = ref('last30')
const bestFrom = ref('')
const bestTo = ref('')
const bestRows = ref([])
const bestLoading = ref(false)
const bestExporting = ref(false)
const bestPagination = ref({ sortBy: 'unitsSold', descending: true, page: 1, rowsPerPage: 10 })
const bestColumns = [
  { name: 'productName', label: 'Product', field: 'productName', align: 'left', sortable: true },
  { name: 'unitsSold', label: 'Units sold', field: 'unitsSold', align: 'right', sortable: true },
  { name: 'revenue', label: 'Revenue', field: 'revenue', align: 'right', sortable: true }
]

function bestParams () { return { ...periodParams(bestPeriod.value, bestFrom.value, bestTo.value), take: 50 } }
async function loadBest () {
  if (bestPeriod.value === 'custom' && (!bestFrom.value || !bestTo.value)) return
  bestLoading.value = true
  try {
    bestRows.value = asArray(await operationalReportsApi.bestSellers(bestParams()))
  } catch (e) { notify.error(getApiErrorMessage(e)) } finally { bestLoading.value = false }
}
async function exportBest () {
  bestExporting.value = true
  try {
    const res = await operationalReportsApi.bestSellersCsv(bestParams())
    saveBlob(res.data, 'best-sellers.csv')
    notify.success('Export ready')
  } catch (e) { notify.error(getApiErrorMessage(e)) } finally { bestExporting.value = false }
}
function onBestPeriod () { if (bestPeriod.value !== 'custom') loadBest() }
function onBestCustom () { if (bestFrom.value && bestTo.value) loadBest() }

// ---- Low stock --------------------------------------------------------------
const lowRows = ref([])
const lowLoading = ref(false)
const lowExporting = ref(false)
const lowPagination = ref({ sortBy: 'stockQuantity', descending: false, page: 1, rowsPerPage: 10 })
const lowColumns = [
  { name: 'productName', label: 'Product', field: 'productName', align: 'left', sortable: true },
  { name: 'variantSku', label: 'Variant SKU', field: 'variantSku', align: 'left', sortable: true },
  { name: 'storeName', label: 'Store', field: 'storeName', align: 'left', sortable: true },
  { name: 'stockQuantity', label: 'In stock', field: 'stockQuantity', align: 'right', sortable: true },
  { name: 'lowStockThreshold', label: 'Threshold', field: 'lowStockThreshold', align: 'right', sortable: true }
]

async function loadLow () {
  lowLoading.value = true
  try {
    // Synthesise a stable row key (low-stock rows carry no id).
    lowRows.value = asArray(await operationalReportsApi.lowStock()).map((x, i) => ({
      ...x,
      _key: `${x.productName}|${x.variantSku || ''}|${x.storeName || ''}|${i}`
    }))
  } catch (e) { notify.error(getApiErrorMessage(e)) } finally { lowLoading.value = false }
}
async function exportLow () {
  lowExporting.value = true
  try {
    const res = await operationalReportsApi.lowStockCsv()
    saveBlob(res.data, 'low-stock.csv')
    notify.success('Export ready')
  } catch (e) { notify.error(getApiErrorMessage(e)) } finally { lowExporting.value = false }
}

// ---- Customers --------------------------------------------------------------
const custPeriod = ref('last30')
const custFrom = ref('')
const custTo = ref('')
const customers = ref(null)
const custLoading = ref(false)
const custExporting = ref(false)
const custPagination = ref({ sortBy: 'totalSpent', descending: true, page: 1, rowsPerPage: 10 })
const topCustomers = computed(() => customers.value?.topCustomers || [])
const custColumns = [
  { name: 'customerName', label: 'Customer', field: 'customerName', align: 'left', sortable: true },
  { name: 'email', label: 'Email', field: 'email', align: 'left', sortable: true },
  { name: 'orderCount', label: 'Orders', field: 'orderCount', align: 'right', sortable: true },
  { name: 'totalSpent', label: 'Total spent', field: 'totalSpent', align: 'right', sortable: true }
]

function custParams () { return periodParams(custPeriod.value, custFrom.value, custTo.value) }
async function loadCustomers () {
  if (custPeriod.value === 'custom' && (!custFrom.value || !custTo.value)) return
  custLoading.value = true
  try {
    customers.value = await operationalReportsApi.customers(custParams())
  } catch (e) { notify.error(getApiErrorMessage(e)) } finally { custLoading.value = false }
}
async function exportCust () {
  custExporting.value = true
  try {
    const res = await operationalReportsApi.customersCsv(custParams())
    saveBlob(res.data, 'customers.csv')
    notify.success('Export ready')
  } catch (e) { notify.error(getApiErrorMessage(e)) } finally { custExporting.value = false }
}
function onCustPeriod () { if (custPeriod.value !== 'custom') loadCustomers() }
function onCustCustom () { if (custFrom.value && custTo.value) loadCustomers() }

// ---- Sales by State (guest checkout A-40/A-41) -------------------------------
const statePeriod = ref('last30')
const stateFrom = ref('')
const stateTo = ref('')
const stateCustomerType = ref('All')
const stateRows = ref([])
const stateLoading = ref(false)
const stateExporting = ref(false)
const statePagination = ref({ sortBy: 'revenue', descending: true, page: 1, rowsPerPage: 25 })
const stateColumns = [
  { name: 'state', label: 'State', field: 'state', align: 'left', sortable: true },
  { name: 'countryCode', label: 'Country', field: (r) => r.countryCode || '—', align: 'left', sortable: true },
  { name: 'orders', label: 'Orders', field: 'orders', align: 'right', sortable: true },
  { name: 'revenue', label: 'Revenue', field: 'revenue', align: 'right', sortable: true },
  { name: 'guestOrders', label: 'Guest orders', field: 'guestOrders', align: 'right', sortable: true },
  { name: 'guestRevenue', label: 'Guest revenue', field: 'guestRevenue', align: 'right', sortable: true },
  { name: 'registeredOrders', label: 'Registered orders', field: 'registeredOrders', align: 'right', sortable: true },
  { name: 'registeredRevenue', label: 'Registered revenue', field: 'registeredRevenue', align: 'right', sortable: true }
]
function stateParams () {
  return {
    ...periodParams(statePeriod.value, stateFrom.value, stateTo.value),
    customerType: stateCustomerType.value !== 'All' ? stateCustomerType.value : undefined
  }
}
async function loadStates () {
  if (statePeriod.value === 'custom' && (!stateFrom.value || !stateTo.value)) return
  stateLoading.value = true
  try {
    stateRows.value = asArray(await operationalReportsApi.salesByState(stateParams()))
      .map((r) => ({ ...r, _key: `${r.state}|${r.countryCode || ''}` }))
  } catch (e) { notify.error(getApiErrorMessage(e)) } finally { stateLoading.value = false }
}
async function exportStates () {
  stateExporting.value = true
  try {
    const res = await operationalReportsApi.salesByStateCsv(stateParams())
    saveBlob(res.data, 'sales-by-state.csv')
    notify.success('Export ready')
  } catch (e) { notify.error(getApiErrorMessage(e)) } finally { stateExporting.value = false }
}
function onStatePeriod () { if (statePeriod.value !== 'custom') loadStates() }
function onStateCustom () { if (stateFrom.value && stateTo.value) loadStates() }

// ---- Taxes (PHP "Tax Report") --------------------------------------------------
const taxPeriod = ref('last30')
const taxFrom = ref('')
const taxTo = ref('')
const taxReport = ref(null)
const taxRows = ref([])
const taxLoading = ref(false)
const taxExporting = ref(false)
const taxPagination = ref({ sortBy: 'tax', descending: true, page: 1, rowsPerPage: 25 })
const taxColumns = [
  { name: 'state', label: 'State', field: 'state', align: 'left', sortable: true },
  { name: 'countryCode', label: 'Country', field: (r) => r.countryCode || '—', align: 'left', sortable: true },
  { name: 'orders', label: 'Orders', field: 'orders', align: 'right', sortable: true },
  { name: 'taxedOrders', label: 'Taxed orders', field: 'taxedOrders', align: 'right', sortable: true },
  { name: 'sales', label: 'Sales (pre-tax)', field: 'sales', align: 'right', sortable: true },
  { name: 'taxRefunded', label: 'Tax refunded', field: 'taxRefunded', align: 'right', sortable: true },
  { name: 'tax', label: 'Net tax collected', field: 'tax', align: 'right', sortable: true }
]
function taxParams () { return periodParams(taxPeriod.value, taxFrom.value, taxTo.value) }
async function loadTaxes () {
  if (taxPeriod.value === 'custom' && (!taxFrom.value || !taxTo.value)) return
  taxLoading.value = true
  try {
    taxReport.value = await operationalReportsApi.taxes(taxParams())
    taxRows.value = (taxReport.value?.rows || []).map((r) => ({ ...r, _key: `${r.state}|${r.countryCode || ''}` }))
  } catch (e) { notify.error(getApiErrorMessage(e)) } finally { taxLoading.value = false }
}
async function exportTaxes () {
  taxExporting.value = true
  try {
    const res = await operationalReportsApi.taxesCsv(taxParams())
    saveBlob(res.data, 'taxes.csv')
    notify.success('Export ready')
  } catch (e) { notify.error(getApiErrorMessage(e)) } finally { taxExporting.value = false }
}
function onTaxPeriod () { if (taxPeriod.value !== 'custom') loadTaxes() }
function onTaxCustom () { if (taxFrom.value && taxTo.value) loadTaxes() }

// ---- Abandoned carts (guest checkout A-40/A-43) ------------------------------
const idleOptions = [
  { label: 'Idle 1 hour or more', value: 1 },
  { label: 'Idle 24 hours or more', value: 24 },
  { label: 'Idle 7 days or more', value: 168 },
  { label: 'Idle 30 days or more', value: 720 }
]
const cartIdle = ref(1)
const cartCustomerType = ref('All')
const cartSearch = ref('')
const cartOnlyWithEmail = ref(false)
const cartRows = ref([])
const cartLoading = ref(false)
const cartExporting = ref(false)
const cartPagination = ref({ page: 1, rowsPerPage: 25, rowsNumber: 0 })
const cartColumns = [
  { name: 'customer', label: 'Customer', field: 'name', align: 'left' },
  { name: 'email', label: 'Email', field: 'email', align: 'left' },
  { name: 'itemCount', label: 'Items', field: 'itemCount', align: 'right' },
  { name: 'subtotal', label: 'Subtotal', field: 'subtotal', align: 'right' },
  { name: 'reachedCheckout', label: 'Reached checkout', field: (r) => (r.reachedCheckout ? 'Yes' : 'No'), align: 'center' },
  { name: 'lastActivityUtc', label: 'Last activity', field: 'lastActivityUtc', align: 'left' },
  { name: 'recoveryEmailSentUtc', label: 'Recovery email', field: 'recoveryEmailSentUtc', align: 'left' }
]
function cartParams () {
  return {
    idleHours: cartIdle.value,
    customerType: cartCustomerType.value !== 'All' ? cartCustomerType.value : undefined,
    search: cartSearch.value || undefined,
    onlyWithEmail: cartOnlyWithEmail.value || undefined
  }
}
async function loadCarts (props) {
  const p = props?.pagination || cartPagination.value
  cartLoading.value = true
  try {
    const r = await operationalReportsApi.abandonedCarts({ page: p.page, pageSize: p.rowsPerPage, ...cartParams() })
    cartRows.value = asArray(r)
    cartPagination.value = { ...p, rowsNumber: r?.totalCount ?? cartRows.value.length }
  } catch (e) { notify.error(getApiErrorMessage(e)) } finally { cartLoading.value = false }
}
function onCartRequest (props) { loadCarts(props) }
function reloadCarts () { loadCarts({ pagination: { ...cartPagination.value, page: 1 } }) }
async function exportCarts () {
  cartExporting.value = true
  try {
    const res = await operationalReportsApi.abandonedCartsCsv(cartParams())
    saveBlob(res.data, 'abandoned-carts.csv')
    notify.success('Export ready')
  } catch (e) { notify.error(getApiErrorMessage(e)) } finally { cartExporting.value = false }
}

onMounted(() => {
  loadBest()
  loadLow()
  loadCustomers()
  loadStates()
  loadTaxes()
  loadCarts()
})
</script>

<style scoped lang="scss">
.stat-tile {
  border-radius: 10px;
  padding: 14px 16px;
}
.stat-tile__label {
  font-size: 12px;
  font-weight: 500;
  color: #6b6c76;
  text-transform: uppercase;
  letter-spacing: 0.3px;
}
.stat-tile__value {
  font-size: 24px;
  font-weight: 700;
  color: #1d1d1f;
  margin-top: 6px;
}
</style>
