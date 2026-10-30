<template>
  <q-page class="app-page">
    <AppListHeader
      title="Currencies"
      subtitle="Base currency, exchange rates and the currencies buyers can shop in."
      :breadcrumbs="[{ label: 'Home', icon: 'o_home', to: '/dashboard' }, { label: 'Currencies' }]"
    >
      <template #actions>
        <q-btn flat color="primary" icon="o_refresh" label="Reload" no-caps :loading="loading" @click="load" />
        <q-btn v-if="canWrite" unelevated color="primary" icon="o_add" label="New currency" no-caps @click="onAdd" />
      </template>
    </AppListHeader>

    <div class="row q-col-gutter-md q-mb-md">
      <!-- Base currency -->
      <div class="col-12 col-md-6">
        <q-card flat bordered class="full-height">
          <q-card-section>
            <div class="text-subtitle1">Base currency</div>
            <div class="text-caption text-grey-7">
              All exchange rates are expressed relative to this currency.
            </div>

            <div class="row items-center q-mt-md">
              <div class="col">
                <template v-if="baseCurrency">
                  <span class="text-h5 text-weight-medium">{{ baseCurrency.currencyCode }}</span>
                  <span v-if="baseCurrency.symbol" class="text-h6 text-grey-7 q-ml-sm">{{ baseCurrency.symbol }}</span>
                </template>
                <span v-else class="text-grey-7">No base currency set.</span>
              </div>
              <q-btn
                v-if="isSuperAdmin"
                outline
                color="primary"
                icon="o_swap_horiz"
                label="Change"
                no-caps
                :disable="!rows.length"
                @click="baseDialogOpen = true"
              />
            </div>

            <div v-if="!isSuperAdmin" class="text-caption text-grey-6 q-mt-sm">
              Only a Super Admin can change the base currency.
            </div>
          </q-card-section>
        </q-card>
      </div>

      <!-- Auto-refresh -->
      <div class="col-12 col-md-6">
        <AutoRefreshCard
          :config="autoRefreshConfig"
          :saving="savingAutoRefresh"
          :can-write="canWrite"
          @save="onAutoRefreshSave"
        />
      </div>
    </div>

    <div class="text-subtitle1 q-mb-sm">Supported currencies</div>
    <AppDataTable
      page-key="currencies"
      row-key="currencyCode"
      :rows="rows"
      :columns="columns"
      :loading="loading"
      :pagination="tablePagination"
      no-data-label="No currencies configured yet."
    >
      <template #body="props">
        <q-tr :props="props" :class="{ 'currency-row--disabled': !props.row.isEnabled }">
          <q-td key="currencyCode" :props="props">
            <a class="text-primary cursor-pointer text-weight-medium" @click="onManage(props.row)">{{ props.row.currencyCode }}</a>
            <q-badge v-if="props.row.isBaseCurrency" color="primary" class="q-ml-sm" label="Base" />
            <q-badge v-if="props.row.isRateLocked && !props.row.isBaseCurrency" outline color="grey-7" class="q-ml-sm" label="Rate locked" />
          </q-td>
          <q-td key="symbol" :props="props">{{ props.row.symbol || '—' }}</q-td>
          <q-td key="exchangeRate" :props="props">{{ formatRate(props.row.exchangeRate) }}</q-td>
          <q-td key="status" :props="props">
            <q-toggle
              :model-value="props.row.isEnabled"
              color="primary"
              :disable="!canWrite || props.row.isBaseCurrency"
              @update:model-value="onToggleEnabled(props.row, $event)"
            />
            <q-badge v-if="!props.row.isEnabled" color="grey-4" text-color="grey-9" label="Disabled" />
          </q-td>
          <q-td key="lastRate" :props="props">{{ formatDate(props.row.lastRateUpdatedOnUtc) }}</q-td>
          <q-td key="actions" :props="props" class="text-right">
            <q-btn
              flat round dense icon="o_sync"
              :disable="!canWrite || props.row.isBaseCurrency"
              @click="onUpdateRateClick(props.row)"
            >
              <q-tooltip>{{ props.row.isBaseCurrency ? 'Base rate is fixed at 1' : 'Update rate' }}</q-tooltip>
            </q-btn>
            <q-btn flat round dense icon="o_tune" :disable="!canWrite" @click="onManage(props.row)">
              <q-tooltip>Edit</q-tooltip>
            </q-btn>
            <q-btn
              flat round dense icon="o_delete" color="negative"
              :disable="!canWrite || props.row.isBaseCurrency"
              @click="onDelete(props.row)"
            >
              <q-tooltip>{{ props.row.isBaseCurrency ? 'The base currency cannot be deleted' : 'Delete' }}</q-tooltip>
            </q-btn>
          </q-td>
        </q-tr>
      </template>
    </AppDataTable>

    <q-banner v-if="!canWrite" class="bg-grey-2 rounded-borders q-mt-md text-grey-8">
      You have read-only access to currencies.
    </q-banner>

    <RateUpdateDialog
      v-model="rateDialogOpen"
      :item="rateTarget"
      :saving="savingRate"
      @submit="onRateSubmit"
      @cancel="rateDialogOpen = false"
    />

    <BaseCurrencyDialog
      v-model="baseDialogOpen"
      :currencies="rows"
      :current-base-code="baseCurrency?.currencyCode || ''"
      :saving="savingBase"
      @submit="onBaseSubmit"
      @cancel="baseDialogOpen = false"
    />
  </q-page>
</template>

<script setup>
/*
 * Currency configuration page (WO-91 REQ-TEN-006).
 *
 * - Base currency display with a SuperAdmin-only "Change" action that opens a
 *   confirmation warning modal (BaseCurrencyDialog) before promoting a currency
 *   (AC-TEN-006.1). SuperAdmin is detected from the auth store roles; the backend
 *   PUT /api/tenant/base-currency is itself gated by the SuperAdmin policy.
 * - Supported currencies table with add/edit/remove, an inline enable/disable
 *   toggle and a per-row manual "Update rate" action (AC-TEN-006.2/3/5). Disabled
 *   rows are muted and badged.
 * - Auto-refresh toggle + interval + source URL (AC-TEN-006.4).
 */
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { currenciesApi } from 'modules/currencies/api'
import { formatDateTime } from 'src/utils/datetime'
import { getApiErrorMessage } from 'services/api'
import { useAuthStore } from 'stores/auth'
import { useNotify } from 'composables/useNotify'
import { usePermissions, Permissions } from 'composables/usePermissions'
import { deleteConfirmation } from 'dialogs/delete_confirmation'
import RateUpdateDialog from 'modules/currencies/components/RateUpdateDialog.vue'
import BaseCurrencyDialog from 'modules/currencies/components/BaseCurrencyDialog.vue'
import AutoRefreshCard from 'modules/currencies/components/AutoRefreshCard.vue'

const router = useRouter()
const auth = useAuthStore()
const notify = useNotify()
const { has } = usePermissions()

const canWrite = computed(() => has(Permissions.CurrenciesWrite))
// The base-currency change is SuperAdmin-only (mirrors the backend policy).
const isSuperAdmin = computed(() => (auth.roles || []).includes('SuperAdmin'))

const columns = [
  { name: 'currencyCode', label: 'Code', field: 'currencyCode', align: 'left', sortable: true },
  { name: 'symbol', label: 'Symbol', field: 'symbol', align: 'left' },
  { name: 'exchangeRate', label: 'Exchange rate', field: 'exchangeRate', align: 'left', sortable: true },
  { name: 'status', label: 'Enabled', field: 'isEnabled', align: 'left', sortable: true },
  { name: 'lastRate', label: 'Rate updated', field: 'lastRateUpdatedOnUtc', align: 'left', sortable: true },
  { name: 'actions', label: '', field: 'actions', align: 'right', sortable: false }
]
// Client-side table: show every currency (config lists are small).
const tablePagination = { rowsPerPage: 0 }

const rows = ref([])
const baseCurrency = ref(null)
const autoRefreshConfig = ref(null)
const loading = ref(false)

const rateDialogOpen = ref(false)
const rateTarget = ref(null)
const savingRate = ref(false)

const baseDialogOpen = ref(false)
const savingBase = ref(false)

const savingAutoRefresh = ref(false)

function formatRate (value) {
  if (value === null || value === undefined) return '—'
  const n = Number(value)
  if (!Number.isFinite(n)) return String(value)
  return n.toLocaleString(undefined, { maximumFractionDigits: 6 })
}

function formatDate (iso) {
  return formatDateTime(iso)
}

async function loadCurrencies () {
  try {
    const res = await currenciesApi.list()
    rows.value = Array.isArray(res) ? res : (res?.items || res?.data || [])
  } catch (err) {
    rows.value = []
    notify.error(getApiErrorMessage(err))
  }
}

async function loadBase () {
  try {
    baseCurrency.value = await currenciesApi.getBaseCurrency()
  } catch (err) {
    baseCurrency.value = null
  }
}

async function loadAutoRefresh () {
  try {
    autoRefreshConfig.value = await currenciesApi.getAutoRefreshConfig()
  } catch (err) {
    autoRefreshConfig.value = null
  }
}

async function load () {
  loading.value = true
  try {
    await Promise.allSettled([loadCurrencies(), loadBase(), loadAutoRefresh()])
  } finally {
    loading.value = false
  }
}

function onAdd () { router.push({ name: 'currency-new' }) }
function onManage (row) { router.push({ name: 'currency-detail', params: { id: row.currencyCode } }) }

async function onDelete (row) {
  if (row.isBaseCurrency) return
  if (!(await deleteConfirmation(`the currency "${row.currencyCode}"`))) return
  try {
    await currenciesApi.remove(row.currencyCode)
    notify.success(`${row.currencyCode} deleted`)
    await loadCurrencies()
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  }
}

async function onToggleEnabled (row, value) {
  try {
    await currenciesApi.update(row.currencyCode, {
      symbol: row.symbol,
      exchangeRate: row.exchangeRate,
      isEnabled: value,
      isRateLocked: row.isRateLocked
    })
    notify.success(value ? `${row.currencyCode} enabled` : `${row.currencyCode} disabled`)
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    // Reload so the toggle reflects the persisted truth either way.
    await loadCurrencies()
  }
}

function onUpdateRateClick (row) {
  rateTarget.value = { ...row }
  rateDialogOpen.value = true
}

async function onRateSubmit (newRate) {
  const row = rateTarget.value
  if (!row) return
  savingRate.value = true
  try {
    await currenciesApi.updateRate(row.currencyCode, {
      symbol: row.symbol,
      exchangeRate: newRate,
      isEnabled: row.isEnabled,
      isRateLocked: row.isRateLocked
    })
    notify.success(`${row.currencyCode} rate updated`)
    rateDialogOpen.value = false
    await loadCurrencies()
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    savingRate.value = false
  }
}

async function onBaseSubmit (code) {
  savingBase.value = true
  try {
    await currenciesApi.setBaseCurrency(code)
    notify.success(`Base currency changed to ${code}`)
    baseDialogOpen.value = false
    await Promise.all([loadCurrencies(), loadBase()])
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    savingBase.value = false
  }
}

async function onAutoRefreshSave (payload) {
  savingAutoRefresh.value = true
  try {
    autoRefreshConfig.value = await currenciesApi.updateAutoRefreshConfig(payload)
    notify.success('Auto-refresh settings saved')
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    savingAutoRefresh.value = false
  }
}

onMounted(load)
</script>

<style scoped>
.currency-row--disabled {
  opacity: 0.55;
}
</style>
