<template>
  <q-page class="app-page">
    <AppListHeader
      title="Gift Certificates"
      subtitle="Issue, adjust and track store gift certificates. Every balance change is recorded in the ledger."
      :breadcrumbs="[{ label: 'Home', icon: 'o_home', to: '/dashboard' }, { label: 'Gift Certificates' }]"
      show-add
      add-label="Create Gift Certificate"
      @add="router.push({ name: 'gift-certificate-new' })"
    >
      <template #actions>
        <q-input v-model="search" dense outlined debounce="400" placeholder="Code, recipient or purchaser" style="min-width: 240px" @update:model-value="reload">
          <template #prepend><q-icon name="o_search" /></template>
          <template v-if="search" #append><q-icon name="o_close" class="cursor-pointer" @click="search = ''; reload()" /></template>
        </q-input>
        <q-select v-model="status" dense outlined emit-value map-options :options="statusOptions" style="min-width: 130px" @update:model-value="reload" />
        <q-select v-model="source" dense outlined emit-value map-options :options="sourceOptions" style="min-width: 130px" @update:model-value="reload" />
        <q-toggle v-model="hasBalance" label="Has balance" @update:model-value="reload" />
        <q-btn outline no-caps color="primary" icon="o_download" label="Export CSV" :loading="exporting" @click="exportCsv" />
        <q-btn flat round dense icon="o_settings" @click="openSettings"><q-tooltip>Gift certificate settings</q-tooltip></q-btn>
      </template>
    </AppListHeader>

    <AppDataTable
      page-key="gift-certificates"
      row-key="id"
      title="All gift certificates"
      :rows="rows"
      :columns="columns"
      :loading="loading"
      :pagination="pagination"
      show-actions
      @request="onRequest"
      @refresh="reload"
    >
      <template #body-cell-code="cell">
        <q-td :props="cell">
          <a class="text-primary cursor-pointer text-weight-medium" style="font-family: monospace" @click="open(cell.row)">{{ cell.row.code }}</a>
          <q-icon v-if="!cell.row.isOnline" name="o_markunread_mailbox" size="16px" class="q-ml-xs text-grey-7"><q-tooltip>Physical card</q-tooltip></q-icon>
        </q-td>
      </template>
      <template #body-cell-source="cell">
        <q-td :props="cell">
          <span v-if="cell.row.source === 'Admin'">Admin</span>
          <template v-else>
            Purchased
            <router-link v-if="cell.row.sourceOrderId" :to="{ name: 'admin-order-detail', params: { id: cell.row.sourceOrderId } }" class="text-primary q-ml-xs">
              #{{ cell.row.sourceOrderNumber }}
            </router-link>
          </template>
        </q-td>
      </template>
      <template #body-cell-recipient="cell">
        <q-td :props="cell">
          <div>{{ cell.row.recipientName || '—' }}</div>
          <div v-if="cell.row.recipientEmail" class="text-caption text-grey-7">{{ cell.row.recipientEmail }}</div>
        </q-td>
      </template>
      <template #body-cell-status="cell">
        <q-td :props="cell">
          <q-badge :color="cell.row.status === 'Active' ? 'positive' : 'grey'" :label="cell.row.status" />
          <q-badge v-if="isExpired(cell.row)" color="orange" label="Expired" class="q-ml-xs" />
        </q-td>
      </template>
      <template #actions="{ row }">
        <q-btn flat round dense icon="o_edit" @click="open(row)"><q-tooltip>Edit</q-tooltip></q-btn>
        <q-btn flat round dense icon="o_delete" color="negative" @click="remove(row)"><q-tooltip>Delete</q-tooltip></q-btn>
      </template>
    </AppDataTable>

    <!-- ============ SETTINGS ============ -->
    <q-dialog v-model="settingsOpen">
      <q-card style="width: 520px; max-width: 95vw">
        <q-card-section class="text-subtitle1 text-weight-medium">Gift certificate settings</q-card-section>
        <q-card-section class="q-gutter-y-sm">
          <div class="row q-col-gutter-md">
            <div class="col-6"><AppTextField v-model="settings.codePrefix" label="Code prefix" maxlength="10" hint="Optional, e.g. HK" /></div>
            <div class="col-6"><AppTextField v-model.number="settings.codeLength" label="Code length" type="number" min="10" max="40" hint="Random characters (min 10)" /></div>
          </div>
          <AppTextField v-model.number="settings.defaultExpiryMonths" label="Default expiry for purchased certificates (months)" type="number" min="1" max="120" clearable hint="Empty = never expires" />
          <q-toggle v-model="settings.allowRedeemOnGcPurchase" label="Allow redeeming a gift certificate on an order that buys gift certificates" />
          <AppSelect
            v-model="settings.purchasePaymentMethods"
            label="Payment methods allowed when buying gift certificates"
            multiple use-chips emit-value map-options
            :options="paymentMethodOptions"
          />
        </q-card-section>
        <q-card-actions align="right">
          <q-btn v-close-popup flat no-caps label="Cancel" />
          <q-btn unelevated no-caps color="primary" label="Save settings" :loading="savingSettings" @click="saveSettings" />
        </q-card-actions>
      </q-card>
    </q-dialog>
  </q-page>
</template>

<script setup>
/*
 * Gift certificates list: server paging (25, newest first), search on code / recipient / purchaser (the
 * PURCHASING order's buyer), status / source / has-balance filters, CSV export and the code settings.
 */
import { ref, reactive, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { date } from 'quasar'
import { getApiErrorMessage } from 'services/api'
import { giftCertificateApi, money } from 'modules/gift-certificates/api'
import { useNotify } from 'composables/useNotify'
import { confirmation } from 'dialogs/confirmation'

const router = useRouter()
const notify = useNotify()

const statusOptions = [{ label: 'All statuses', value: null }, { label: 'Active', value: 'Active' }, { label: 'Inactive', value: 'Inactive' }]
const sourceOptions = [{ label: 'All sources', value: null }, { label: 'Admin', value: 'Admin' }, { label: 'Purchased', value: 'Purchased' }]
const paymentMethodOptions = ['Stripe', 'PayPal', 'Razorpay', 'Square', 'AuthorizeNet', 'CashOnDelivery', 'BankTransfer']
  .map((m) => ({ label: m.replace(/([a-z])([A-Z])/g, '$1 $2'), value: m }))

const columns = [
  { name: 'code', label: 'Code', field: 'code', align: 'left', sortable: true },
  { name: 'createdOnUtc', label: 'Created', field: 'createdOnUtc', align: 'left', sortable: true, format: (v) => date.formatDate(v, 'YYYY-MM-DD HH:mm') },
  { name: 'source', label: 'Source', field: 'source', align: 'left' },
  { name: 'purchasedBy', label: 'Purchased By', field: 'purchasedBy', align: 'left', format: (v) => v || '—' },
  { name: 'recipient', label: 'Recipient', field: 'recipientName', align: 'left' },
  { name: 'amount', label: 'Amount', field: 'amount', align: 'right', sortable: true, format: money },
  { name: 'spent', label: 'Spent', field: 'spent', align: 'right', format: money },
  { name: 'balance', label: 'Balance', field: 'balance', align: 'right', sortable: true, format: money },
  { name: 'status', label: 'Status', field: 'status', align: 'center', sortable: true }
]

const rows = ref([])
const loading = ref(false)
const exporting = ref(false)
const search = ref('')
const status = ref(null)
const source = ref(null)
const hasBalance = ref(false)
const pagination = ref({ page: 1, rowsPerPage: 25, rowsNumber: 0, sortBy: 'createdOnUtc', descending: true })

const settingsOpen = ref(false)
const savingSettings = ref(false)
const settings = reactive({ codePrefix: '', codeLength: 12, defaultExpiryMonths: null, allowRedeemOnGcPurchase: false, purchasePaymentMethods: [] })

const isExpired = (row) => row.expiresAtUtc && new Date(row.expiresAtUtc) < new Date()

function filters () {
  return {
    search: search.value || undefined,
    status: status.value || undefined,
    source: source.value || undefined,
    hasBalance: hasBalance.value || undefined
  }
}

async function fetch (props) {
  const p = props?.pagination || pagination.value
  loading.value = true
  try {
    const result = await giftCertificateApi.list({
      ...filters(),
      page: p.page,
      pageSize: p.rowsPerPage,
      sort: p.sortBy || undefined,
      dir: p.sortBy ? (p.descending ? 'desc' : 'asc') : undefined
    })
    rows.value = result?.items || []
    pagination.value = { ...p, rowsNumber: result?.totalCount ?? rows.value.length }
  } catch (err) {
    rows.value = []
    notify.error(getApiErrorMessage(err))
  } finally {
    loading.value = false
  }
}

function onRequest (props) { fetch(props) }
function reload () { fetch({ pagination: { ...pagination.value, page: 1 } }) }
function open (row) { router.push({ name: 'gift-certificate-detail', params: { id: row.id } }) }

async function remove (row) {
  const ok = await confirmation({
    title: 'Delete gift certificate?',
    message: `${row.code} will be deactivated and its remaining balance of ${money(row.balance)} voided. This cannot be undone.`,
    okLabel: 'Delete',
    color: 'negative'
  })
  if (!ok) return
  try {
    await giftCertificateApi.remove(row.id)
    notify.success('Gift Certificate deleted successfully')
    reload()
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  }
}

async function exportCsv () {
  exporting.value = true
  try {
    await giftCertificateApi.exportCsv(filters())
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    exporting.value = false
  }
}

async function openSettings () {
  try {
    Object.assign(settings, await giftCertificateApi.getSettings())
    settingsOpen.value = true
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  }
}

async function saveSettings () {
  savingSettings.value = true
  try {
    Object.assign(settings, await giftCertificateApi.saveSettings({
      ...settings,
      codeLength: Number(settings.codeLength) || 12,
      defaultExpiryMonths: settings.defaultExpiryMonths ? Number(settings.defaultExpiryMonths) : null
    }))
    notify.success('Gift certificate settings saved')
    settingsOpen.value = false
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    savingSettings.value = false
  }
}

onMounted(() => fetch())
</script>
