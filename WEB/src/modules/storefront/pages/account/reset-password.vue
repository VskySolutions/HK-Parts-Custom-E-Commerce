<template>
  <div class="account-auth-page">
    <q-card flat bordered class="account-auth-card">
      <q-card-section class="q-pb-none">
        <div class="text-h6 text-weight-bold">Choose a new password</div>
        <div class="text-grey-7">Pick a password you don't use anywhere else.</div>
      </q-card-section>

      <template v-if="!done">
        <q-banner v-if="!hasToken" dense class="bg-red-1 text-negative q-mx-md q-mb-sm rounded-borders">
          This reset link is missing its token. Request a new one from the sign-in page.
        </q-banner>
        <q-form @submit.prevent="onSubmit">
          <q-card-section class="q-gutter-md">
            <AppPasswordField v-model="password" label="New password" strength :rules="passwordRules()" />
            <AppPasswordField v-model="confirm" label="Confirm new password" :rules="[matchRule(() => password)]" />
          </q-card-section>
          <q-card-actions class="q-px-md q-pb-md">
            <q-btn type="submit" color="primary" no-caps unelevated label="Update password" :loading="loading" :disable="!hasToken" class="full-width" />
          </q-card-actions>
        </q-form>
      </template>

      <q-card-section v-else class="text-center q-gutter-sm">
        <q-icon name="o_lock_reset" color="positive" size="48px" />
        <div class="text-subtitle1 text-weight-medium">Password updated</div>
        <div class="text-grey-7">You can now sign in with your new password.</div>
        <q-btn color="primary" no-caps unelevated label="Sign in" :to="{ name: 'shop-login' }" />
      </q-card-section>
    </q-card>
  </div>
</template>

<script setup>
import { ref, computed } from 'vue'
import { useRoute } from 'vue-router'
import { useQuasar } from 'quasar'
import { useCustomerAuthStore } from 'stores/customerAuth'
import { getApiErrorMessage } from 'services/api'
import { passwordRules, matchRule } from 'validators'

const route = useRoute()
const $q = useQuasar()
const auth = useCustomerAuthStore()

const token = computed(() => (typeof route.query.token === 'string' ? route.query.token : ''))
const hasToken = computed(() => !!token.value)

const password = ref('')
const confirm = ref('')
const loading = ref(false)
const done = ref(false)

async function onSubmit () {
  if (!hasToken.value) return
  loading.value = true
  try {
    await auth.resetPassword(token.value, password.value)
    done.value = true
  } catch (e) {
    $q.notify({ type: 'negative', message: getApiErrorMessage(e) })
  } finally {
    loading.value = false
  }
}
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
  max-width: 440px;
  border-radius: 12px;
}
</style>
