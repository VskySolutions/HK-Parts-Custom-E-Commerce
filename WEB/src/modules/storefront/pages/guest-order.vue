<template>
  <q-page class="q-pa-md storefront-container">
    <q-inner-loading :showing="loading" color="primary" />

    <!-- Invalid / expired link, or the order now lives in an account -->
    <q-card v-if="!loading && error" flat bordered class="q-mx-auto sf-guest-card">
      <q-card-section class="text-center q-pa-xl">
        <q-icon name="o_link_off" color="grey-6" size="56px" />
        <div class="text-h6 text-weight-bold q-mt-md">We couldn't open this order</div>
        <div class="text-body2 text-grey-8 q-mt-sm" role="alert">{{ error }}</div>
        <div class="row justify-center q-gutter-sm q-mt-lg">
          <q-btn unelevated color="primary" no-caps label="Look up an order" :to="{ name: 'shop-order-lookup' }" />
          <q-btn flat color="primary" no-caps label="Sign in" :to="{ path: '/auth/login', query: { redirect: '/shop/account/orders' } }" />
        </div>
      </q-card-section>
    </q-card>

    <template v-else-if="order">
      <div class="row items-start justify-between q-mb-md q-gutter-sm">
        <div>
          <h1 class="text-h5 text-weight-bold q-my-none">Order {{ order.orderNumber }}</h1>
          <div class="text-caption text-grey-7">Placed {{ formatDate(order.placedOnUtc) }} · receipt sent to {{ view.email }}</div>
        </div>
        <div class="column items-end q-gutter-xs">
          <q-badge :color="statusColor(order.status)" :label="`Order: ${humanize(order.status)}`" />
          <q-badge outline :color="paymentStatusColor(order.paymentStatus)" :label="`Payment: ${humanize(order.paymentStatus)}`" />
        </div>
      </div>

      <div class="row q-col-gutter-lg">
        <div class="col-12 col-md-8">
          <q-card flat bordered>
            <q-card-section>
              <div class="row q-col-gutter-md q-mb-md">
                <div class="col-12 col-sm-6">
                  <div class="text-body2 text-weight-medium q-mb-xs">{{ order.isPickup ? 'Pickup' : 'Shipping' }}</div>
                  <div class="text-body2 text-grey-8">
                    <div v-if="order.isPickup">Pick up in store<span v-if="order.assignedStoreName"> · {{ order.assignedStoreName }}</span></div>
                    <div v-else>{{ order.shippingMethodName || 'Standard delivery' }}<span v-if="order.shippingCarrier" class="text-grey-6"> · {{ order.shippingCarrier }}</span></div>
                    <div v-for="t in trackingNumbers" :key="t" class="q-mt-xs">Tracking: <span class="text-weight-medium">{{ t }}</span></div>
                    <div v-if="order.shippedOnUtc" class="text-caption text-grey-6 q-mt-xs">Shipped {{ formatDate(order.shippedOnUtc) }}</div>
                    <div v-if="order.deliveredOnUtc" class="text-caption text-grey-6">Delivered {{ formatDate(order.deliveredOnUtc) }}</div>
                  </div>
                </div>
                <div class="col-12 col-sm-6">
                  <div class="text-body2 text-weight-medium q-mb-xs">{{ order.isPickup ? 'Contact' : 'Delivery address' }}</div>
                  <div class="text-body2 text-grey-8">
                    <div v-if="order.contactName">{{ order.contactName }}</div>
                    <div v-if="!order.isPickup && addressLine(order)">{{ addressLine(order) }}</div>
                    <div v-if="order.contactPhone" class="text-grey-6">{{ order.contactPhone }}</div>
                  </div>
                </div>
                <div v-if="fflDealer" class="col-12 col-sm-6">
                  <div class="text-body2 text-weight-medium q-mb-xs">FFL dealer</div>
                  <div class="text-body2 text-grey-8">
                    <div>{{ fflDealer.fflDealerName }}</div>
                    <div>{{ [fflDealer.fflDealerStreet, fflDealer.fflDealerCity, fflDealer.fflDealerState, fflDealer.fflDealerZip].filter(Boolean).join(', ') }}</div>
                  </div>
                </div>
                <div v-if="order.billingAddress" class="col-12 col-sm-6">
                  <div class="text-body2 text-weight-medium q-mb-xs">Billing address</div>
                  <div class="text-body2 text-grey-8">
                    <div>{{ [order.billingAddress.firstName, order.billingAddress.lastName].filter(Boolean).join(' ') }}</div>
                    <div>{{ addressLine(order.billingAddress) }}</div>
                  </div>
                </div>
              </div>

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
                  <tr v-for="line in order.lines" :key="line.id">
                    <td class="text-left">
                      <router-link v-if="line.productAvailable" :to="{ name: 'shop-product', params: { idOrSlug: line.productId } }">{{ line.productName }}</router-link>
                      <span v-else>{{ line.productName }}</span>
                      <div v-if="line.sku" class="text-caption text-grey-6">SKU: {{ line.sku }}</div>
                      <AppLineCustomAttributes :attributes="line.customAttributes" />
                    </td>
                    <td class="text-center">{{ line.quantity }}</td>
                    <td class="text-right">{{ money(line.unitPrice) }}</td>
                    <td class="text-right">{{ money(line.lineTotal) }}</td>
                  </tr>
                </tbody>
              </q-markup-table>

              <div class="q-ml-auto" style="max-width: 340px">
                <div class="row justify-between q-py-xs text-body2"><span class="text-grey-7">Subtotal</span><span>{{ money((order.subtotal || 0) + (order.groupDiscountTotal || 0)) }}</span></div>
                <div v-if="order.groupDiscountTotal > 0" class="row justify-between q-py-xs text-body2 text-green-8"><span>Customer group discount</span><span>−{{ money(order.groupDiscountTotal) }}</span></div>
                <div v-if="order.discountTotal > 0" class="row justify-between q-py-xs text-body2 text-green-8">
                  <span>Discount<span v-if="order.appliedCouponCode"> ({{ order.appliedCouponCode }})</span></span><span>−{{ money(order.discountTotal) }}</span>
                </div>
                <div class="row justify-between q-py-xs text-body2"><span class="text-grey-7">{{ order.isPickup ? 'Pickup' : 'Shipping' }}</span><span>{{ money(order.shippingTotal) }}</span></div>
                <div class="row justify-between q-py-xs text-body2"><span class="text-grey-7">Tax</span><span>{{ money(order.taxTotal) }}</span></div>
                <div v-if="order.paymentFeeTotal > 0" class="row justify-between q-py-xs text-body2"><span class="text-grey-7">Payment fee</span><span>{{ money(order.paymentFeeTotal) }}</span></div>
                <div v-if="order.giftCertificateAppliedAmount > 0" class="row justify-between q-py-xs text-body2"><span class="text-grey-7">Gift certificate</span><span>−{{ money(order.giftCertificateAppliedAmount) }}</span></div>
                <q-separator class="q-my-xs" />
                <div class="row justify-between q-py-xs text-subtitle1 text-weight-bold"><span>Total</span><span>{{ money(order.totalAmount) }}</span></div>
              </div>
            </q-card-section>
          </q-card>
          <!-- U-63: cancel (before it ships) or return (under the returns policy) without an account. -->
          <q-banner v-if="view.cancelRequestedOnUtc" dense rounded class="bg-orange-1 text-orange-10 q-mt-md" role="status">
            <template #avatar><q-icon name="o_hourglass_top" color="orange-10" /></template>
            Cancellation requested on {{ formatDate(view.cancelRequestedOnUtc) }}. We'll email you once it's done.
          </q-banner>

          <q-card v-if="view.returns && view.returns.length" flat bordered class="q-mt-md">
            <q-card-section class="q-pb-sm text-body2 text-weight-medium">Your returns</q-card-section>
            <q-list dense separator>
              <q-item v-for="r in view.returns" :key="r.rmaNumber">
                <q-item-section>
                  <q-item-label>{{ r.rmaNumber }}</q-item-label>
                  <q-item-label caption>Requested {{ formatDate(r.createdOnUtc) }}</q-item-label>
                </q-item-section>
                <q-item-section side><q-badge outline color="primary" :label="humanize(r.status)" /></q-item-section>
              </q-item>
            </q-list>
            <q-card-section class="text-caption text-grey-7 q-pt-sm">
              We email you at every step. Reply to those emails if we ask for anything.
            </q-card-section>
          </q-card>

          <div class="row q-gutter-sm q-mt-md">
            <q-btn
              v-if="view.canRequestReturn"
              outline
              no-caps
              color="primary"
              icon="o_assignment_return"
              label="Request a return"
              :to="{ name: 'shop-guest-return', query: { token } }"
            />
            <q-btn
              v-if="view.canRequestCancel"
              outline
              no-caps
              color="negative"
              icon="o_cancel"
              label="Cancel order"
              @click="openCancel"
            />
          </div>
          <div v-if="!view.canRequestReturn && !view.canRequestCancel && !view.cancelRequestedOnUtc" class="text-caption text-grey-7 q-mt-sm">
            Need help with this order? Reply to your order confirmation email or contact us with your order number.
          </div>
        </div>

        <!-- Guest → account (U-70 / U-72) -->
        <div class="col-12 col-md-4">
          <GuestCreateAccountCard :token="token" :email="view.email" :account-exists="view.accountExists" />
        </div>
      </div>

      <!-- U-63: cancel request (staff confirm it and void or refund the payment). -->
      <q-dialog v-model="cancelOpen" persistent>
        <q-card style="width: 440px; max-width: 95vw">
          <q-card-section class="text-h6">Cancel order {{ order.orderNumber }}?</q-card-section>
          <q-card-section class="q-pt-none">
            <div class="text-body2 text-grey-8 q-mb-md">
              We'll stop the order before it ships and email you when it's cancelled. Any payment taken is refunded to
              the original payment method.
            </div>
            <AppSelect
              label="Why are you cancelling?"
              required
              :options="cancelReasons.map((r) => ({ label: r, value: r }))"
              :model-value="cancelReason"
              :v="cancelReasonV"
              @update:model-value="cancelReason = $event"
            />
            <AppTextField
              class="q-mt-sm"
              label="Anything else? (optional)"
              type="textarea"
              autogrow
              maxlength="390"
              :model-value="cancelComment"
              @update:model-value="cancelComment = $event"
            />
            <div v-if="cancelError" class="text-negative text-body2 q-mt-sm" role="alert">{{ cancelError }}</div>
          </q-card-section>
          <q-card-actions align="right">
            <q-btn flat no-caps label="Keep my order" :disable="cancelling" v-close-popup />
            <q-btn unelevated no-caps color="negative" label="Request cancellation" :loading="cancelling" @click="submitCancel" />
          </q-card-actions>
        </q-card>
      </q-dialog>
    </template>
  </q-page>
</template>

<script setup>
/*
 * Read-only guest order page (docs/guest-checkout-requirements.md U-60/U-61): opened from the signed link in an
 * order email or from the lookup form. Shows status, items, totals, addresses and tracking. The side card offers
 * account creation (U-70) or, when the email already has an account, sign in (U-72).
 */
import { ref, computed, watch } from 'vue'
import { useRoute } from 'vue-router'
import { guestOrderApi, formatPrice } from 'modules/storefront/api'
import { formatDateTime as formatDate } from 'src/utils/datetime'
import { getApiErrorMessage } from 'services/api'
import GuestCreateAccountCard from 'modules/storefront/components/GuestCreateAccountCard.vue'

const route = useRoute()
const token = computed(() => String(route.query.token || ''))
const loading = ref(true)
const error = ref('')
const view = ref(null)
const order = computed(() => view.value?.order || null)

const trackingNumbers = computed(() => {
  const o = order.value
  if (!o) return []
  const all = [o.trackingNumber, ...(o.shippings || []).map((s) => s.trackingNumber)].filter(Boolean)
  return [...new Set(all)]
})
const fflDealer = computed(() => (order.value?.shippings || []).find((s) => s.containsFflItems && s.fflDealerName) || null)

function money (v) {
  return formatPrice(v, order.value?.currencyCode || 'USD')
}

function addressLine (o) {
  return [o.addressLine1, o.addressLine2, o.landmark, o.city, o.stateProvince || o.region, o.postalCode, o.countryCode]
    .filter(Boolean)
    .join(', ')
}

function humanize (s) {
  return s ? String(s).replace(/([a-z0-9])([A-Z])/g, '$1 $2') : '—'
}

function statusColor (status) {
  const s = (status || '').toLowerCase()
  if (s.includes('cancel')) return 'negative'
  if (s.includes('deliver') || s.includes('complete')) return 'positive'
  if (s.includes('ship')) return 'teal'
  if (s.includes('process') || s.includes('pending')) return 'orange'
  return 'grey'
}

function paymentStatusColor (status) {
  const s = (status || '').toLowerCase()
  if (s.includes('captur') || s.includes('paid')) return 'positive'
  if (s.includes('refund')) return 'deep-orange'
  if (s.includes('fail') || s.includes('declin')) return 'negative'
  if (s.includes('authoriz')) return 'blue'
  return 'orange'
}

// ---- U-63: cancel request ----
const cancelOpen = ref(false)
const cancelReasons = ref([])
const cancelReason = ref(null)
const cancelComment = ref('')
const cancelling = ref(false)
const cancelError = ref('')
const cancelTouched = ref(false)
const cancelReasonV = computed(() => {
  const errors = cancelTouched.value && !cancelReason.value ? [{ $message: 'Choose a reason' }] : []
  return { $error: errors.length > 0, $errors: errors, $touch: () => { cancelTouched.value = true } }
})

async function openCancel () {
  cancelReason.value = null
  cancelComment.value = ''
  cancelError.value = ''
  cancelTouched.value = false
  cancelOpen.value = true
  if (!cancelReasons.value.length) {
    try { cancelReasons.value = (await guestOrderApi.cancelReasons()) || [] } catch (e) { cancelReasons.value = ['Other'] }
  }
}

async function submitCancel () {
  cancelTouched.value = true
  if (!cancelReason.value) return
  cancelling.value = true
  cancelError.value = ''
  try {
    view.value = await guestOrderApi.requestCancel(token.value, cancelReason.value, cancelComment.value.trim())
    cancelOpen.value = false
  } catch (e) {
    cancelError.value = getApiErrorMessage(e)
  } finally {
    cancelling.value = false
  }
}

async function load () {
  loading.value = true
  error.value = ''
  view.value = null
  if (!token.value) {
    error.value = 'This order link is missing its code. Look up your order with its number and email instead.'
    loading.value = false
    return
  }
  try {
    view.value = await guestOrderApi.get(token.value)
  } catch (e) {
    error.value = getApiErrorMessage(e)
  } finally {
    loading.value = false
  }
}

watch(token, load, { immediate: true })
</script>

<style scoped>
.sf-guest-card {
  max-width: 520px;
}
</style>
