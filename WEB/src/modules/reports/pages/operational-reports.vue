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
      </q-tab-panels>
    </q-card>
  </q-page>
</template>

<script setup>
/*
 * Operational Reports (WO-60): three report views under one page (q-tabs) — Best Sellers
 * (period-scoped, CSV export), Low Stock (CSV export) and Customers (new/active KPIs +
 * top-customers table, CSV export). Each table is client-sorted/paginated; each Export button
 * downloads the matching `.csv` blob. Reads /api/admin/reports/{best-sellers,low-stock,customers}.
 */
import { ref, computed, onMounted } from 'vue'
import { getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'
import { operationalReportsApi, periodOptions, periodParams, money, saveBlob } from 'modules/reports/operationalApi'

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

onMounted(() => {
  loadBest()
  loadLow()
  loadCustomers()
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
