<template>
  <q-page class="q-pa-md storefront-container">
    <div class="sf-guest-return q-mx-auto">
      <ReturnRequest v-if="token" :guest-token="token" />
      <q-banner v-else rounded class="bg-orange-1 text-deep-orange">
        This return link is missing its code. Open your order from the order email or
        <router-link :to="{ name: 'shop-order-lookup' }">look it up</router-link> first.
      </q-banner>
    </div>
  </q-page>
</template>

<script setup>
/*
 * Guest return request (docs/guest-checkout-requirements.md U-63): the account return form in guest mode, opened
 * from the guest order page with the signed order token. Public: no sign-in.
 */
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import ReturnRequest from 'modules/storefront/pages/account/return-request.vue'

const route = useRoute()
const token = computed(() => String(route.query.token || ''))
</script>

<style scoped>
.sf-guest-return {
  max-width: 860px;
}
</style>
