<template>
  <q-page class="app-page">
    <AppDetailHeader
      :title="gc ? gc.code : 'Gift Certificate'"
      :breadcrumbs="[
        { label: 'Home', icon: 'o_home', to: '/dashboard' },
        { label: 'Gift Certificates', to: { name: 'gift-certificates' } },
        { label: gc ? gc.code : 'Certificate' }
      ]"
      :status="gc ? gc.status : ''"
      :status-color="gc?.status === 'Active' ? 'positive' : 'grey'"
      show-back
      @back="goBack"
    >
      <template #actions>
        <q-btn v-if="gc?.recipientEmail" outline no-caps color="primary" icon="o_forward_to_inbox" label="Resend email" :loading="resending" @click="resend" />
      </template>
    </AppDetailHeader>

    <q-inner-loading :showing="loading" />

    <template v-if="gc">
      <!-- ============ HEADER ============ -->
      <q-card flat bordered class="app-section q-mb-md">
        <q-card-section class="row q-col-gutter-lg items-center">
          <div class="col-12 col-md-4">
            <div class="text-caption text-grey-7">Code</div>
            <div class="row items-center no-wrap">
              <span class="text-h6 text-weight-bold" style="font-family: monospace; letter-spacing: 2px">{{ gc.code }}</span>
              <q-btn flat round dense icon="o_content_copy" class="q-ml-xs" @click="copyText(gc.code, notify)"><q-tooltip>Copy code</q-tooltip></q-btn>
            </div>
          </div>
          <div class="col-6 col-md-2">
            <div class="text-caption text-grey-7">Source</div>
            <div>
              {{ gc.source }}
              <router-link v-if="gc.sourceOrderId" :to="{ name: 'admin-order-detail', params: { id: gc.sourceOrderId } }" class="text-primary">#{{ gc.sourceOrderNumber }}</router-link>
            </div>
            <div v-if="gc.purchasedBy" class="text-caption text-grey-7">by {{ gc.purchasedBy }}</div>
          </div>
          <div class="col-6 col-md-2">
            <div class="text-caption text-grey-7">Original balance</div>
            <div class="text-subtitle1">{{ money(gc.originalBalance) }}</div>
          </div>
          <div class="col-6 col-md-2">
            <div class="text-caption text-grey-7">Current balance</div>
            <div class="text-subtitle1 text-weight-bold">{{ money(gc.currentBalance) }}</div>
          </div>
          <div class="col-6 col-md-2">
            <div class="text-caption text-grey-7">{{ gc.isOnline ? 'Email' : 'Delivery' }}</div>
            <div v-if="!gc.isOnline">Physical card</div>
            <div v-else-if="gc.emailSentAtUtc">Sent {{ fmt(gc.emailSentAtUtc) }}</div>
            <div v-else class="text-grey-6">Not sent</div>
          </div>
        </q-card-section>
      </q-card>

      <!-- ============ FORM ============ -->
      <q-form @submit.prevent="save">
        <q-card flat bordered class="app-section q-mb-md">
          <q-card-section class="text-subtitle1 text-weight-medium">Details</q-card-section>
          <q-separator />
          <q-card-section class="q-gutter-y-xs">
            <div class="row q-col-gutter-md">
              <div class="col-12 col-md-4"><AppSelect v-model="form.status" label="Status" :options="statusOptions" emit-value map-options /></div>
              <div class="col-12 col-md-4"><AppDateField v-model="form.expiresAt" label="Expires At" hint="Empty = never expires" /></div>
            </div>
            <div class="row q-col-gutter-md">
              <div class="col-12 col-md-6"><AppTextField v-model="form.recipientName" label="Recipient Name" maxlength="75" /></div>
              <div class="col-12 col-md-6"><AppTextField v-model="form.recipientEmail" label="Recipient Email" type="email" maxlength="75" :v="v$.recipientEmail" /></div>
            </div>
            <AppTextField v-model="form.message" label="Message" type="textarea" autogrow maxlength="1000" />

            <q-separator class="q-my-md" />
            <div class="text-subtitle2">Adjust balance</div>
            <div class="row q-col-gutter-md items-start">
              <div class="col-12 col-md-4">
                <q-btn-toggle v-model="adjustMode" no-caps unelevated toggle-color="primary" color="grey-3" text-color="grey-9"
                  :options="[{ label: 'Set new balance', value: 'set' }, { label: '+ / − amount', value: 'adjust' }]" />
              </div>
              <div class="col-12 col-md-4">
                <AppTextField v-model.number="form.balanceInput" :label="adjustMode === 'set' ? 'New balance' : 'Amount to add (negative to subtract)'"
                  type="number" step="0.01" :min="adjustMode === 'set' ? 0 : undefined" :v="v$.balanceInput" clearable>
                  <template #prepend><span class="text-grey-7">$</span></template>
                </AppTextField>
              </div>
              <div v-if="balanceDelta !== 0" class="col-12 col-md-4 q-pt-lg text-body2">
                {{ money(gc.currentBalance) }} → <b>{{ money(gc.currentBalance + balanceDelta) }}</b>
                <span :class="balanceDelta > 0 ? 'text-positive' : 'text-negative'">({{ signedMoney(balanceDelta) }})</span>
              </div>
            </div>
            <AppTextField v-model="form.comment" label="Comments" :required="commentRequired" maxlength="255" counter :v="v$.comment"
              :hint="commentRequired ? 'Required: the balance or status is changing' : 'Saved on the ledger entry'" />
          </q-card-section>
          <q-separator />
          <q-card-actions class="q-pa-md">
            <q-btn flat no-caps label="Cancel" @click="goBack" />
            <q-space />
            <q-btn unelevated no-caps color="primary" icon="o_check" label="Save" type="submit" :loading="saving" />
          </q-card-actions>
        </q-card>
      </q-form>

      <!-- ============ LEDGER ============ -->
      <q-card flat bordered class="app-section q-mb-md">
        <q-card-section class="text-subtitle1 text-weight-medium">Ledger</q-card-section>
        <q-table flat dense :rows="gc.ledger" :columns="ledgerColumns" row-key="id" :rows-per-page-options="[25, 50, 0]">
          <template #body-cell-amount="cell">
            <q-td :props="cell" :class="cell.row.amount > 0 ? 'text-positive' : cell.row.amount < 0 ? 'text-negative' : ''">{{ signedMoney(cell.row.amount) }}</q-td>
          </template>
          <template #body-cell-orderNumber="cell">
            <q-td :props="cell">
              <router-link v-if="cell.row.orderId" :to="{ name: 'admin-order-detail', params: { id: cell.row.orderId } }" class="text-primary">#{{ cell.row.orderNumber || 'order' }}</router-link>
              <span v-else class="text-grey-6">—</span>
            </q-td>
          </template>
        </q-table>
      </q-card>

      <!-- ============ ORDERS ============ -->
      <q-card flat bordered class="app-section">
        <q-card-section class="text-subtitle1 text-weight-medium">Orders using this certificate</q-card-section>
        <q-table flat dense :rows="gc.orders" :columns="orderColumns" row-key="orderId" :rows-per-page-options="[25, 0]" no-data-label="Not used on any order yet">
          <template #body-cell-orderNumber="cell">
            <q-td :props="cell">
              <q-icon v-if="cell.row.ebayOrderId" name="o_storefront" color="indigo-7" size="16px" class="q-mr-xs"><q-tooltip>eBay order {{ cell.row.ebayOrderId }}</q-tooltip></q-icon>
              #{{ cell.row.orderNumber }}
            </q-td>
          </template>
          <template #body-cell-paid="cell">
            <q-td :props="cell"><q-badge :color="cell.row.paid ? 'positive' : 'orange'" :label="cell.row.paid ? 'Paid' : 'Unpaid'" /></q-td>
          </template>
          <template #body-cell-view="cell">
            <q-td :props="cell"><q-btn flat dense no-caps color="primary" label="View" :to="{ name: 'admin-order-detail', params: { id: cell.row.orderId } }" /></q-td>
          </template>
        </q-table>
      </q-card>
    </template>
  </q-page>
</template>

<script setup>
/*
 * Gift certificate edit: status / expiry / recipient, and a balance adjustment (new balance or ± amount).
 * The original balance never changes. A comment is required when the balance or status changes; a 409
 * (stale rowVersion) offers to reload.
 */
import { ref, reactive, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { date } from 'quasar'
import useVuelidate from '@vuelidate/core'
import { helpers, email, maxLength } from 'validators'
import { getApiErrorMessage } from 'services/api'
import { giftCertificateApi, money, signedMoney, copyText, ACTION_LABELS } from 'modules/gift-certificates/api'
import { useNotify } from 'composables/useNotify'
import { confirmation } from 'dialogs/confirmation'
import AppDetailHeader from 'components/common/AppDetailHeader.vue'
import AppDateField from 'components/common/AppDateField.vue'

const route = useRoute()
const router = useRouter()
const notify = useNotify()

const statusOptions = [{ label: 'Active', value: 'Active' }, { label: 'Inactive', value: 'Inactive' }]
const fmt = (v) => (v ? date.formatDate(v, 'YYYY-MM-DD HH:mm') : '')

const gc = ref(null)
const loading = ref(false)
const saving = ref(false)
const resending = ref(false)
const adjustMode = ref('set')
const form = reactive({ status: 'Active', expiresAt: '', recipientName: '', recipientEmail: '', message: '', balanceInput: null, comment: '' })

const hasInput = computed(() => form.balanceInput !== null && form.balanceInput !== '')
const balanceDelta = computed(() => {
  if (!gc.value || !hasInput.value) return 0
  const n = Number(form.balanceInput)
  const delta = adjustMode.value === 'set' ? n - gc.value.currentBalance : n
  return Math.round(delta * 100) / 100
})
const commentRequired = computed(() => balanceDelta.value !== 0 || (gc.value && form.status !== gc.value.status))

const $externalResults = reactive({})
const v$ = useVuelidate({
  recipientEmail: { email, maxLength: maxLength(75) },
  balanceInput: {
    valid: helpers.withMessage('The balance cannot go below 0 (max 2 decimals)', (v) => {
      if (v === null || v === '' || !gc.value) return true
      const n = Number(v)
      if (Math.round(n * 100) !== n * 100) return false
      return (adjustMode.value === 'set' ? n : gc.value.currentBalance + n) >= 0
    })
  },
  comment: {
    requiredIfChanged: helpers.withMessage('A comment is required when the balance or status changes',
      (v) => !commentRequired.value || !!(v && v.trim()))
  }
}, form, { $externalResults })

const ledgerColumns = [
  { name: 'createdOnUtc', label: 'Date', field: 'createdOnUtc', align: 'left', format: fmt },
  { name: 'action', label: 'Action', field: 'action', align: 'left', format: (v) => ACTION_LABELS[v] || v },
  { name: 'amount', label: 'Amount', field: 'amount', align: 'right' },
  { name: 'balanceAfter', label: 'Balance After', field: 'balanceAfter', align: 'right', format: money },
  { name: 'orderNumber', label: 'Order #', field: 'orderNumber', align: 'left' },
  { name: 'comment', label: 'Comment', field: 'comment', align: 'left', format: (v) => v || '' },
  { name: 'createdBy', label: 'By', field: 'createdBy', align: 'left' }
]
const orderColumns = [
  { name: 'orderNumber', label: 'Order #', field: 'orderNumber', align: 'left' },
  { name: 'placedOnUtc', label: 'Date', field: 'placedOnUtc', align: 'left', format: fmt },
  { name: 'billTo', label: 'Bill To', field: 'billTo', align: 'left' },
  { name: 'shipTo', label: 'Ship To', field: 'shipTo', align: 'left' },
  { name: 'orderTotal', label: 'Order Total', field: 'orderTotal', align: 'right', format: money },
  { name: 'appliedAmount', label: 'Applied', field: 'appliedAmount', align: 'right', format: money },
  { name: 'paid', label: 'Paid', field: 'paid', align: 'center' },
  { name: 'status', label: 'Status', field: 'status', align: 'left' },
  { name: 'view', label: '', field: 'orderId', align: 'right' }
]

function fill (dto) {
  gc.value = dto
  Object.assign(form, {
    status: dto.status,
    expiresAt: dto.expiresAtUtc ? dto.expiresAtUtc.slice(0, 10) : '',
    recipientName: dto.recipientName || '',
    recipientEmail: dto.recipientEmail || '',
    message: dto.message || '',
    balanceInput: null,
    comment: ''
  })
}

async function load () {
  loading.value = true
  try {
    fill(await giftCertificateApi.get(route.params.id))
    v$.value.$reset()
  } catch (err) {
    gc.value = null
    notify.error(getApiErrorMessage(err))
  } finally {
    loading.value = false
  }
}

async function save () {
  v$.value.$clearExternalResults()
  if (!(await v$.value.$validate())) return
  saving.value = true
  try {
    fill(await giftCertificateApi.update(gc.value.id, {
      status: form.status,
      expiresAt: form.expiresAt ? `${form.expiresAt}T23:59:59Z` : null,
      recipientName: form.recipientName || null,
      recipientEmail: form.recipientEmail || null,
      message: form.message || null,
      newBalance: balanceDelta.value !== 0 ? Math.round((gc.value.currentBalance + balanceDelta.value) * 100) / 100 : null,
      adjustBy: null,
      comment: form.comment || null,
      rowVersion: gc.value.rowVersion
    }))
    notify.success('Gift Certificate updated successfully')
  } catch (err) {
    const status = err?.response?.status
    if (status === 409) {
      if (await confirmation({ title: 'Changed by someone else', message: 'This gift certificate was changed by someone else. Reload?', okLabel: 'Reload' })) await load()
      return
    }
    const errors = err?.response?.data?.errors
    if (status === 400 && errors && !Array.isArray(errors)) {
      for (const [k, m] of Object.entries(errors)) {
        const field = k === 'NewBalance' ? 'balanceInput' : k.charAt(0).toLowerCase() + k.slice(1)
        if (field in form) $externalResults[field] = m
      }
    }
    notify.error(getApiErrorMessage(err))
  } finally {
    saving.value = false
  }
}

async function resend () {
  resending.value = true
  try {
    await giftCertificateApi.resendEmail(gc.value.id)
    notify.success(`Email queued to ${gc.value.recipientEmail}`)
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    resending.value = false
  }
}

function goBack () { router.push({ name: 'gift-certificates' }) }

onMounted(load)
</script>
