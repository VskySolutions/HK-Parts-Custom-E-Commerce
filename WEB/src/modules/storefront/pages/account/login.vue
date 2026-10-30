<template>
  <div class="account-auth-page">
    <q-card flat bordered class="account-auth-card">
      <q-card-section class="q-pb-none">
        <div class="text-h6 text-weight-bold">Sign in</div>
        <div class="text-grey-7">Access your account, orders and saved addresses.</div>
      </q-card-section>

      <q-form @submit.prevent="onSubmit">
        <q-card-section class="q-gutter-md">
          <AppTextField
            v-model="email"
            label="Email"
            type="email"
            autocomplete="email"
            :rules="[(v) => !!v || 'Email is required']"
          />
          <AppPasswordField
            v-model="password"
            label="Password"
            autocomplete="current-password"
            :rules="[(v) => !!v || 'Password is required']"
          />

          <div class="row justify-end">
            <router-link class="text-primary text-caption" :to="{ name: 'shop-forgot-password' }">
              Forgot password?
            </router-link>
          </div>
        </q-card-section>

        <q-card-actions class="q-px-md q-pb-md column q-gutter-sm">
          <q-btn type="submit" color="primary" no-caps unelevated label="Sign in" :loading="loading" class="full-width" />
          <div class="text-center text-grey-7 text-caption">
            New here?
            <router-link class="text-primary" :to="{ name: 'shop-register' }">Create an account</router-link>
          </div>
        </q-card-actions>
      </q-form>
    </q-card>
  </div>
</template>

<script setup>
import { ref } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { useQuasar } from 'quasar'
import { useAuthStore } from 'stores/auth'
import { useCustomerAuthStore } from 'stores/customerAuth'
import { getApiErrorMessage } from 'services/api'

const router = useRouter()
const route = useRoute()
const $q = useQuasar()
const auth = useAuthStore()
const customerAuth = useCustomerAuthStore()

const email = ref('')
const password = ref('')
const loading = ref(false)

async function onSubmit () {
  loading.value = true
  try {
    await customerAuth.login({ email: email.value.trim(), password: password.value })
    const redirect = route.query.redirect
    if (redirect && typeof redirect === 'string') {
      router.push(redirect)
    } else {
      // WO-112 unified login: staff land in the Admin Portal, customers stay on the storefront.
      router.push(auth.isStaff ? { name: 'dashboard' } : { name: 'shop-account-profile' })
    }
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
