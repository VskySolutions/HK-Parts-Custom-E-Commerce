<template>
  <q-card flat bordered>
    <q-card-section class="text-subtitle1 text-weight-medium">Subscriptions</q-card-section>
    <q-separator />

    <q-table
      flat
      :rows="rows"
      :columns="columns"
      row-key="id"
      :loading="loading"
      hide-pagination
      :pagination="{ rowsPerPage: 0 }"
      no-data-label="You don't have any subscriptions yet."
    >
      <!-- Interval doubles as the change control: picking a new frequency PUTs it. -->
      <template #body-cell-interval="props">
        <q-td :props="props">
          <q-select
            dense
            outlined
            emit-value
            map-options
            :options="intervalOptions"
            :model-value="props.row.interval"
            :disable="props.row.status === 'Cancelled' || busyId === props.row.id"
            style="min-width: 150px"
            @update:model-value="(val) => onChangeInterval(props.row, val)"
          />
        </q-td>
      </template>

      <template #body-cell-nextOrderOnUtc="props">
        <q-td :props="props">{{ props.value ? formatDate(props.value) : '—' }}</q-td>
      </template>

      <template #body-cell-status="props">
        <q-td :props="props">
          <q-badge :color="statusColor(props.value)" :label="humanize(props.value)" />
        </q-td>
      </template>

      <template #body-cell-actions="props">
        <q-td :props="props" class="text-right">
          <q-btn
            v-if="props.row.status === 'Active'"
            flat dense no-caps size="sm" color="grey-8" icon="o_pause_circle" label="Pause"
            :loading="busyId === props.row.id"
            @click="onPause(props.row)"
          />
          <q-btn
            v-else-if="props.row.status === 'Paused'"
            flat dense no-caps size="sm" color="primary" icon="o_play_circle" label="Resume"
            :loading="busyId === props.row.id"
            @click="onResume(props.row)"
          />
          <q-btn
            v-if="props.row.status !== 'Cancelled'"
            flat dense no-caps size="sm" color="negative" icon="o_cancel" label="Cancel"
            :disable="busyId === props.row.id"
            @click="confirmCancel(props.row)"
          />
        </q-td>
      </template>
    </q-table>
  </q-card>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import { useQuasar } from 'quasar'
import { accountApi } from 'modules/storefront/account-api'
import { getApiErrorMessage } from 'services/api'
import { formatDate } from 'src/utils/datetime'

const $q = useQuasar()

const rows = ref([])
const loading = ref(false)
const busyId = ref(null) // the row currently mid-action (disables its controls)

const columns = [
  { name: 'productName', label: 'Product', field: 'productName', align: 'left' },
  { name: 'interval', label: 'Frequency', field: 'interval', align: 'left' },
  { name: 'nextOrderOnUtc', label: 'Next order', field: 'nextOrderOnUtc', align: 'left' },
  { name: 'quantity', label: 'Qty', field: 'quantity', align: 'center' },
  { name: 'status', label: 'Status', field: 'status', align: 'left' },
  { name: 'actions', label: '', field: 'id', align: 'right' }
]

const intervalOptions = [
  { label: 'Weekly', value: 'Weekly' },
  { label: 'Every 2 weeks', value: 'BiWeekly' },
  { label: 'Monthly', value: 'Monthly' },
  { label: 'Quarterly', value: 'Quarterly' }
]

// Split an enum name into words (BiWeekly → "Bi Weekly"); single-word values pass through.
function humanize (s) {
  if (!s) return '—'
  return String(s).replace(/([a-z0-9])([A-Z])/g, '$1 $2')
}

function statusColor (status) {
  switch (status) {
    case 'Active': return 'positive'
    case 'Paused': return 'orange'
    default: return 'grey' // Cancelled + anything unexpected
  }
}

async function load () {
  loading.value = true
  try {
    const data = await accountApi.subscriptions()
    rows.value = Array.isArray(data) ? data : (Array.isArray(data?.items) ? data.items : [])
  } catch (e) {
    $q.notify({ type: 'negative', message: getApiErrorMessage(e) })
  } finally {
    loading.value = false
  }
}

// Run a per-row mutation, then refresh so statuses/next-order dates reflect the change.
async function runAction (row, fn, successMsg) {
  busyId.value = row.id
  try {
    await fn()
    if (successMsg) $q.notify({ type: 'positive', message: successMsg })
    await load()
  } catch (e) {
    $q.notify({ type: 'negative', message: getApiErrorMessage(e) })
  } finally {
    busyId.value = null
  }
}

function onPause (row) {
  runAction(row, () => accountApi.pauseSubscription(row.id), 'Subscription paused.')
}

function onResume (row) {
  runAction(row, () => accountApi.resumeSubscription(row.id), 'Subscription resumed.')
}

function onChangeInterval (row, interval) {
  if (!interval || interval === row.interval) return
  runAction(row, () => accountApi.changeSubscriptionInterval(row.id, interval), 'Delivery frequency updated.')
}

function confirmCancel (row) {
  $q.dialog({
    title: 'Cancel subscription',
    message: `Cancel your subscription to ${row.productName}? This can't be undone.`,
    cancel: true,
    ok: { label: 'Cancel subscription', color: 'negative', unelevated: true, noCaps: true }
  }).onOk(() => {
    runAction(row, () => accountApi.cancelSubscription(row.id), 'Subscription cancelled.')
  })
}

onMounted(load)
</script>
