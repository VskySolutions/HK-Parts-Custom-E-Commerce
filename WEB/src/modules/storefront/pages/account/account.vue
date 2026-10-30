<template>
  <div class="account-shell">
    <div class="row items-center justify-between q-mb-md">
      <div>
        <div class="text-h6 text-weight-bold">My account</div>
        <div class="text-grey-7">{{ greeting }}</div>
      </div>
      <q-btn flat no-caps color="grey-8" icon="o_logout" label="Sign out" @click="onLogout" />
    </div>

    <q-tabs
      v-model="tab"
      dense
      no-caps
      align="left"
      active-color="primary"
      indicator-color="primary"
      class="text-grey-7 q-mb-md account-tabs"
    >
      <q-route-tab name="profile" label="Profile" icon="o_person" :to="{ name: 'shop-account-profile' }" />
      <q-route-tab name="addresses" label="Addresses" icon="o_location_on" :to="{ name: 'shop-account-addresses' }" />
      <q-route-tab v-if="!isInquiryOnly" name="orders" label="Orders" icon="o_receipt_long" :to="{ name: 'shop-account-orders' }" />
      <q-route-tab name="inquiries" label="My requests" icon="o_contact_support" :to="{ name: 'shop-account-inquiries' }" />
      <q-route-tab v-if="!isInquiryOnly" name="tax-exemption" label="Tax exemption" icon="o_receipt_long" :to="{ name: 'shop-account-tax-exemption' }" />
      <q-route-tab v-if="!isInquiryOnly" name="subscriptions" label="Subscriptions" icon="o_autorenew" :to="{ name: 'shop-account-subscriptions' }" />
      <q-route-tab v-if="!isInquiryOnly" name="rewards" label="Rewards" icon="o_loyalty" :to="{ name: 'shop-account-rewards' }" />
      <q-route-tab name="privacy" label="Data & Privacy" icon="o_privacy_tip" :to="{ name: 'shop-account-privacy' }" />
    </q-tabs>

    <router-view />
  </div>
</template>

<script setup>
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { useCommerceMode } from 'modules/storefront/composables/useCommerceMode'

// An inquiry-only tenant has no orders, subscriptions, rewards or tax exemptions to show.
const { isInquiryOnly } = useCommerceMode()
import { useCustomerAuthStore } from 'stores/customerAuth'

const router = useRouter()
const auth = useCustomerAuthStore()

const tab = ref('profile')
const greeting = auth.displayName ? `Welcome back, ${auth.displayName}` : 'Manage your profile, addresses and orders.'

function onLogout () {
  auth.logout()
  router.push({ name: 'shop-home' })
}
</script>

<style scoped lang="scss">
.account-shell {
  max-width: 900px;
  margin: 0 auto;
  padding: 24px 16px 48px;
}
.account-tabs {
  border-bottom: 1px solid rgba(0, 0, 0, 0.08);
}
</style>
