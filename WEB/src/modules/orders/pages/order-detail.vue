<template>
  <q-page class="app-page">
    <AppDetailHeader
      :breadcrumbs="[
        { label: 'Home', icon: 'o_home', to: '/dashboard' },
        { label: 'Orders', to: '/orders' },
        { label: order ? order.orderNumber : 'Order' }
      ]"
      :status="order ? order.status : ''"
      :status-color="order ? statusColor(order.status) : 'grey'"
      @back="$router.push({ name: 'admin-orders' })"
    >
      <template #actions>
        <q-btn outline color="grey-8" no-caps icon="o_download" label="Invoice" class="q-mr-xs" @click="downloadInvoice" />
        <q-btn outline color="grey-8" no-caps icon="o_description" label="Packing slip" @click="downloadPackingSlip" />
      </template>
    </AppDetailHeader>

    <q-inner-loading :showing="loading" />

    <div v-if="order" class="row q-col-gutter-md">
      <!-- Left: summary + lines + timeline + shipments -->
      <div class="col-12 col-md-8">
        <AppSection title="Summary">
          <div class="row q-col-gutter-md text-body2">
            <div class="col-12 col-sm-6">
              <div class="text-caption text-grey-7">Customer</div>
              <div class="text-weight-medium">{{ order.contactName || '—' }}</div>
              <div class="text-grey-8">{{ order.contactEmail || '' }}</div>
              <div v-if="order.contactPhone" class="text-grey-8">{{ order.contactPhone }}</div>
            </div>
            <div class="col-12 col-sm-6">
              <div class="text-caption text-grey-7">Placed</div>
              <div>{{ formatDate(order.placedOnUtc) }}</div>
              <div class="text-caption text-grey-7 q-mt-sm">Payment</div>
              <q-badge :color="paymentStatusColor(order.paymentStatus)" :label="humanizeEnum(order.paymentStatus) || '—'" />
            </div>
            <div class="col-12 col-sm-6">
              <div class="text-caption text-grey-7">Fulfilment</div>
              <div>
                {{ order.isPickup ? 'Pickup in store' : 'Delivery' }}
                <span v-if="order.assignedStoreName" class="text-grey-8">· {{ order.assignedStoreName }}</span>
              </div>
            </div>
            <div v-if="order.shippingMethodName || order.shippingCarrier || order.trackingNumber" class="col-12 col-sm-6">
              <div class="text-caption text-grey-7">Shipping</div>
              <div>
                <span v-if="order.shippingMethodName">{{ order.shippingMethodName }}</span>
                <span v-if="order.shippingCarrier" class="text-grey-8"> · {{ order.shippingCarrier }}</span>
                <span v-if="!order.shippingMethodName && !order.shippingCarrier">—</span>
              </div>
              <div v-if="order.trackingNumber" class="text-grey-8">Tracking: {{ order.trackingNumber }}</div>
            </div>
            <div class="col-12">
              <div class="text-caption text-grey-7">Ship to</div>
              <div>{{ shipAddress }}</div>
            </div>
          </div>
        </AppSection>

        <AppSection title="Line items" class="q-mt-md">
          <q-markup-table flat dense>
            <thead>
              <tr>
                <th class="text-left">Product</th>
                <th class="text-left">SKU</th>
                <th class="text-right">Qty</th>
                <th class="text-right">Shipped</th>
                <th class="text-right">Unit</th>
                <th class="text-right">Total</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="l in order.lines" :key="l.id">
                <td>
                  {{ l.productName }}
                  <AppLineCustomAttributes :attributes="l.customAttributes" />
                </td>
                <td>{{ l.sku || '—' }}</td>
                <td class="text-right">{{ l.quantity }}</td>
                <td class="text-right">{{ shippedFor(l.id) }}</td>
                <td class="text-right">
                  <span
                    v-if="l.discountAmount > 0"
                    class="text-grey-6 q-mr-xs"
                    style="text-decoration: line-through"
                  >{{ formatMoney(l.originalUnitPrice) }}</span>
                  {{ formatMoney(l.unitPrice) }}
                </td>
                <td class="text-right">{{ formatMoney(l.lineTotal) }}</td>
              </tr>
            </tbody>
          </q-markup-table>
        </AppSection>

        <AppSection title="Payment" class="q-mt-md">
          <div v-if="!payments.length" class="text-grey-9">No payment records for this order.</div>
          <q-list v-else separator>
            <q-item v-for="p in payments" :key="p.id">
              <q-item-section>
                <q-item-label class="text-weight-medium">
                  {{ paymentMethodLabel(p.method) }}
                  <q-badge class="q-ml-sm" :color="paymentStatusColor(p.status)" :label="humanizeEnum(p.status)" />
                </q-item-label>
                <q-item-label v-if="p.transactionId || p.authorizationId || p.gatewayReference" caption class="text-grey-9">
                  <span v-if="p.transactionId" class="q-mb-xs">Txn :- {{ p.transactionId }}</span>
                  <span v-else-if="p.authorizationId" class="q-mb-xs">Auth :- {{ p.authorizationId }}</span><br />
                  <span v-if="p.gatewayReference" class="q-mb-xs">Ref :- {{ p.gatewayReference }}</span>
                </q-item-label>
                <q-item-label v-if="p.authorizedOnUtc || p.capturedOnUtc || p.refundedOnUtc" caption class="text-grey-9">
                  <span v-if="p.authorizedOnUtc" class="q-mb-xs">Authorized :- {{ formatDate(p.authorizedOnUtc) }}</span><br />
                  <span v-if="p.capturedOnUtc" class="q-mb-xs">Captured :- {{ formatDate(p.capturedOnUtc) }}</span><br />
                  <span v-if="p.refundedOnUtc" class="q-mb-xs">Refunded :- {{ formatDate(p.refundedOnUtc) }}</span>
                </q-item-label>
                <q-item-label v-if="p.status === 'Authorized' && p.authorizationExpiresUtc" caption class="text-orange-9 q-mb-xs">
                  Authorization expires {{ formatDate(p.authorizationExpiresUtc) }}
                </q-item-label>
                <q-item-label v-if="p.errorMessage" caption class="text-negative q-mb-xs">{{ p.errorMessage }}</q-item-label>
              </q-item-section>
              <q-item-section side top>
                <div class="text-weight-medium">{{ p.currencyCode }} {{ formatMoney(p.amount) }}</div>
                <div v-if="p.refundedAmount > 0" class="text-caption text-deep-orange">
                  −{{ formatMoney(p.refundedAmount) }} refunded
                </div>
              </q-item-section>
            </q-item>
          </q-list>
        </AppSection>

        <AppSection title="Shipments" class="q-mt-md">
          <template #actions>
            <q-btn v-if="canShipMore" color="primary" unelevated no-caps size="sm" icon="o_local_shipping" label="Create shipment" @click="openShipmentDialog" />
          </template>
          <div v-if="!shipments.length" class="text-grey-6">No shipments yet.</div>
          <q-list v-else separator>
            <q-item v-for="s in shipments" :key="s.id">
              <q-item-section>
                <q-item-label class="text-weight-medium">{{ s.shipmentNumber }} <q-badge class="q-ml-sm" :color="statusColor(s.status)" :label="s.status" /></q-item-label>
                <q-item-label caption>
                  {{ s.carrier }}<span v-if="s.serviceName"> · {{ s.serviceName }}</span>
                  <span v-if="s.trackingNumber"> · {{ s.trackingNumber }}</span>
                  · {{ s.lines.reduce((n, x) => n + x.quantity, 0) }} items
                </q-item-label>
              </q-item-section>
              <q-item-section side>
                <q-btn v-if="s.labelPdfUrl" flat dense no-caps size="sm" icon="o_download" label="Label" type="a" :href="s.labelPdfUrl" target="_blank" />
                <q-btn v-else flat dense no-caps size="sm" icon="o_label" label="Generate label" :loading="labelBusy[s.id]" @click="generateLabel(s)" />
              </q-item-section>
            </q-item>
          </q-list>
        </AppSection>

        <AppSection title="Timeline" class="q-mt-md">
          <div v-if="!history.length" class="text-grey-6">No status changes yet.</div>
          <q-timeline v-else color="primary">
            <q-timeline-entry
              v-for="h in history"
              :key="h.id"
              :title="`${h.fromStatus} → ${h.toStatus}`"
              :subtitle="formatDate(h.changedOnUtc, true)"
            >
              <div v-if="h.note" class="text-body2">{{ h.note }}</div>
            </q-timeline-entry>
          </q-timeline>
        </AppSection>
      </div>

      <!-- Right: totals + action panel -->
      <div class="col-12 col-md-4">
        <AppSection title="Order totals">
          <div class="text-body2">
            <div class="row justify-between q-py-xs">
              <span class="text-grey-7">Subtotal</span><span>{{ money(listSubtotal) }}</span>
            </div>
            <div v-if="order.groupDiscountTotal > 0" class="row justify-between q-py-xs">
              <span class="text-grey-7">Customer group discount</span>
              <span class="text-negative">−{{ money(order.groupDiscountTotal) }}</span>
            </div>
            <div v-if="order.discountTotal > 0" class="row justify-between q-py-xs">
              <span class="text-grey-7">Discount<span v-if="order.appliedCouponCode"> ({{ order.appliedCouponCode }})</span></span>
              <span class="text-negative">−{{ money(order.discountTotal) }}</span>
            </div>
            <div class="row justify-between q-py-xs">
              <span class="text-grey-7">Shipping</span><span>{{ money(order.shippingTotal) }}</span>
            </div>
            <div class="row justify-between q-py-xs">
              <span class="text-grey-7">Tax <q-badge v-if="order.taxFlaggedForReview" color="orange" label="review" class="q-ml-xs" /></span>
              <span>{{ money(order.taxTotal) }}</span>
            </div>
            <div v-if="order.paymentFeeTotal > 0" class="row justify-between q-py-xs">
              <span class="text-grey-7">Payment fee<span v-if="order.paymentFeePercent"> ({{ order.paymentFeePercent }}%)</span></span>
              <span>{{ money(order.paymentFeeTotal) }}</span>
            </div>
            <q-separator class="q-my-sm" />
            <div class="row justify-between items-center">
              <span class="text-weight-medium">Total</span>
              <span class="text-h6">{{ money(order.totalAmount) }}</span>
            </div>
          </div>
        </AppSection>

        <AppSection title="Actions" class="q-mt-md">
          <div class="column q-gutter-sm">
            <q-btn
              v-for="t in transitions"
              :key="t"
              :color="t === 'Cancelled' ? 'negative' : 'primary'"
              :outline="t === 'Cancelled'"
              unelevated
              no-caps
              :icon="transitionIcon(t)"
              :label="`Mark as ${t}`"
              @click="onTransition(t)"
            />
            <div v-if="!transitions.length" class="text-grey-6 text-caption">No further status changes available.</div>
            <q-separator class="q-my-sm" />
            <q-btn outline color="deep-orange" no-caps icon="o_currency_exchange" label="Refund" @click="openRefundDialog" />
          </div>
        </AppSection>
      </div>
    </div>

    <!-- Tracking dialog (Ship transition) -->
    <q-dialog v-model="trackingDialog.open">
      <q-card style="min-width: 380px">
        <q-card-section class="text-subtitle1 text-weight-medium">Mark as shipped</q-card-section>
        <q-card-section class="q-gutter-sm">
          <q-input v-model="trackingDialog.carrier" dense outlined label="Carrier" :rules="[(v) => !!v || 'Required']" />
          <q-input v-model="trackingDialog.trackingNumber" dense outlined label="Tracking number" :rules="[(v) => !!v || 'Required']" />
        </q-card-section>
        <q-card-actions align="right">
          <q-btn flat no-caps label="Cancel" v-close-popup />
          <q-btn color="primary" unelevated no-caps label="Ship" :loading="busy" @click="confirmShip" />
        </q-card-actions>
      </q-card>
    </q-dialog>

    <!-- Create shipment dialog -->
    <q-dialog v-model="shipmentDialog.open">
      <q-card style="min-width: 460px; max-width: 95vw">
        <q-card-section class="text-subtitle1 text-weight-medium">Create shipment</q-card-section>
        <q-card-section class="q-gutter-sm scroll" style="max-height: 60vh">
          <div v-for="row in shipmentDialog.lines" :key="row.orderLineItemId" class="row items-center q-col-gutter-sm">
            <div class="col">
              <div class="text-body2">{{ row.productName }}</div>
              <div class="text-caption text-grey-6">Remaining: {{ row.remaining }}</div>
            </div>
            <div class="col-4">
              <q-input v-model.number="row.quantity" type="number" dense outlined min="0" :max="row.remaining" />
            </div>
          </div>
          <q-separator class="q-my-sm" />
          <q-input v-model="shipmentDialog.carrier" dense outlined label="Carrier" :rules="[(v) => !!v || 'Required']" />
          <q-input v-model="shipmentDialog.trackingNumber" dense outlined label="Tracking number (optional)" />
          <q-input v-model="shipmentDialog.serviceName" dense outlined label="Service (optional)" />
        </q-card-section>
        <q-card-actions align="right">
          <q-btn flat no-caps label="Cancel" v-close-popup />
          <q-btn color="primary" unelevated no-caps label="Create shipment" :loading="busy" @click="confirmShipment" />
        </q-card-actions>
      </q-card>
    </q-dialog>

    <!-- Refund dialog -->
    <q-dialog v-model="refundDialog.open">
      <q-card style="min-width: 420px">
        <q-card-section class="text-subtitle1 text-weight-medium">Refund order</q-card-section>
        <q-card-section class="q-gutter-sm">
          <q-option-group v-model="refundDialog.mode" inline :options="[{ label: 'Full refund', value: 'full' }, { label: 'Partial refund', value: 'partial' }]" />
          <div v-if="refundDialog.mode === 'full'" class="text-caption text-grey-7">
            Refunds the full remaining balance and restocks every line item.
          </div>
          <template v-if="refundDialog.mode === 'partial'">
            <div class="text-caption text-grey-7">Choose the line items to refund — their value is refunded and their stock restocked.</div>
            <q-option-group
              v-model="refundDialog.lineIds"
              type="checkbox"
              :options="order.lines.map((l) => ({ label: `${l.productName} — ${formatMoney(l.lineTotal)}`, value: l.id }))"
            />
            <div class="text-body2 text-weight-medium">Refund total: {{ formatMoney(partialRefundTotal) }}</div>
          </template>
          <q-input v-model="refundDialog.reason" dense outlined label="Reason (optional)" />
        </q-card-section>
        <q-card-actions align="right">
          <q-btn flat no-caps label="Cancel" v-close-popup />
          <q-btn color="deep-orange" unelevated no-caps label="Process refund" :loading="busy" :disable="refundDialog.mode === 'partial' && !refundDialog.lineIds.length" @click="confirmRefund" />
        </q-card-actions>
      </q-card>
    </q-dialog>

    <AppRecordMeta entity-type="order" :record-id="order?.id" />
  </q-page>
</template>

<script setup>
/*
 * Admin order detail (WO-114): summary, line items (with shipped counts), status
 * timeline, context-aware status actions, invoice/packing-slip download, partial
 * shipments (create + label), and refunds — all against the existing admin APIs.
 */
import { ref, reactive, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useQuasar } from 'quasar'
import { getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'
import {
  orderApi, orderStatusColor as statusColor, allowedTransitions, formatMoney, formatDate,
  paymentStatusColor, paymentMethodLabel, humanizeEnum
} from 'modules/orders/api'

const route = useRoute()
const router = useRouter()
const $q = useQuasar()
const notify = useNotify()

const order = ref(null)
const history = ref([])
const shipments = ref([])
const payments = ref([])
const loading = ref(false)
const busy = ref(false)
const labelBusy = reactive({})

const trackingDialog = reactive({ open: false, carrier: '', trackingNumber: '' })
const shipmentDialog = reactive({ open: false, lines: [], carrier: '', trackingNumber: '', serviceName: '' })
const refundDialog = reactive({ open: false, mode: 'full', lineIds: [], reason: '' })

// The value refunded/restocked by a partial refund = the sum of the chosen line items' totals.
const partialRefundTotal = computed(() => {
  const o = order.value
  if (!o) return 0
  const ids = new Set(refundDialog.lineIds)
  return o.lines.filter((l) => ids.has(l.id)).reduce((sum, l) => sum + (l.lineTotal || 0), 0)
})

const transitions = computed(() => (order.value ? allowedTransitions(order.value.status) : []))
const shipAddress = computed(() => {
  const o = order.value
  if (!o) return '—'
  return [o.addressLine1, o.addressLine2, o.city, o.stateProvince || o.region, o.postalCode, o.countryCode].filter(Boolean).join(', ') || '—'
})
const canShipMore = computed(() => (order.value?.lines || []).some((l) => remainingFor(l.id) > 0))
// The list-price subtotal = charged subtotal + the group saving that was folded into the unit prices.
const listSubtotal = computed(() => {
  const o = order.value
  return o ? (o.subtotal || 0) + (o.groupDiscountTotal || 0) : 0
})

// Money with the order's currency code, e.g. "USD 42.00".
function money (value) {
  return `${order.value?.currencyCode || ''} ${formatMoney(value)}`.trim()
}

function shippedFor (lineId) {
  return shipments.value.reduce((sum, s) => sum + s.lines.filter((x) => x.orderLineItemId === lineId).reduce((n, x) => n + x.quantity, 0), 0)
}
function remainingFor (lineId) {
  const line = (order.value?.lines || []).find((l) => l.id === lineId)
  return line ? line.quantity - shippedFor(lineId) : 0
}

function transitionIcon (t) {
  return { Processing: 'o_settings', Shipped: 'o_local_shipping', Delivered: 'o_check_circle', Cancelled: 'o_cancel' }[t] || 'o_arrow_forward'
}

async function load () {
  loading.value = true
  try {
    const [o, h, s, p] = await Promise.all([
      orderApi.get(route.params.id),
      orderApi.history(route.params.id).catch(() => []),
      orderApi.shipments(route.params.id).catch(() => []),
      orderApi.payments(route.params.id).catch(() => [])
    ])
    order.value = o
    history.value = Array.isArray(h) ? h : []
    shipments.value = Array.isArray(s) ? s : []
    payments.value = Array.isArray(p) ? p : []
  } catch (err) {
    notify.error(getApiErrorMessage(err))
    order.value = null
  } finally {
    loading.value = false
  }
}

function onTransition (t) {
  if (t === 'Shipped') {
    trackingDialog.carrier = ''
    trackingDialog.trackingNumber = ''
    trackingDialog.open = true
    return
  }
  if (t === 'Cancelled') {
    $q.dialog({ title: 'Cancel order', message: 'Cancel this order? Any authorized payment will be released.', cancel: true, ok: { label: 'Cancel order', color: 'negative' } })
      .onOk(() => advance(t))
    return
  }
  advance(t)
}

async function advance (toStatus, extra = {}) {
  busy.value = true
  try {
    await orderApi.advance(route.params.id, { toStatus, ...extra })
    notify.success(`Order marked ${toStatus}`)
    trackingDialog.open = false
    await load()
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    busy.value = false
  }
}

function confirmShip () {
  if (!trackingDialog.carrier || !trackingDialog.trackingNumber) { notify.warning('Carrier and tracking number are required'); return }
  advance('Shipped', { carrier: trackingDialog.carrier, trackingNumber: trackingDialog.trackingNumber })
}

function openShipmentDialog () {
  shipmentDialog.lines = (order.value.lines || [])
    .map((l) => ({ orderLineItemId: l.id, productName: l.productName, remaining: remainingFor(l.id), quantity: remainingFor(l.id) }))
    .filter((r) => r.remaining > 0)
  shipmentDialog.carrier = ''
  shipmentDialog.trackingNumber = ''
  shipmentDialog.serviceName = ''
  shipmentDialog.open = true
}

async function confirmShipment () {
  const lines = shipmentDialog.lines.filter((r) => r.quantity > 0).map((r) => ({ orderLineItemId: r.orderLineItemId, quantity: r.quantity }))
  if (!lines.length) { notify.warning('Select at least one item to ship'); return }
  if (!shipmentDialog.carrier) { notify.warning('Carrier is required'); return }
  busy.value = true
  try {
    await orderApi.createShipment(route.params.id, {
      lines, carrier: shipmentDialog.carrier, trackingNumber: shipmentDialog.trackingNumber || null, serviceName: shipmentDialog.serviceName || null
    })
    notify.success('Shipment created')
    shipmentDialog.open = false
    await load()
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    busy.value = false
  }
}

async function generateLabel (s) {
  labelBusy[s.id] = true
  try {
    await orderApi.generateLabel(s.id)
    notify.success('Label generated')
    await load()
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    labelBusy[s.id] = false
  }
}

function openRefundDialog () {
  refundDialog.mode = 'full'
  refundDialog.lineIds = []
  refundDialog.reason = ''
  refundDialog.open = true
}

async function confirmRefund () {
  const payload = { reason: refundDialog.reason || null }
  // 'partial' → send the chosen line ids; the backend refunds their value and restocks those lines.
  // 'full' → send neither → the backend refunds the full remaining balance and restocks every line.
  if (refundDialog.mode === 'partial') payload.orderLineItemIds = refundDialog.lineIds
  busy.value = true
  try {
    await orderApi.refund(route.params.id, payload)
    notify.success('Refund processed')
    refundDialog.open = false
    await load()
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    busy.value = false
  }
}

function downloadInvoice () { orderApi.invoice(route.params.id).catch((e) => notify.error(getApiErrorMessage(e))) }
function downloadPackingSlip () { orderApi.packingSlip(route.params.id).catch((e) => notify.error(getApiErrorMessage(e))) }

onMounted(load)
</script>
