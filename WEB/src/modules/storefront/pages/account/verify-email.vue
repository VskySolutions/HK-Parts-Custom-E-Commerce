<template>
  <div class="account-auth-page">
    <q-card flat bordered class="account-auth-card">
      <q-card-section class="text-center q-gutter-sm">
        <template v-if="state === 'loading'">
          <q-spinner color="primary" size="42px" />
          <div class="text-subtitle1">Verifying your email…</div>
        </template>

        <template v-else-if="state === 'success'">
          <q-icon name="o_verified" color="positive" size="48px" />
          <div class="text-subtitle1 text-weight-medium">Email verified</div>
          <div class="text-grey-7">Your email address is confirmed. You can now sign in.</div>
          <q-btn color="primary" no-caps unelevated label="Sign in" :to="{ name: 'shop-login' }" />
        </template>

        <template v-else>
          <q-icon name="o_error_outline" color="negative" size="48px" />
          <div class="text-subtitle1 text-weight-medium">Verification failed</div>
          <div class="text-grey-7">{{ message }}</div>
          <q-btn color="primary" flat no-caps label="Back to sign in" :to="{ name: 'shop-login' }" />
        </template>
      </q-card-section>
    </q-card>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { useCustomerAuthStore } from 'stores/customerAuth'
import { getApiErrorMessage } from 'services/api'

const route = useRoute()
const auth = useCustomerAuthStore()

const state = ref('loading') // loading | success | error
const message = ref('')

onMounted(async () => {
  const token = route.query.token
  if (!token || typeof token !== 'string') {
    state.value = 'error'
    message.value = 'This verification link is missing its token.'
    return
  }
  try {
    await auth.verifyEmail(token)
    state.value = 'success'
  } catch (e) {
    state.value = 'error'
    message.value = getApiErrorMessage(e)
  }
})
</script>

<style scoped lang="scss">
.account-auth-page {
  min-height: 70vh;
  display: flex;
  align-items: flex-start;
  justify-content: center;
  padding: 32px 16px;
}
.account-auth-card {
  width: 100%;
  max-width: 420px;
  border-radius: 12px;
}
</style>
