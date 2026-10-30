<template>
  <q-page class="q-pa-md">
    <div class="text-h5 q-mb-md">My Wishlist</div>

    <q-inner-loading :showing="loading" color="primary" />

    <q-banner v-if="needsSignIn" class="bg-grey-2 rounded-borders">
      Please sign in to view and manage your wishlist.
    </q-banner>

    <template v-else>
      <q-banner v-if="!loading && !items.length" class="bg-grey-2 rounded-borders">
        Your wishlist is empty. Browse the shop and tap “Wishlist” on a product to save it here.
        <template #action>
          <q-btn flat no-caps color="primary" label="Go to shop" :to="{ name: 'shop-home' }" />
        </template>
      </q-banner>

      <q-list v-else bordered separator class="rounded-borders">
        <q-item v-for="item in items" :key="item.id">
          <q-item-section>
            <q-item-label>{{ item.name }}</q-item-label>
            <q-item-label caption>
              {{ item.sku || '—' }} · {{ formatPrice(item.price) }}
              <q-badge v-if="!item.available" color="grey" label="Unavailable" class="q-ml-sm" />
            </q-item-label>
          </q-item-section>
          <q-item-section side>
            <div class="row q-gutter-sm">
              <q-btn
                dense
                unelevated
                color="primary"
                icon="o_shopping_cart"
                label="Move to cart"
                no-caps
                :disable="!item.available"
                :loading="busyId === item.id"
                @click="onMoveToCart(item)"
              />
              <q-btn
                dense
                flat
                round
                color="negative"
                icon="o_delete"
                :loading="busyId === item.id"
                @click="onRemove(item)"
              />
            </div>
          </q-item-section>
        </q-item>
      </q-list>
    </template>
  </q-page>
</template>

<script setup>
/*
 * Storefront wishlist view (WO-29, AC-CHK-002.2). Lists the authenticated customer's saved products
 * with remove and move-to-cart actions. The wishlist is registered-buyers-only, so a 401 renders a
 * sign-in prompt rather than an error.
 */
import { ref, computed, onMounted } from 'vue'
import { wishlistApi, formatPrice } from 'modules/storefront/api'
import { useCart } from 'modules/storefront/composables/useCart'
import { useNotify } from 'composables/useNotify'
import { getApiErrorMessage } from 'services/api'

const notify = useNotify()
const { refresh: refreshCart } = useCart()

const wishlist = ref(null)
const loading = ref(false)
const needsSignIn = ref(false)
const busyId = ref(null)

const items = computed(() => wishlist.value?.items || [])

async function load () {
  loading.value = true
  needsSignIn.value = false
  try {
    wishlist.value = await wishlistApi.get()
  } catch (err) {
    if (err?.response?.status === 401) needsSignIn.value = true
    else notify.error(getApiErrorMessage(err))
  } finally {
    loading.value = false
  }
}

async function onRemove (item) {
  busyId.value = item.id
  try {
    wishlist.value = await wishlistApi.removeItem(item.id)
    notify.success('Removed from wishlist')
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    busyId.value = null
  }
}

async function onMoveToCart (item) {
  busyId.value = item.id
  try {
    wishlist.value = await wishlistApi.moveToCart(item.id)
    await refreshCart()
    notify.success('Moved to your cart')
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    busyId.value = null
  }
}

onMounted(load)
</script>
