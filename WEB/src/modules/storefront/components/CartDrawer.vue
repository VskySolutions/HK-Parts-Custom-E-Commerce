<template>
  <q-dialog
    :model-value="modelValue"
    position="right"
    class="sf-cart-dialog"
    @update:model-value="$emit('update:modelValue', $event)"
  >
    <q-card class="sf-cart-drawer column no-wrap">
      <!-- Header -->
      <div class="row items-center justify-between q-pa-md sf-cart-drawer__head">
        <div class="row items-center q-gutter-sm">
          <q-icon name="o_shopping_cart" size="22px" color="primary" />
          <span class="text-weight-bold">My Cart ({{ itemCount }})</span>
        </div>
        <q-btn flat dense round icon="o_close" color="grey-8" @click="close" />
      </div>

      <!-- Items -->
      <q-scroll-area class="col">
        <div v-if="loading && !items.length" class="q-pa-lg text-center text-grey-6">
          <q-spinner size="28px" color="primary" />
        </div>

        <div v-else-if="!items.length" class="q-pa-xl text-center text-grey-6">
          <q-icon name="o_remove_shopping_cart" size="48px" class="q-mb-sm" />
          <div>Your cart is empty.</div>
          <q-btn class="q-mt-md" color="primary" no-caps unelevated label="Start shopping" :to="{ name: 'shop-home' }" @click="close" />
        </div>

        <q-list v-else separator>
          <q-item v-for="item in items" :key="item.id" class="q-py-md">
            <q-item-section avatar>
              <router-link :to="productTo(item)" @click="close">
                <q-avatar rounded size="56px" color="grey-2" text-color="grey-6">
                  <img v-if="item.imageUrl || item.primaryImageUrl" :src="$media(item.imageUrl || item.primaryImageUrl)" :alt="item.productName">
                  <q-icon v-else name="o_image" />
                </q-avatar>
              </router-link>
            </q-item-section>
            <q-item-section>
              <q-item-label class="text-weight-medium">
                <router-link class="sf-cart-item__name" :to="productTo(item)" @click="close">{{ item.productName }}</router-link>
              </q-item-label>
              <q-item-label caption>{{ format(item.unitPrice) }}</q-item-label>
              <AppLineCustomAttributes :attributes="item.customAttributes" />
              <q-item-label v-if="!item.available" class="text-negative" caption>Unavailable</q-item-label>

              <!-- Quantity stepper (capped by available stock) -->
              <div class="sf-qtybox row items-center no-wrap q-mt-xs">
                <q-btn flat dense size="sm" icon="o_remove" :disable="busy[item.id] || item.quantity <= 1" @click="setQty(item, item.quantity - 1)" />
                <span class="sf-qtybox__val">{{ item.quantity }}</span>
                <q-btn flat dense size="sm" icon="o_add" :disable="busy[item.id] || !canIncrease(item)" @click="setQty(item, item.quantity + 1)" />
              </div>
              <q-item-label v-if="atMaxStock(item)" caption class="text-orange-8">Max available stock</q-item-label>
            </q-item-section>
            <q-item-section side top>
              <div class="text-weight-bold">{{ format(item.lineTotal) }}</div>
              <q-btn flat dense round size="sm" icon="o_delete" color="negative" :disable="busy[item.id]" @click="remove(item)" />
            </q-item-section>
          </q-item>
        </q-list>
      </q-scroll-area>

      <!-- Footer -->
      <div v-if="items.length" class="sf-cart-drawer__footer q-pa-md">
        <div class="row items-center justify-between q-mb-md">
          <span class="text-grey-7">Subtotal</span>
          <span class="text-h6 text-weight-bold">{{ format(subtotal) }}</span>
        </div>
        <q-btn class="full-width q-mb-sm" color="primary" outline no-caps label="View cart" :to="{ name: 'shop-cart' }" @click="close" />
        <q-btn
          class="full-width"
          color="primary"
          no-caps
          unelevated
          :label="checkoutLabel"
          :icon-right="isInquiryCart ? 'o_send' : undefined"
          :to="{ name: 'shop-checkout' }"
          @click="close"
        />
      </div>
    </q-card>
  </q-dialog>
</template>

<script setup>
import { reactive, computed, onMounted } from 'vue'
import { useCart } from 'modules/storefront/composables/useCart'
import { useCurrency } from 'modules/storefront/composables/useCurrency'
import { useCommerceMode } from 'modules/storefront/composables/useCommerceMode'

defineProps({ modelValue: { type: Boolean, default: false } })
const emit = defineEmits(['update:modelValue'])

const { items, itemCount, subtotal, loading, ensureLoaded, removeItem, updateItem } = useCart()
const { format } = useCurrency()
const { isInquiryOnly, inquiryButtonLabel } = useCommerceMode()

// Inquiry checkout (REQ-INQ-001): the CTA reads as the request the buyer is making,
// using the same label as the product-page request button.
const isInquiryCart = computed(() =>
  isInquiryOnly.value || items.value.some((i) => i.isInquiryOnly === true)
)
const checkoutLabel = computed(() => (isInquiryCart.value ? inquiryButtonLabel.value : 'Checkout'))

const busy = reactive({})

function close () {
  emit('update:modelValue', false)
}

// Link a line to its product detail page.
function productTo (item) {
  return { name: 'shop-product', params: { idOrSlug: item.productId } }
}

// A quote line is not sold from stock (REQ-INQ-001), so no quantity cap applies to it.
function isQuoteLine (item) {
  return isInquiryOnly.value || item.isInquiryOnly === true
}

// Whether the + button may increase the quantity (respecting available stock unless backorder is allowed).
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

async function setQty (item, qty) {
  const next = Math.max(1, qty)
  if (next === item.quantity || busy[item.id]) return
  if (!isQuoteLine(item) && !item.allowBackorder && item.stockQuantity != null && next > item.stockQuantity) return
  busy[item.id] = true
  try {
    await updateItem(item.id, next)
  } finally {
    busy[item.id] = false
  }
}

async function remove (item) {
  if (busy[item.id]) return
  busy[item.id] = true
  try {
    await removeItem(item.id)
  } finally {
    busy[item.id] = false
  }
}

onMounted(ensureLoaded)
</script>

<style scoped lang="scss">
// Full-height right slide-over (q-dialog position="right" stretches to the viewport height).
.sf-cart-drawer {
  width: 400px;
  max-width: 92vw;
  height: 100vh;
  max-height: 100vh;
  border-radius: 0;
}
.sf-cart-drawer__head {
  border-bottom: 1px solid var(--sf-border);
  background: #fff;
}
.sf-cart-drawer__footer {
  border-top: 1px solid var(--sf-border);
  background: #fff;
}
.sf-cart-item__name {
  color: var(--sf-heading);
  text-decoration: none;
}
.sf-cart-item__name:hover { color: var(--sf-accent); }
.sf-qtybox {
  border: 1px solid var(--sf-border);
  border-radius: var(--sf-radius);
  width: fit-content;
}
.sf-qtybox__val {
  min-width: 28px;
  text-align: center;
  font-weight: 600;
  font-size: 13px;
}
</style>
