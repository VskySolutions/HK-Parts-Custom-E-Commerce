<template>
  <q-btn
    flat
    no-caps
    dense
    icon="o_shopping_cart"
    :label="$q.screen.gt.sm ? 'Cart' : ''"
    :to="{ name: 'shop-cart' }"
    aria-label="Cart"
  >
    <q-badge v-if="itemCount" color="red" floating>{{ itemCount }}</q-badge>
    <q-tooltip>Your cart</q-tooltip>
  </q-btn>
</template>

<script setup>
/*
 * Cart indicator (WO-28): a header cart button with a live item-count badge that
 * links to the cart page. The count comes from the shared useCart state, loaded
 * once on mount so the badge is accurate on any storefront page.
 */
import { onMounted } from 'vue'
import { useQuasar } from 'quasar'
import { useCart } from 'modules/storefront/composables/useCart'

const $q = useQuasar()
const { itemCount, ensureLoaded } = useCart()

onMounted(() => {
  // Best-effort — a failure here must never break the storefront chrome.
  ensureLoaded().catch(() => {})
})
</script>
