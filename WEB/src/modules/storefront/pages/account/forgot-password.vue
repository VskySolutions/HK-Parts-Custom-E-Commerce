<template>
  <div class="account-auth-page">
    <q-card flat bordered class="account-auth-card">
      <q-card-section class="q-pb-none">
        <div class="text-h6 text-weight-bold">Reset your password</div>
        <div class="text-grey-7">We'll email you a link to choose a new password.</div>
      </q-card-section>

      <template v-if="!done">
        <q-form @submit.prevent="onSubmit">
          <q-card-section class="q-gutter-md">
            <AppTextField
              v-model="email"
              label="Email"
              type="email"
              autocomplete="email"
              :rules="[(v) => !!v || 'Email is required']"
            />
          </q-card-section>
          <q-card-actions class="q-px-md q-pb-md column q-gutter-sm">
            <q-btn type="submit" color="primary" no-caps unelevated label="Send reset link" :loading="loading" class="full-width" />
            <router-link class="text-center text-primary text-caption" :to="{ name: 'shop-login' }">
              Back to sign in
            </router-link>
          </q-card-actions>
        </q-form>
      </template>

      <q-card-section v-else class="text-center q-gutter-sm">
        <q-icon name="o_outgoing_mail" color="positive" size="48px" />
        <div class="text-subtitle1 text-weight-medium">Check your inbox</div>
        <div class="text-grey-7">
          If an account exists for <strong>{{ email }}</strong>, a password-reset link is on its way.
        </div>
        <q-btn color="primary" flat no-caps label="Back to sign in" :to="{ name: 'shop-login' }" />
      </q-card-section>
    </q-card>
  </div>
</template>

<script setup>
import { ref } from 'vue'
import { useQuasar } from 'quasar'
import { useCustomerAuthStore } from 'stores/customerAuth'
import { getApiErrorMessage } from 'services/api'

const $q = useQuasar()
const auth = useCustomerAuthStore()

const email = ref('')
const loading = ref(false)
const done = ref(false)

async function onSubmit () {
  loading.value = true
  try {
    await auth.requestPasswordReset({ email: email.value.trim() })
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
