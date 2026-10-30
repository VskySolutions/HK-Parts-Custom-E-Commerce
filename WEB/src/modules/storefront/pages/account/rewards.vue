<template>
  <div class="q-gutter-md rewards">
    <q-inner-loading :showing="loading" />

    <template v-if="!loading">
      <!-- Empty / disabled programme -->
      <q-card v-if="unavailable" flat bordered>
        <q-card-section class="column items-center text-center q-py-xl q-gutter-sm">
          <q-icon name="o_redeem" size="48px" color="grey-5" />
          <div class="text-subtitle1 text-weight-medium">No rewards yet</div>
          <div class="text-body2 text-grey-6" style="max-width: 420px">
            You haven't earned any reward points yet. Points from your purchases and promotions will
            show up here.
          </div>
        </q-card-section>
      </q-card>

      <template v-else>
        <!-- Balance -->
        <q-card flat bordered>
          <q-card-section class="row items-center justify-between">
            <div>
              <div class="text-caption text-grey-6 text-uppercase">Points balance</div>
              <div class="text-h4 text-weight-bold text-primary">{{ formatPoints(balance) }}</div>
              <div class="text-caption text-grey-6">available reward points</div>
            </div>
            <q-icon name="o_loyalty" size="52px" color="primary" class="balance-icon" />
          </q-card-section>
        </q-card>

        <!-- History -->
        <q-card flat bordered>
          <q-card-section class="text-subtitle1 text-weight-medium">Recent activity</q-card-section>
          <q-separator />
          <q-table
            flat
            :rows="transactions"
            :columns="columns"
            row-key="__idx"
            hide-pagination
            :pagination="{ rowsPerPage: 0 }"
            no-data-label="No points activity yet."
          >
            <template #body-cell-createdOnUtc="props">
              <q-td :props="props">{{ formatDateTime(props.value) }}</q-td>
            </template>
            <template #body-cell-type="props">
              <q-td :props="props">
                <q-badge outline :color="props.value ? 'primary' : 'grey'" :label="humanize(props.value)" />
              </q-td>
            </template>
            <template #body-cell-points="props">
              <q-td
                :props="props"
                :class="props.value >= 0 ? 'text-positive text-weight-medium' : 'text-negative text-weight-medium'"
              >
                {{ signedPoints(props.value) }}
              </q-td>
            </template>
            <template #body-cell-balanceAfter="props">
              <q-td :props="props">{{ props.value == null ? '—' : formatPoints(props.value) }}</q-td>
            </template>
          </q-table>
        </q-card>
      </template>
    </template>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import { useQuasar } from 'quasar'
import { accountApi } from 'modules/storefront/account-api'
import { getApiErrorMessage } from 'services/api'
import { formatDateTime } from 'src/utils/datetime'

const $q = useQuasar()

const loading = ref(false)
const unavailable = ref(false)
const balance = ref(0)
const transactions = ref([])

const columns = [
  { name: 'createdOnUtc', label: 'Date', field: 'createdOnUtc', align: 'left' },
  { name: 'reason', label: 'Description', field: 'reason', align: 'left' },
  { name: 'type', label: 'Type', field: 'type', align: 'left' },
  { name: 'points', label: 'Points', field: 'points', align: 'right' },
  { name: 'balanceAfter', label: 'Balance', field: 'balanceAfter', align: 'right' }
]

function formatPoints (v) {
  const n = Number(v)
  if (Number.isNaN(n)) return '0'
  return new Intl.NumberFormat().format(n)
}

// Prefix earned points with a "+"; redemptions already carry their own "-".
function signedPoints (v) {
  const n = Number(v) || 0
  return (n >= 0 ? '+' : '') + formatPoints(n)
}

function humanize (s) {
  if (!s) return '—'
  return String(s).replace(/([a-z0-9])([A-Z])/g, '$1 $2')
}

async function load () {
  loading.value = true
  try {
    const data = await accountApi.points()
    // Adapt to the actual shape: balance may be balance/pointsBalance/points; history under
    // transactions/history/items. A disabled or empty programme falls through to the empty state.
    if (!data) {
      unavailable.value = true
    } else {
      balance.value = Number(data.balance ?? data.pointsBalance ?? data.points ?? 0) || 0
      const list = data.transactions ?? data.history ?? data.items ?? []
      transactions.value = (Array.isArray(list) ? list : []).map((t, i) => ({ ...t, __idx: i }))
      unavailable.value = balance.value === 0 && transactions.value.length === 0
    }
  } catch (e) {
    // A missing/disabled programme (404/501) reads as a friendly empty state, not a red error.
    const status = e && e.response ? e.response.status : null
    unavailable.value = true
    if (status !== 404 && status !== 501) {
      $q.notify({ type: 'negative', message: getApiErrorMessage(e) })
    }
  } finally {
    loading.value = false
  }
}

onMounted(load)
</script>

<style scoped lang="scss">
.rewards {
  position: relative;
  min-height: 160px;
}
.balance-icon {
  opacity: 0.85;
}
</style>
