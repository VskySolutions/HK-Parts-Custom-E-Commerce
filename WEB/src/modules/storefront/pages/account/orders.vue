<template>
  <q-card flat bordered>
    <q-card-section class="text-subtitle1 text-weight-medium">Order history</q-card-section>
    <q-separator />

    <q-table
      flat
      :rows="rows"
      :columns="columns"
      row-key="id"
      :loading="loading"
      :pagination="pagination"
      :rows-per-page-options="[10, 20, 50]"
      no-data-label="You haven't placed any orders yet."
      @request="onRequest"
    >
      <template #body-cell-status="props">
        <q-td :props="props">
          <q-badge :color="statusColor(props.value)" :label="props.value" />
        </q-td>
      </template>
      <template #body-cell-total="props">
        <q-td :props="props" class="text-weight-medium">{{ formatPrice(props.value) }}</q-td>
      </template>
      <template #body-cell-placedOnUtc="props">
        <q-td :props="props">{{ formatDate(props.value) }}</q-td>
      </template>
      <template #body-cell-actions="props">
        <q-td :props="props" class="text-right">
          <q-btn flat round dense color="primary" icon="o_visibility" @click="view(props.row)">
            <q-tooltip>View details</q-tooltip>
          </q-btn>
          <q-btn
            flat
            round
            dense
            color="primary"
            icon="o_download"
            :loading="downloadingId === props.row.id"
            @click="downloadInvoice(props.row)"
          >
            <q-tooltip>Download invoice</q-tooltip>
          </q-btn>
        </q-td>
      </template>
    </q-table>

    <!-- Order details dialog -->
    <q-dialog v-model="detail.open">
      <q-card style="width: 820px; max-width: 96vw">
        <q-card-section class="row items-start justify-between q-gutter-sm">
          <div>
            <div class="text-subtitle1 text-weight-bold">Order {{ detail.data ? detail.data.orderNumber : '' }}</div>
            <div v-if="detail.data" class="text-caption text-grey-6">Placed {{ formatDate(detail.data.placedOnUtc) }}</div>
          </div>
          <div v-if="detail.data" class="column items-end q-gutter-xs">
            <q-badge :color="statusColor(detail.data.status)" :label="`Order: ${humanize(detail.data.status)}`" />
            <q-badge
              outline
              :color="paymentStatusColor(detail.data.paymentStatus)"
              :label="`Payment: ${humanize(detail.data.paymentStatus)}`"
            />
          </div>
        </q-card-section>
        <q-separator />

        <q-card-section class="scroll" style="max-height: 74vh">
          <q-inner-loading :showing="detail.loading" />
          <template v-if="detail.data">
            <!-- Order progress timeline -->
            <div v-if="detail.timeline.length" class="q-mb-md">
              <div class="text-body2 text-weight-medium q-mb-sm">Order progress</div>
              <q-timeline color="primary" layout="dense" class="q-my-none q-pl-xs">
                <q-timeline-entry
                  v-for="h in detail.timeline"
                  :key="h.id"
                  :title="humanize(h.toStatus)"
                  :subtitle="formatDate(h.changedOnUtc)"
                  :icon="statusIcon(h.toStatus)"
                  :color="statusColor(h.toStatus)"
                >
                  <div v-if="h.note" class="text-caption text-grey-7">{{ h.note }}</div>
                </q-timeline-entry>
              </q-timeline>
            </div>

            <!-- Fulfilment + payment at a glance -->
            <div class="row q-col-gutter-md q-mb-md">
              <div class="col-12 col-sm-6">
                <div class="text-body2 text-weight-medium q-mb-xs">{{ detail.data.isPickup ? 'Pickup' : 'Shipping' }}</div>
                <div class="text-body2 text-grey-8">
                  <div v-if="detail.data.isPickup" class="row items-center no-wrap">
                    <q-icon name="o_storefront" size="18px" class="q-mr-xs" />
                    <span>Pick up in store<span v-if="detail.data.assignedStoreName"> · {{ detail.data.assignedStoreName }}</span></span>
                  </div>
                  <div v-else class="row items-center no-wrap">
                    <q-icon name="o_local_shipping" size="18px" class="q-mr-xs" />
                    <span>
                      {{ detail.data.shippingMethodName || 'Standard delivery' }}
                      <span v-if="detail.data.shippingCarrier" class="text-grey-6"> · {{ detail.data.shippingCarrier }}</span>
                    </span>
                  </div>
                  <div v-if="detail.data.trackingNumber" class="row items-center no-wrap q-mt-xs">
                    <span>Tracking: <span class="text-weight-medium">{{ detail.data.trackingNumber }}</span></span>
                    <q-btn flat dense round size="sm" icon="o_content_copy" class="q-ml-xs" @click="copy(detail.data.trackingNumber)">
                      <q-tooltip>Copy tracking number</q-tooltip>
                    </q-btn>
                  </div>
                  <div v-if="detail.data.shippedOnUtc" class="text-caption text-grey-6 q-mt-xs">
                    Shipped {{ formatDate(detail.data.shippedOnUtc) }}
                  </div>
                  <div v-if="detail.data.deliveredOnUtc" class="text-caption text-grey-6">
                    Delivered {{ formatDate(detail.data.deliveredOnUtc) }}
                  </div>
                </div>
              </div>
              <div class="col-12 col-sm-6">
                <div class="text-body2 text-weight-medium q-mb-xs">Payment</div>
                <div class="text-body2 text-grey-8">
                  <div class="row items-center no-wrap">
                    <q-icon name="o_payments" size="18px" class="q-mr-xs" />
                    <span>{{ paymentMethodLabel(detail.data.paymentMethod) }}</span>
                  </div>
                  <div class="q-mt-xs">
                    Payment status:
                    <q-badge :color="paymentStatusColor(detail.data.paymentStatus)" :label="humanize(detail.data.paymentStatus)" />
                  </div>
                  <div v-if="detail.data.paymentTransactionId" class="q-mt-xs sf-txn-id">
                    Txn ID: <span class="text-weight-medium">{{ detail.data.paymentTransactionId }}</span>
                    <q-btn
                      flat dense round size="sm" icon="o_content_copy" class="q-ml-xs"
                      @click="copy(detail.data.paymentTransactionId)"
                    >
                      <q-tooltip>Copy transaction ID</q-tooltip>
                    </q-btn>
                  </div>
                </div>
              </div>
            </div>

            <!-- Contact + delivery address -->
            <div class="q-mb-md">
              <div class="text-body2 text-weight-medium q-mb-xs">{{ detail.data.isPickup ? 'Contact' : 'Delivery address' }}</div>
              <div class="text-body2 text-grey-8">
                <div v-if="detail.data.contactName">{{ detail.data.contactName }}</div>
                <div v-if="!detail.data.isPickup && addressLine(detail.data)">{{ addressLine(detail.data) }}</div>
                <div v-if="detail.data.contactPhone" class="text-grey-6">{{ detail.data.contactPhone }}</div>
                <div v-if="detail.data.contactEmail" class="text-grey-6">{{ detail.data.contactEmail }}</div>
              </div>
            </div>

            <!-- Line items -->
            <div class="text-body2 text-weight-medium q-mb-xs">Items</div>
            <q-markup-table flat bordered dense class="q-mb-md">
              <thead>
                <tr>
                  <th class="text-left">Product</th>
                  <th class="text-center">Qty</th>
                  <th class="text-right">Unit price</th>
                  <th class="text-right">Total</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="line in detail.data.lines" :key="line.id">
                  <td class="text-left">
                    <router-link
                      v-if="line.productAvailable"
                      :to="{ name: 'shop-product', params: { idOrSlug: line.productId } }"
                      class="sf-product-link"
                    >{{ line.productName }}</router-link>
                    <span v-else>{{ line.productName }}</span>
                    <div v-if="line.sku" class="text-caption text-grey-6">SKU: {{ line.sku }}</div>
                    <AppLineCustomAttributes :attributes="line.customAttributes" />
                  </td>
                  <td class="text-center">{{ line.quantity }}</td>
                  <td class="text-right">
                    <span
                      v-if="line.discountAmount > 0"
                      class="text-grey-6 q-mr-xs"
                      style="text-decoration: line-through"
                    >{{ money(line.originalUnitPrice) }}</span>
                    {{ money(line.unitPrice) }}
                  </td>
                  <td class="text-right">{{ money(line.lineTotal) }}</td>
                </tr>
              </tbody>
            </q-markup-table>

            <!-- Totals: list-price subtotal, then the itemized group saving + any coupon discount. -->
            <div class="q-ml-auto" style="max-width: 340px">
              <div class="row justify-between q-py-xs text-body2">
                <span class="text-grey-7">Subtotal</span>
                <span>{{ money(listSubtotal(detail.data)) }}</span>
              </div>
              <div
                v-if="detail.data.groupDiscountTotal > 0"
                class="row justify-between q-py-xs text-body2 text-green-8"
              >
                <span>Customer group discount</span>
                <span>−{{ money(detail.data.groupDiscountTotal) }}</span>
              </div>
              <div
                v-if="detail.data.discountTotal > 0"
                class="row justify-between q-py-xs text-body2 text-green-8"
              >
                <span>Discount<span v-if="detail.data.appliedCouponCode"> ({{ detail.data.appliedCouponCode }})</span></span>
                <span>−{{ money(detail.data.discountTotal) }}</span>
              </div>
              <div class="row justify-between q-py-xs text-body2">
                <span class="text-grey-7">{{ detail.data.isPickup ? 'Pickup' : 'Shipping' }}</span>
                <span>{{ money(detail.data.shippingTotal) }}</span>
              </div>
              <div class="row justify-between q-py-xs text-body2">
                <span class="text-grey-7">Tax</span>
                <span>{{ money(detail.data.taxTotal) }}</span>
              </div>
              <div
                v-if="detail.data.paymentFeeTotal > 0"
                class="row justify-between q-py-xs text-body2"
              >
                <span class="text-grey-7">Payment fee<span v-if="detail.data.paymentFeePercent"> ({{ detail.data.paymentFeePercent }}%)</span></span>
                <span>{{ money(detail.data.paymentFeeTotal) }}</span>
              </div>
              <q-separator class="q-my-xs" />
              <div class="row justify-between items-center">
                <span class="text-body1 text-weight-medium">Total</span>
                <span class="text-h6 text-weight-bold">{{ money(detail.data.totalAmount) }}</span>
              </div>
            </div>
          </template>
        </q-card-section>

        <q-separator />
        <q-card-actions align="right">
          <q-btn flat no-caps label="Close" v-close-popup />
          <q-btn
            color="primary"
            unelevated
            no-caps
            icon="o_download"
            label="Download invoice"
            :loading="detail.data && downloadingId === detail.data.id"
            :disable="!detail.data"
            @click="downloadInvoice(detail.data)"
          />
        </q-card-actions>
      </q-card>
    </q-dialog>
  </q-card>
</template>

<script setup>
import { ref, reactive, onMounted } from 'vue'
import { useQuasar } from 'quasar'
import { accountApi } from 'modules/storefront/account-api'
import { formatPrice } from 'modules/storefront/api'
import { formatDateTime as formatDate } from 'src/utils/datetime'
import { getApiErrorMessage } from 'services/api'

const $q = useQuasar()

const rows = ref([])
const loading = ref(false)
const downloadingId = ref(null)
const pagination = ref({ page: 1, rowsPerPage: 20, rowsNumber: 0 })
const detail = reactive({ open: false, loading: false, data: null, timeline: [] })

const columns = [
  { name: 'orderNumber', label: 'Order #', field: 'orderNumber', align: 'left' },
  { name: 'placedOnUtc', label: 'Date', field: 'placedOnUtc', align: 'left' },
  { name: 'itemCount', label: 'Items', field: 'itemCount', align: 'center' },
  { name: 'total', label: 'Total', field: 'totalAmount', align: 'right' },
  { name: 'status', label: 'Status', field: 'status', align: 'left' },
  { name: 'actions', label: 'Actions', field: 'id', align: 'right' }
]

// Money in the order's own currency (orders carry a currencyCode, unlike the catalogue DTOs).
function money (v) {
  return formatPrice(v, detail.data?.currencyCode || 'USD')
}

// List-price subtotal = charged subtotal + the group saving folded into the unit prices.
function listSubtotal (o) {
  return (o?.subtotal || 0) + (o?.groupDiscountTotal || 0)
}

function addressLine (o) {
  return [o.addressLine1, o.addressLine2, o.landmark, o.city, o.stateProvince || o.region, o.postalCode, o.countryCode]
    .filter(Boolean)
    .join(', ')
}

// Split an enum name into words (CashOnDelivery → "Cash On Delivery"); single-word statuses pass through.
function humanize (s) {
  if (!s) return '—'
  return String(s).replace(/([a-z0-9])([A-Z])/g, '$1 $2')
}

const PAYMENT_METHOD_LABELS = {
  CashOnDelivery: 'Cash on delivery',
  BankTransfer: 'Bank transfer',
  PayPal: 'PayPal',
  Stripe: 'Stripe',
  Razorpay: 'Razorpay',
  Square: 'Square',
  AuthorizeNet: 'Authorize.Net'
}
function paymentMethodLabel (m) {
  if (!m) return 'Not recorded'
  return PAYMENT_METHOD_LABELS[m] || humanize(m)
}

function statusColor (status) {
  const s = (status || '').toLowerCase()
  if (s.includes('cancel')) return 'negative'
  if (s.includes('deliver') || s.includes('complete')) return 'positive'
  if (s.includes('ship')) return 'teal'
  if (s.includes('process') || s.includes('pending')) return 'orange'
  return 'grey'
}

function statusIcon (status) {
  const s = (status || '').toLowerCase()
  if (s.includes('cancel')) return 'o_cancel'
  if (s.includes('deliver') || s.includes('complete')) return 'o_check_circle'
  if (s.includes('ship')) return 'o_local_shipping'
  if (s.includes('process')) return 'o_settings'
  return 'o_receipt_long'
}

function paymentStatusColor (status) {
  const s = (status || '').toLowerCase()
  if (s.includes('captur') || s.includes('paid')) return 'positive'
  if (s.includes('refund')) return 'deep-orange'
  if (s.includes('fail') || s.includes('declin')) return 'negative'
  if (s.includes('authoriz')) return 'blue'
  return 'orange'
}

async function copy (text) {
  try {
    await navigator.clipboard.writeText(text)
    $q.notify({ type: 'positive', message: 'Copied to clipboard', timeout: 1200 })
  } catch (e) {
    // Clipboard unavailable (insecure context / denied) — non-fatal.
  }
}

async function fetchPage (page, rowsPerPage) {
  loading.value = true
  try {
    const data = await accountApi.orders({ page, pageSize: rowsPerPage })
    rows.value = Array.isArray(data?.items) ? data.items : []
    pagination.value = {
      page: data?.pageNumber ?? page,
      rowsPerPage,
      rowsNumber: data?.totalCount ?? rows.value.length
    }
  } catch (e) {
    $q.notify({ type: 'negative', message: getApiErrorMessage(e) })
  } finally {
    loading.value = false
  }
}

function onRequest (props) {
  const { page, rowsPerPage } = props.pagination
  fetchPage(page, rowsPerPage)
}

async function view (row) {
  detail.open = true
  detail.loading = true
  detail.data = null
  detail.timeline = []
  try {
    // Detail + timeline together; the timeline degrades to empty rather than failing the dialog.
    const [data, timeline] = await Promise.all([
      accountApi.getOrder(row.id),
      accountApi.orderTimeline(row.id).catch(() => [])
    ])
    detail.data = data
    detail.timeline = Array.isArray(timeline) ? timeline : []
  } catch (e) {
    $q.notify({ type: 'negative', message: getApiErrorMessage(e) })
  } finally {
    detail.loading = false
  }
}

async function downloadInvoice (row) {
  if (!row) return
  downloadingId.value = row.id
  try {
    await accountApi.downloadInvoice(row.id)
  } catch (e) {
    $q.notify({ type: 'negative', message: getApiErrorMessage(e) })
  } finally {
    downloadingId.value = null
  }
}

onMounted(() => fetchPage(pagination.value.page, pagination.value.rowsPerPage))
</script>

<style scoped lang="scss">
.sf-product-link {
  color: var(--q-primary);
  text-decoration: none;
  font-weight: 500;

  &:hover {
    text-decoration: underline;
  }
}

// The transaction id is a long opaque string — let it wrap rather than stretch the dialog.
.sf-txn-id {
  word-break: break-all;
}
</style>
