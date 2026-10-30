<template>
  <q-page class="app-page">
    <AppListHeader title="Reports" :breadcrumbs="[{ label: 'Home', icon: 'o_home', to: '/dashboard' }, { label: 'Reports' }]" :show-add="false" />

    <AppSection title="Store performance">
      <template #actions>
        <div class="row items-end q-gutter-sm">
          <div><AppFieldLabel label="From" /><q-input v-model="from" dense outlined type="date" style="width: 150px" /></div>
          <div><AppFieldLabel label="To" /><q-input v-model="to" dense outlined type="date" style="width: 150px" /></div>
          <q-btn color="primary" unelevated no-caps icon="o_play_arrow" label="Run" :loading="loading" @click="run" />
        </div>
      </template>

      <q-markup-table flat>
        <thead>
          <tr>
            <th class="text-left">Store</th>
            <th class="text-right">Orders received</th>
            <th class="text-right">Orders fulfilled</th>
            <th class="text-right">Revenue</th>
          </tr>
        </thead>
        <tbody>
          <tr v-if="!report">
            <td colspan="4" class="text-center text-grey-6 q-pa-lg">Choose a date range and run the report.</td>
          </tr>
          <tr v-else-if="!report.stores.length">
            <td colspan="4" class="text-center text-grey-6 q-pa-lg">No activity in this period.</td>
          </tr>
          <tr v-for="s in (report?.stores || [])" :key="s.storeId">
            <td>{{ s.storeName }}</td>
            <td class="text-right">{{ s.ordersReceived }}</td>
            <td class="text-right">{{ s.ordersFulfilled }}</td>
            <td class="text-right">{{ money(s.revenue) }}</td>
          </tr>
          <tr v-if="report && report.stores.length" class="text-weight-medium">
            <td>Total</td>
            <td class="text-right">{{ sum('ordersReceived') }}</td>
            <td class="text-right">{{ sum('ordersFulfilled') }}</td>
            <td class="text-right">{{ money(sum('revenue')) }}</td>
          </tr>
        </tbody>
      </q-markup-table>
    </AppSection>
  </q-page>
</template>

<script setup>
/* Store-level reporting (WO-119): date-range store-performance report. */
import { ref, onMounted } from 'vue'
import { getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'
import { reportApi } from 'modules/reports/api'
import AppListHeader from 'components/common/AppListHeader.vue'
import AppSection from 'components/common/AppSection.vue'
import AppFieldLabel from 'components/common/AppFieldLabel.vue'

const notify = useNotify()
const report = ref(null)
const loading = ref(false)

const today = new Date()
const monthAgo = new Date(today.getTime() - 30 * 24 * 60 * 60 * 1000)
const from = ref(monthAgo.toISOString().slice(0, 10))
const to = ref(today.toISOString().slice(0, 10))

function money (v) { return v == null ? '—' : Number(v).toFixed(2) }
function sum (field) { return (report.value?.stores || []).reduce((n, s) => n + (Number(s[field]) || 0), 0) }

async function run () {
  loading.value = true
  try {
    report.value = await reportApi.storePerformance({ from: from.value, to: to.value })
    if (!report.value?.stores) report.value = { stores: [] }
  } catch (err) {
    notify.error(getApiErrorMessage(err))
    report.value = { stores: [] }
  } finally {
    loading.value = false
  }
}

onMounted(run)
</script>
