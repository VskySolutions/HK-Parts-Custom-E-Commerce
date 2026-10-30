<template>
  <div class="sf-card">
    <!-- Media -->
    <div class="sf-card__media">
      <router-link :to="detailTo" :aria-label="product.name">
        <img
          v-if="image"
          :src="$media(image)"
          :alt="product.name"
          class="sf-card__img"
          loading="lazy"
        >
        <div v-else class="sf-card__img row items-center justify-center bg-grey-2 text-grey-5">
          <q-icon name="o_image" size="42px" />
        </div>
        <img
          v-if="hoverImage"
          :src="$media(hoverImage)"
          :alt="product.name"
          class="sf-card__img-alt"
          loading="lazy"
        >
      </router-link>

      <!-- Badges -->
      <div v-if="badges.length" class="sf-card__badges">
        <span v-for="b in badges" :key="b.label" class="sf-badge" :class="`sf-badge--${b.type}`">{{ b.label }}</span>
      </div>

      <!-- Hover action strip -->
      <div class="sf-card__actions">
        <!-- Wishlist hidden
        <button class="sf-card__action-btn" aria-label="Add to wishlist" @click.prevent="addToWishlist">
          <q-icon name="o_favorite_border" size="18px" />
          <q-tooltip>Wishlist</q-tooltip>
        </button>
        -->
        <button class="sf-card__action-btn" aria-label="Compare" :class="{ 'text-primary': inCompare }" @click.prevent="toggleCompare">
          <q-icon name="o_compare_arrows" size="18px" />
          <q-tooltip>{{ inCompare ? 'Remove from compare' : 'Compare' }}</q-tooltip>
        </button>
      </div>
    </div>

    <!-- Body -->
    <div class="sf-card__body">
      <span v-if="brandName" class="sf-card__brand">{{ brandName }}</span>
      <router-link :to="detailTo" class="sf-card__title">{{ product.name }}</router-link>

      <StarRating v-if="rating != null" :value="rating" :count="reviewCount" class="q-my-xs" />

      <div class="sf-card__price">
        <span class="sf-card__price-now">{{ formatPrice(product.price) }}</span>
        <span v-if="oldPrice" class="sf-card__price-old">{{ formatPrice(oldPrice) }}</span>
      </div>

      <div v-if="restockNote" class="text-caption text-orange-9">{{ restockNote }}</div>

      <div class="sf-card__cart">
        <button v-if="!isVariation" class="sf-btn sf-btn--primary sf-btn--block" :disabled="adding || buying" @click.prevent="addToCart">
          <q-icon v-if="!adding" name="o_add_shopping_cart" size="16px" />
          <q-spinner v-else size="14px" />
          Add to Cart
        </button>
        <button
          v-if="isVariation"
          class="sf-btn sf-btn--dark sf-btn--block"
          @click.prevent="chooseOptions"
        >
          <q-icon name="o_tune" size="16px" />
          Choose Options
        </button>
        <!-- Quote-only product: the second CTA adds the item and lands straight on the quote form. -->
        <button
          v-else-if="isInquiryProduct"
          class="sf-btn sf-btn--dark sf-btn--block"
          :disabled="adding || buying"
          @click.prevent="buyNow"
        >
          <q-icon v-if="!buying" name="o_request_quote" size="16px" />
          <q-spinner v-else size="14px" />
          {{ requestLabel }}
        </button>
        <button
          v-else
          class="sf-btn sf-btn--dark sf-btn--block"
          :disabled="adding || buying"
          @click.prevent="buyNow"
        >
          <q-icon v-if="!buying" name="o_bolt" size="16px" />
          <q-spinner v-else size="14px" />
          Buy Now
        </button>
      </div>
    </div>
  </div>
</template>

<script setup>
/*
 * Porto-style product card (WO-111) — used on category, search, home rows and
 * relationship rails. Renders the real summary DTO fields (name, price, image)
 * and DEGRADES GRACEFULLY for data the catalog DTOs don't carry yet (flagged on
 * WO-111): badges, sale/original price, star rating, hover second image, and
 * stock/backorder only render when the corresponding fields are present.
 */
import { ref, computed } from 'vue'
import { useRouter } from 'vue-router'
import { useQuasar } from 'quasar'
import { formatPrice, productImage, productRouteParam } from 'modules/storefront/api'
import { wishlistApi } from 'modules/storefront/api'
import { useCart } from 'modules/storefront/composables/useCart'
import { useCommerceMode } from 'modules/storefront/composables/useCommerceMode'
import { formatDate } from 'src/utils/datetime'
import { useCompare } from 'modules/storefront/composables/useStorefrontStorage'
import { useCustomerAuthStore } from 'stores/customerAuth'
import StarRating from 'modules/storefront/components/StarRating.vue'

const props = defineProps({
  product: { type: Object, required: true }
})

const router = useRouter()
const $q = useQuasar()
const { addItem } = useCart()
const { has, toggle, max } = useCompare()
const customerAuth = useCustomerAuthStore()

const adding = ref(false)
const buying = ref(false)

const detailTo = computed(() => ({ name: 'shop-product', params: { idOrSlug: productRouteParam(props.product) } }))
const image = computed(() => productImage(props.product))
// Defensive: no hover/secondary image field exists on the summary DTO yet.
const hoverImage = computed(() => props.product.hoverImageUrl || props.product.secondaryImageUrl || null)
const brandName = computed(() => props.product.manufacturerName || props.product.brandName || null)

// Defensive: sale/original price not in the summary DTO yet.
const oldPrice = computed(() => props.product.oldPrice || props.product.originalPrice || props.product.compareAtPrice || null)

// Defensive: rating not in the summary DTO yet.
const rating = computed(() => {
  const r = props.product.averageRating ?? props.product.rating
  return typeof r === 'number' ? r : null
})
const reviewCount = computed(() => props.product.reviewCount ?? null)

const inCompare = computed(() => has(props.product.id))

// Variation and grouped products can't be added straight from a card — the customer
// must pick options/items on the detail page first, so the primary action becomes
// "Choose Options".
const isVariation = computed(() =>
  props.product.productType === 'WithVariants' || props.product.productType === 'Grouped'
)

// Badges: explicit product.badges[] if present, else derived from flags/sale.
const badges = computed(() => {
  if (Array.isArray(props.product.badges) && props.product.badges.length) {
    return props.product.badges.map((b) => normalizeBadge(b)).filter(Boolean)
  }
  const out = []
  if (props.product.isNew || props.product.markAsNew) out.push({ type: 'new', label: 'New' })
  if (props.product.isFeatured) out.push({ type: 'featured', label: 'Featured' })
  if (props.product.isHot || props.product.isBestseller) out.push({ type: 'hot', label: 'Hot' })
  if (oldPrice.value && props.product.price) {
    const pct = Math.round((1 - props.product.price / oldPrice.value) * 100)
    if (pct > 0) out.push({ type: 'sale', label: `-${pct}%` })
  }
  return out
})

// Quote-only product (REQ-INQ-001): the card sells AND takes quote requests — Add to Cart keeps
// collecting items, the request CTA sends this one straight to checkout.
const commerce = useCommerceMode()
const isInquiryProduct = computed(() => commerce.isInquiryProduct(props.product))
const requestLabel = computed(() => commerce.buttonLabelFor(props.product))

const restockNote = computed(() => {
  // A quote-only product isn't sold from stock (REQ-INQ-001) — never show an availability note for it.
  if (isInquiryProduct.value) return null
  if (props.product.stockQuantity > 0) return null
  if (props.product.allowBackorder && props.product.estimatedRestockDate) {
    return `Backordered — restock ${formatDate(props.product.estimatedRestockDate)}`
  }
  return null
})

function normalizeBadge (b) {
  if (typeof b === 'string') {
    const t = b.toLowerCase()
    return { type: ['new', 'sale', 'hot', 'featured'].includes(t) ? t : 'featured', label: b }
  }
  if (b && b.label) return { type: b.type || 'featured', label: b.label }
  return null
}

async function addToCart () {
  adding.value = true
  try {
    await addItem({ productId: props.product.id, quantity: 1 })
    $q.notify({ type: 'positive', message: `${props.product.name} added to cart`, timeout: 1500 })
  } catch (e) {
    // Variant-required or unavailable products can't be added from a card — send to detail.
    $q.notify({ type: 'info', message: 'Please choose options for this product.' })
    router.push(detailTo.value)
  } finally {
    adding.value = false
  }
}

// Buy Now — add one to the cart and go straight to checkout. Same variant-required
// fallback as addToCart: if the product needs options, route to the detail page instead.
async function buyNow () {
  buying.value = true
  try {
    await addItem({ productId: props.product.id, quantity: 1 })
    router.push({ name: 'shop-checkout' })
  } catch (e) {
    $q.notify({ type: 'info', message: 'Please choose options for this product.' })
    router.push(detailTo.value)
  } finally {
    buying.value = false
  }
}

// Variation products route to the detail page so the customer can pick a variant.
function chooseOptions () {
  router.push(detailTo.value)
}

function toggleCompare () {
  const result = toggle(props.product.id)
  if (result.full) $q.notify({ type: 'warning', message: `You can compare up to ${max} products.` })
  else if (result.removed) $q.notify({ type: 'info', message: 'Removed from compare', timeout: 1200 })
  else $q.notify({ type: 'positive', message: 'Added to compare', timeout: 1200 })
}

async function addToWishlist () {
  if (!customerAuth.isAuthenticated) {
    $q.notify({ type: 'info', message: 'Please sign in to save to your wishlist.' })
    router.push({ name: 'shop-login', query: { redirect: router.currentRoute.value.fullPath } })
    return
  }
  try {
    await wishlistApi.addItem({ productId: props.product.id })
    $q.notify({ type: 'positive', message: 'Saved to wishlist', timeout: 1500 })
  } catch (e) {
    $q.notify({ type: 'negative', message: 'Could not save to wishlist.' })
  }
}
</script>
