<template>
  <q-page class="app-page">
    <AppDetailHeader
      :breadcrumbs="[
        { label: 'Home', icon: 'o_home', to: '/dashboard' },
        { label: 'Orders', to: '/orders' },
        { label: form ? form.orderNumber : 'Order', to: { name: 'admin-order-detail', params: { id: orderId } } },
        { label: 'Apply credit' }
      ]"
      @back="backToOrder"
    >
      <template #actions>
        <q-btn outline color="grey-8" no-caps icon="o_arrow_back" label="Back to order" @click="backToOrder" />
      </template>
    </AppDetailHeader>

    <q-inner-loading :showing="loading" />

    <template v-if="form">
      <AppSection :title="`Order# ${form.orderNumber} — Order items`">
        <q-markup-table flat bordered dense separator="horizontal" class="quick-return-table">
          <thead>
            <tr class="bg-primary text-white">
              <th class="text-left">SKU</th>
              <th class="text-left">Name</th>
              <th class="text-center">Qty</th>
              <th class="text-center" style="width: 110px">Return</th>
              <th class="text-center">Return to stock</th>
              <th class="text-right">Price</th>
              <th class="text-right">Total</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="l in lines" :key="l.orderLineItemId">
              <td>{{ l.sku || '—' }}</td>
              <td style="white-space: normal">
                {{ l.productName }}
                <q-badge v-if="l.requiresFfl" color="deep-orange" label="FFL" class="q-ml-xs" />
                <div v-if="l.quantityRefunded" class="text-caption text-grey-7">{{ l.quantityRefunded }} already credited</div>
              </td>
              <td class="text-center">{{ l.quantity }}</td>
              <td class="text-center">
                <q-input
                  v-if="l.quantity > 0"
                  v-model.number="l.returnQty" type="number" dense outlined min="0" :max="l.quantity"
                  :error="qtyInvalid(l)" hide-bottom-space input-class="text-center"
                />
              </td>
              <td class="text-center">
                <q-checkbox v-if="l.quantity > 0" v-model="l.returnToStock" dense />
              </td>
              <td class="text-right">{{ money(l.unitPrice) }}</td>
              <td class="text-right">{{ money(l.lineTotal) }}</td>
            </tr>
          </tbody>
          <tbody class="totals">
            <tr>
              <td colspan="6" class="text-right text-weight-bold">Sub total:</td>
              <td class="text-right text-weight-bold">{{ money(form.subtotal) }}</td>
            </tr>
            <tr>
              <td colspan="6" class="text-right text-weight-bold">Shipping &amp; handling / restock fees:</td>
              <td class="text-right" style="width: 170px">
                <q-input
                  v-model.number="shippingAmount" type="number" step="0.01" min="0" dense outlined
                  prefix="$" input-class="text-right" hide-bottom-space :error="shippingInvalid"
                />
              </td>
            </tr>
            <tr>
              <td colspan="6" class="text-right text-weight-bold">Tax:</td>
              <td class="text-right">
                <span class="text-weight-bold">{{ money(form.taxTotal) }}</span>
                <div class="text-caption text-italic text-grey-7">Will be recalculated based on returned items</div>
              </td>
            </tr>
            <tr v-if="form.discountTotal > 0">
              <td colspan="6" class="text-right text-weight-bold">Discount:</td>
              <td class="text-right text-weight-bold text-negative">- {{ money(form.discountTotal) }}</td>
            </tr>
            <tr v-if="form.paymentFeeTotal > 0">
              <td colspan="6" class="text-right text-weight-bold">Payment fee:</td>
              <td class="text-right text-weight-bold">{{ money(form.paymentFeeTotal) }}</td>
            </tr>
            <tr>
              <td colspan="6" class="text-right text-weight-bold">Grand total:</td>
              <td class="text-right text-weight-bold text-subtitle1">{{ money(form.totalAmount) }}</td>
            </tr>
          </tbody>
        </q-markup-table>
      </AppSection>

      <AppSection title="Credit" class="q-mt-md">
        <div class="row q-col-gutter-md items-start">
          <div class="col-12 col-md-4">
            <div class="text-caption text-grey-7">Credit to apply</div>
            <div class="text-h6 text-weight-bold" :class="credit < 0 ? 'text-negative' : 'text-positive'">{{ money(credit) }}</div>
            <div class="text-caption text-grey-7">
              Returned items {{ money(preview.items) }} · shipping {{ money(preview.shipping) }} · tax {{ money(preview.tax) }}
            </div>
            <div class="text-caption text-grey-7">New order total: {{ money(form.totalAmount - credit) }}</div>
          </div>
          <div class="col-12 col-sm-6 col-md-4">
            <q-select v-model="method" :options="methodOptions" emit-value map-options dense outlined label="Credit to" />
            <div v-if="method === 'OriginalPayment'" class="text-caption text-grey-7 q-mt-xs">
              <template v-if="form.isPaid">Refundable to the original payment: {{ money(form.refundableBalance) }}</template>
              <template v-else>The order is not paid — the order is adjusted but nothing is refunded.</template>
            </div>
          </div>
          <div v-if="method === 'Manual'" class="col-12 col-sm-6 col-md-4">
            <q-input v-model="manualReference" dense outlined label="Refund reference (cheque #, portal ref …)" />
          </div>
          <div class="col-12">
            <q-input v-model="note" type="textarea" autogrow dense outlined label="Note (optional, added to the order history)" />
          </div>
        </div>
        <q-banner v-if="credit < 0" dense rounded class="bg-red-1 text-negative q-mt-sm">
          The credit would be negative. Lower the shipping amount.
        </q-banner>
        <q-banner
          v-else-if="method === 'OriginalPayment' && form.isPaid && credit > form.refundableBalance"
          dense rounded class="bg-orange-1 text-deep-orange q-mt-sm"
        >
          Only {{ money(form.refundableBalance) }} can be refunded automatically; the rest will need a manual refund.
        </q-banner>
      </AppSection>

      <div class="row justify-end q-mt-md">
        <q-btn
          color="positive" unelevated no-caps icon="o_check_circle" label="Apply credit"
          :loading="busy" :disable="!canApply" @click="apply"
        />
      </div>
    </template>
  </q-page>
</template>

<script setup>
/*
 * Apply credit — the PHP admin's credit screen (OrderController@credit / creditPost). Enter how many of each
 * item come back (and whether they go back into stock), adjust the order's shipping amount, and apply: the
 * server lowers the order's quantities and totals, recalculates tax, restocks, refunds the credit to the
 * original payment and logs it in the order history. No return (RMA) record is created.
 * The credit preview uses the same formulas as the server (ApplyQuickReturnCommandHandler).
 */
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'
import { confirmation } from 'dialogs/confirmation'
import { rmaApi } from 'modules/orders/api'

const route = useRoute()
const router = useRouter()
const notify = useNotify()
const orderId = route.params.id

const form = ref(null)
const lines = ref([])
const shippingAmount = ref(0)
const method = ref('OriginalPayment')
const manualReference = ref('')
const note = ref('')
const loading = ref(false)
const busy = ref(false)

const round2 = (v) => Math.round((Number(v) || 0) * 100) / 100
const money = (v) => {
  const code = form.value?.currencyCode || 'USD'
  try {
    return new Intl.NumberFormat(undefined, { style: 'currency', currency: code }).format(round2(v))
  } catch {
    return `${code} ${round2(v).toFixed(2)}`
  }
}

const methodOptions = computed(() => [
  { label: 'Original payment', value: 'OriginalPayment' },
  { label: 'Store credit', value: 'StoreCredit', disable: !form.value?.hasCustomerAccount },
  { label: 'Manual refund (record only)', value: 'Manual' }
])

function qtyInvalid (l) {
  const q = Number(l.returnQty) || 0
  return q < 0 || q > l.quantity || !Number.isInteger(q)
}
const shippingInvalid = computed(() => shippingAmount.value === '' || (Number(shippingAmount.value) || 0) < 0)

// Same maths as the server: goods returned (net of the order discount share) + shipping difference + tax difference.
const preview = computed(() => {
  const f = form.value
  if (!f) return { items: 0, shipping: 0, tax: 0 }
  const newSubtotal = round2(lines.value.reduce((s, l) => s + l.unitPrice * (l.quantity - (Number(l.returnQty) || 0)), 0))
  const newDiscount = f.subtotal > 0 ? round2(f.discountTotal * newSubtotal / f.subtotal) : f.discountTotal
  const oldBase = f.subtotal - f.discountTotal
  const newTax = oldBase > 0 ? round2(f.taxTotal * (newSubtotal - newDiscount) / oldBase) : f.taxTotal
  return {
    items: round2((f.subtotal - newSubtotal) - (f.discountTotal - newDiscount)),
    shipping: round2(f.shippingTotal - (Number(shippingAmount.value) || 0)),
    tax: round2(f.taxTotal - newTax)
  }
})
const credit = computed(() => round2(preview.value.items + preview.value.shipping + preview.value.tax))

const returned = computed(() => lines.value.filter((l) => (Number(l.returnQty) || 0) > 0))
const needsCredit = computed(() =>
  returned.value.length > 0 || round2(shippingAmount.value) !== round2(form.value?.shippingTotal))

const canApply = computed(() =>
  needsCredit.value &&
  credit.value >= 0 &&
  !lines.value.some(qtyInvalid) &&
  !shippingInvalid.value &&
  (method.value !== 'Manual' || manualReference.value.trim()))

function backToOrder () {
  router.push({ name: 'admin-order-detail', params: { id: orderId } })
}

async function load () {
  loading.value = true
  try {
    const f = await rmaApi.quickReturnForm(orderId)
    form.value = f
    // PHP defaults: return 0 of each item, "return to stock" ticked, shipping = the order's shipping.
    lines.value = f.lines.map((l) => ({ ...l, returnQty: 0, returnToStock: true }))
    shippingAmount.value = round2(f.shippingTotal)
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    loading.value = false
  }
}

async function apply () {
  const units = returned.value.reduce((n, l) => n + l.returnQty, 0)
  const ok = await confirmation({
    title: 'Apply credit',
    message: `${units ? `Return ${units} unit(s) and a` : 'A'}pply a credit of ${money(credit.value)} to order ${form.value.orderNumber}? The order quantities and totals will be updated. This cannot be undone.`,
    okLabel: 'Apply credit',
    color: 'positive'
  })
  if (!ok) return

  busy.value = true
  try {
    const res = await rmaApi.applyQuickReturn(orderId, {
      lines: returned.value.map((l) => ({ orderLineItemId: l.orderLineItemId, quantity: l.returnQty, returnToStock: l.returnToStock })),
      shippingAmount: round2(shippingAmount.value),
      method: method.value,
      manualReference: manualReference.value.trim() || null,
      note: note.value.trim() || null
    })
    if (res.success) notify.success(res.message)
    else notify.warning(res.message)
    backToOrder()
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    busy.value = false
  }
}

onMounted(load)
</script>

<style scoped>
.quick-return-table th {
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.02em;
}
.quick-return-table .totals td {
  border-top: 1px solid rgba(0, 0, 0, 0.08);
}
</style>
