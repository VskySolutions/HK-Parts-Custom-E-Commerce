<template>
  <q-page class="q-pa-md storefront-container">
    <div class="text-h5 text-weight-bold q-mb-md">Your cart</div>

    <q-inner-loading :showing="loading && !cart" color="primary" />

    <!-- Empty cart -->
    <q-banner v-if="cart && !items.length" class="bg-grey-2 rounded-borders q-my-md">
      Your cart is empty.
      <template #action>
        <q-btn flat no-caps color="primary" label="Continue shopping" :to="{ name: 'shop-home' }" />
      </template>
    </q-banner>

    <template v-if="items.length">
      <!-- Availability warnings (AC-CHK-001.5/6) -->
      <q-banner
        v-if="warnings.length"
        dense
        class="bg-orange-1 text-orange-9 rounded-borders q-mb-md"
      >
        <template #avatar>
          <q-icon name="o_warning" color="orange-9" />
        </template>
        <ul class="q-my-none q-pl-md">
          <li v-for="(w, i) in warnings" :key="i">{{ w }}</li>
        </ul>
      </q-banner>

      <div class="row q-col-gutter-lg">
        <!-- Line items -->
        <div class="col-12 col-md-8">
          <q-card flat bordered>
            <q-list separator>
              <q-item
                v-for="item in items"
                :key="item.id"
                class="q-py-md"
                :class="{ 'cart-line--backorder': isBackordered(item) }"
              >
                <!-- Thumbnail -->
                <q-item-section avatar>
                  <router-link
                    :to="productLink(item)"
                    class="cart-thumb flex flex-center bg-grey-2 rounded-borders"
                  >
                    <img v-if="item.imageUrl" :src="$media(item.imageUrl)" :alt="item.productName" class="cart-thumb__img">
                    <q-icon v-else name="o_inventory_2" size="28px" color="grey-6" />
                  </router-link>
                </q-item-section>

                <!-- Name + meta -->
                <q-item-section>
                  <q-item-label>
                    <router-link :to="productLink(item)" class="text-dark cart-name">
                      {{ item.productName }}
                    </router-link>
                  </q-item-label>
                  <q-item-label v-if="item.sku" caption>SKU: {{ item.sku }}</q-item-label>
                  <q-item-label caption>{{ format(item.unitPrice) }} each</q-item-label>
                  <AppLineCustomAttributes :attributes="item.customAttributes" />
                  <q-item-label v-if="!item.available">
                    <q-badge color="negative" label="Unavailable" class="q-mt-xs" />
                  </q-item-label>
                  <!-- Orderable only via backorder (out of stock, but backorder allowed) — AC-CHK-001.7 -->
                  <q-item-label v-else-if="isBackordered(item)" class="q-mt-xs row items-center q-gutter-x-sm">
                    <span class="sf-badge sf-badge--backorder">Backordered</span>
                    <span v-if="restockDate(item)" class="text-caption text-orange-9">
                      Ships by {{ restockDate(item) }}
                    </span>
                    <span v-else class="text-caption text-orange-9">Ships when back in stock</span>
                  </q-item-label>

                  <!-- Quantity controls -->
                  <div class="row items-center q-gutter-xs q-mt-sm">
                    <q-btn
                      round
                      dense
                      flat
                      icon="o_remove"
                      size="sm"
                      :disable="rowBusy[item.id] || item.quantity <= 1"
                      aria-label="Decrease quantity"
                      @click="changeQty(item, item.quantity - 1)"
                    />
                    <div class="cart-qty text-center">{{ item.quantity }}</div>
                    <q-btn
                      round
                      dense
                      flat
                      icon="o_add"
                      size="sm"
                      :disable="rowBusy[item.id] || !canIncrease(item)"
                      aria-label="Increase quantity"
                      @click="changeQty(item, item.quantity + 1)"
                    />
                    <q-spinner v-if="rowBusy[item.id]" size="18px" color="primary" class="q-ml-sm" />
                  </div>
                  <q-item-label v-if="atMaxStock(item)" caption class="text-orange-8 q-mt-xs">Max available stock</q-item-label>
                </q-item-section>

                <!-- Line total + remove -->
                <q-item-section side top class="text-right">
                  <q-item-label class="text-subtitle1 text-weight-bold text-dark">
                    {{ format(item.lineTotal) }}
                  </q-item-label>
                  <q-btn
                    flat
                    dense
                    no-caps
                    size="sm"
                    color="negative"
                    icon="o_delete"
                    label="Remove"
                    class="q-mt-sm"
                    :disable="rowBusy[item.id]"
                    @click="removeLine(item)"
                  />
                </q-item-section>
              </q-item>
            </q-list>
          </q-card>

          <div class="q-mt-sm">
            <q-btn flat no-caps color="primary" icon="o_arrow_back" label="Continue shopping" :to="{ name: 'shop-home' }" />
          </div>
        </div>

        <!-- Summary -->
        <div class="col-12 col-md-4">
          <q-card flat bordered>
            <q-card-section>
              <div class="text-subtitle1 text-weight-bold q-mb-sm">Order summary</div>

              <!-- Coupon -->
              <div v-if="appliedCouponCode" class="row items-center q-mb-md">
                <q-chip
                  removable
                  color="green-1"
                  text-color="green-9"
                  icon="o_local_offer"
                  :label="appliedCouponCode"
                  :disable="couponBusy"
                  @remove="onRemoveCoupon"
                />
              </div>
              <div v-else class="q-mb-md">
                <q-input
                  v-model="couponCode"
                  dense
                  outlined
                  placeholder="Coupon code"
                  :disable="couponBusy"
                  @keyup.enter="onApplyCoupon"
                >
                  <template #append>
                    <q-btn
                      flat
                      dense
                      no-caps
                      color="primary"
                      label="Apply"
                      :loading="couponBusy"
                      :disable="!couponCode.trim()"
                      @click="onApplyCoupon"
                    />
                  </template>
                </q-input>
              </div>

              <q-separator class="q-mb-md" />

              <div class="row items-center justify-between q-mb-xs">
                <span class="text-grey-8">Subtotal</span>
                <span class="text-weight-medium">{{ format(subtotal) }}</span>
              </div>
              <div class="text-caption text-grey-6 q-mb-md">
                <template v-if="isInquiryCart">
                  Indicative total — no payment is taken when you submit your request.
                </template>
                <template v-else>Shipping &amp; taxes are calculated at checkout.</template>
              </div>

              <q-btn
                unelevated
                color="primary"
                class="full-width"
                no-caps
                :label="isInquiryCart ? inquiryButtonLabel : 'Proceed to checkout'"
                :icon-right="isInquiryCart ? 'o_send' : 'o_arrow_forward'"
                :disable="!items.length"
                :to="{ name: 'shop-checkout' }"
              />
            </q-card-section>
          </q-card>
        </div>
      </div>
    </template>
  </q-page>
</template>

<script setup>
/*
 * Storefront cart page (WO-28, REQ-CHK-001). Lists the guest cart's lines with a
 * thumbnail, name, per-unit price, a quantity stepper and line total (all money
 * formatted via useCurrency so it follows the selected display currency), plus
 * remove buttons and the availability warnings raised by the backend. A coupon
 * apply/remove control and the subtotal live in the summary, which leads to the
 * one-page checkout. Resilient to empty carts and slow/failed calls.
 */
import { ref, reactive, computed, onMounted } from 'vue'
import { useCart } from 'modules/storefront/composables/useCart'
import { useCommerceMode } from 'modules/storefront/composables/useCommerceMode'
import { useCurrency } from 'modules/storefront/composables/useCurrency'
import { useNotify } from 'composables/useNotify'
import { getApiErrorMessage } from 'services/api'

const {
  cart,
  items,
  subtotal,
  warnings,
  appliedCouponCode,
  loading,
  refresh,
  updateItem,
  removeItem,
  applyCoupon,
  removeCoupon
} = useCart()
const { format, load: loadCurrencies } = useCurrency()
// Quote-only cart (REQ-INQ-001): the CTA leads to the inquiry form rather than payment, using the
// same request label as the product pages and cart drawer.
const { isInquiryOnly, inquiryButtonLabel } = useCommerceMode()
const isInquiryCart = computed(() =>
  isInquiryOnly.value || items.value.some((i) => i.isInquiryOnly === true)
)
const notify = useNotify()

const rowBusy = reactive({})
const couponCode = ref('')
const couponBusy = ref(false)

function productLink (item) {
  return { name: 'shop-product', params: { idOrSlug: item.productId } }
}

// A quote line is not sold from stock (REQ-INQ-001): no cap, no max-stock hint, no backorder state.
function isQuoteLine (item) {
  return isInquiryOnly.value || item.isInquiryOnly === true
}

// Respect available stock (unless the line is a quote, allows backorder, or stock is unknown).
function canIncrease (item) {
  if (isQuoteLine(item)) return true
  if (item.allowBackorder) return true
  if (item.stockQuantity == null) return true
  return item.quantity < item.stockQuantity
}
function atMaxStock (item) {
  if (isQuoteLine(item)) return false
  return !item.allowBackorder && item.stockQuantity != null && item.quantity >= item.stockQuantity
}

// A line is backordered when it is orderable ONLY via backorder: still purchasable (available), but out of
// stock with backorder allowed. In-stock lines and hard-unavailable lines are both excluded (AC-CHK-001.7).
function isBackordered (item) {
  return (
    !isQuoteLine(item) &&
    !!item.allowBackorder &&
    item.available !== false &&
    item.stockQuantity != null &&
    item.stockQuantity <= 0
  )
}

// The estimated restock date is not part of the cart line DTO today, so this degrades to empty when absent
// and only renders once the backend starts sending `estimatedRestockDate` on the line.
function restockDate (item) {
  const raw = item.estimatedRestockDate
  if (!raw) return ''
  const d = new Date(raw)
  if (Number.isNaN(d.getTime())) return ''
  return d.toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' })
}

async function changeQty (item, quantity) {
  if (quantity < 1 || rowBusy[item.id]) return
  if (!isQuoteLine(item) && !item.allowBackorder && item.stockQuantity != null && quantity > item.stockQuantity) return
  rowBusy[item.id] = true
  try {
    await updateItem(item.id, quantity)
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    rowBusy[item.id] = false
  }
}

async function removeLine (item) {
  if (rowBusy[item.id]) return
  rowBusy[item.id] = true
  try {
    await removeItem(item.id)
    notify.info('Item removed from cart')
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    rowBusy[item.id] = false
  }
}

async function onApplyCoupon () {
  const code = couponCode.value.trim()
  if (!code || couponBusy.value) return
  couponBusy.value = true
  try {
    await applyCoupon(code)
    couponCode.value = ''
    notify.success('Coupon applied')
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    couponBusy.value = false
  }
}

async function onRemoveCoupon () {
  if (couponBusy.value) return
  couponBusy.value = true
  try {
    await removeCoupon()
    notify.info('Coupon removed')
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    couponBusy.value = false
  }
}

onMounted(() => {
  loadCurrencies()
  refresh().catch((err) => notify.error(getApiErrorMessage(err)))
})
</script>

<style scoped lang="scss">
.storefront-container {
  max-width: 1200px;
  margin: 0 auto;
}

.cart-thumb {
  width: 64px;
  height: 64px;
  overflow: hidden;
}
.cart-thumb__img {
  width: 100%;
  height: 100%;
  object-fit: contain;
}

.cart-name {
  text-decoration: none;
  font-weight: 500;

  &:hover {
    text-decoration: underline;
  }
}

.cart-qty {
  min-width: 32px;
}

// Backordered lines are visually set apart from in-stock lines with a warm accent strip + tint.
.sf-badge--backorder {
  background: var(--sf-badge-hot);
}
.cart-line--backorder {
  background: rgba(249, 115, 22, 0.06);
  box-shadow: inset 3px 0 0 var(--sf-badge-hot);
}
body.body--dark .cart-line--backorder {
  background: rgba(249, 115, 22, 0.1);
}
</style>
