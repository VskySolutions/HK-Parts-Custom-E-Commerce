<template>
  <q-page class="app-page">
    <AppListHeader
      title="Analytics"
      :breadcrumbs="[{ label: 'Home', icon: 'o_home', to: '/dashboard' }, { label: 'Analytics' }]"
    >
      <template #actions>
        <div class="row items-center q-gutter-sm no-wrap">
          <AppSelect
            v-model="period"
            label=""
            :options="periodOptions"
            hide-bottom-space
            style="min-width: 150px"
            @update:model-value="onPeriodChange"
          />
          <AppSelect
            v-model="customerType"
            label=""
            :options="customerTypeOptions"
            hide-bottom-space
            style="min-width: 150px"
            aria-label="Customer type"
            @update:model-value="refetch"
          />
          <template v-if="period === 'custom'">
            <AppDateField v-model="from" label="" placeholder="From" style="min-width: 140px" @update:model-value="onCustomChange" />
            <AppDateField v-model="to" label="" placeholder="To" style="min-width: 140px" @update:model-value="onCustomChange" />
          </template>
          <q-btn flat round dense icon="o_refresh" :loading="loading" aria-label="Refresh" @click="refetch">
            <q-tooltip>Refresh</q-tooltip>
          </q-btn>
        </div>
      </template>
    </AppListHeader>

    <!-- KPI cards -->
    <div class="row q-col-gutter-md q-mb-md">
      <div class="col-12 col-sm-6 col-md-3">
        <StatCard label="Total orders" :value="summary ? summary.totalOrders : null" icon="o_receipt_long" :loading="loading" />
      </div>
      <div class="col-12 col-sm-6 col-md-3">
        <StatCard label="Total revenue" :value="summary ? money(summary.totalRevenue) : null" icon="o_payments" :loading="loading" />
      </div>
      <div class="col-12 col-sm-6 col-md-3">
        <StatCard label="Average order value" :value="summary ? money(summary.averageOrderValue) : null" icon="o_shopping_cart" :loading="loading" />
      </div>
      <div class="col-12 col-sm-6 col-md-3">
        <StatCard label="New customers" :value="summary ? summary.newCustomers : null" icon="o_person_add" :loading="loading" />
      </div>
    </div>

    <!-- Guest checkout A-42: guest share of orders and revenue in the period (always against every order). -->
    <div class="row q-col-gutter-md q-mb-md">
      <div class="col-12 col-sm-6 col-md-3">
        <StatCard
          label="Guest share of orders"
          :value="summary ? `${summary.guestOrderSharePercent}% (${summary.guestOrders})` : null"
          icon="o_person_outline"
          :loading="loading"
        />
      </div>
      <div class="col-12 col-sm-6 col-md-3">
        <StatCard
          label="Guest share of revenue"
          :value="summary ? `${summary.guestRevenueSharePercent}% (${money(summary.guestRevenue)})` : null"
          icon="o_account_balance_wallet"
          :loading="loading"
        />
      </div>
    </div>

    <!-- Sales trend chart -->
    <AppSection title="Sales trend">
      <TrendChart :data="trend" :loading="loading" />
    </AppSection>

    <!-- Recent orders -->
    <AppDataTable
      page-key="analytics-recent-orders"
      row-key="id"
      title="Recent orders"
      :rows="recentOrders"
      :columns="orderColumns"
      :loading="loading"
      :pagination="{ rowsPerPage: 0 }"
      :column-tools="false"
      hide-pagination
      no-data-label="No recent orders."
      @refresh="refetch"
    >
      <template #body-cell-orderNumber="cell">
        <q-td :props="cell"><span class="text-weight-medium">{{ cell.row.orderNumber }}</span></q-td>
      </template>
      <template #body-cell-placedOnUtc="cell">
        <q-td :props="cell">{{ $datetime(cell.row.placedOnUtc) }}</q-td>
      </template>
      <template #body-cell-status="cell">
        <q-td :props="cell"><q-badge :color="statusColor(cell.row.status)" :label="cell.row.status" /></q-td>
      </template>
      <template #body-cell-totalAmount="cell">
        <q-td :props="cell" class="text-right">{{ money(cell.row.totalAmount, cell.row.currencyCode) }}</q-td>
      </template>
    </AppDataTable>
  </q-page>
</template>

<script setup>
/*
 * Sales Analytics dashboard (WO-59). Period selector (today / 7d / 30d / custom) drives 4 KPI cards,
 * a custom inline-SVG sales-trend chart (orders + revenue), and a recent-orders table. Everything
 * refetches when the period changes. Reads /api/admin/analytics/{summary,trend,recent-orders}.
 */
import { ref, onMounted } from 'vue'
import { getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'
import { analyticsApi, periodOptions, periodParams, money, statusColor } from 'modules/analytics/api'
import StatCard from 'modules/analytics/components/StatCard.vue'
import TrendChart from 'modules/analytics/components/TrendChart.vue'

const notify = useNotify()

const period = ref('last30')
const from = ref('')
const to = ref('')

// Guest checkout A-41: narrow the orders, revenue and trend to guest-checkout or registered orders.
const customerType = ref('All')
const customerTypeOptions = [
  { label: 'All customers', value: 'All' },
  { label: 'Guest', value: 'Guest' },
  { label: 'Registered', value: 'Registered' }
]

const loading = ref(false)
const summary = ref(null)
const trend = ref([])
const recentOrders = ref([])

const orderColumns = [
  { name: 'orderNumber', label: 'Order #', field: 'orderNumber', align: 'left' },
  { name: 'placedOnUtc', label: 'Date', field: 'placedOnUtc', align: 'left' },
  { name: 'customerName', label: 'Customer', field: 'customerName', align: 'left' },
  { name: 'status', label: 'Status', field: 'status', align: 'left' },
  { name: 'totalAmount', label: 'Total', field: 'totalAmount', align: 'right' }
]

const asArray = (r) => (Array.isArray(r) ? r : r?.items || [])

async function refetch () {
  // Custom period needs both bounds before we query the range-based endpoints.
  if (period.value === 'custom' && (!from.value || !to.value)) return
  loading.value = true
  const params = {
    ...periodParams(period.value, from.value, to.value),
    customerType: customerType.value !== 'All' ? customerType.value : undefined
  }
  try {
    const [s, t, r] = await Promise.all([
      analyticsApi.summary(params),
      analyticsApi.trend(params),
      analyticsApi.recentOrders({ take: 10 })
    ])
    summary.value = s || null
    trend.value = asArray(t)
    recentOrders.value = asArray(r)
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    loading.value = false
  }
}

function onPeriodChange () {
  // Switching to custom waits for the date inputs; every other choice fetches immediately.
  if (period.value !== 'custom') refetch()
}
function onCustomChange () {
  if (from.value && to.value) refetch()
}

onMounted(refetch)
</script>
