<template>
  <div class="account-auth-page">
    <q-card flat bordered class="account-auth-card">
      <q-card-section class="q-pb-none">
        <div class="text-h6 text-weight-bold">Sign in</div>
        <div class="text-grey-7">Sign in to {{ tenant.brandName }} to continue.</div>
      </q-card-section>

      <q-form @submit.prevent="onSubmit">
        <q-card-section class="q-gutter-md">
          <AppTextField
            v-model="email"
            label="Email"
            type="email"
            autocomplete="username"
            :disable="loading"
            :rules="[(v) => !!v || 'Email is required']"
          />
          <AppPasswordField
            v-model="password"
            label="Password"
            autocomplete="current-password"
            :disable="loading"
            :rules="[(v) => !!v || 'Password is required']"
          />

          <div class="row justify-end">
            <router-link class="text-primary text-caption" :to="{ name: 'forgot-password' }">
              Forgot password?
            </router-link>
          </div>
        </q-card-section>

        <q-card-actions class="q-px-md q-pb-md column q-gutter-sm">
          <q-btn type="submit" color="primary" no-caps unelevated label="Sign in" :loading="loading" class="full-width" />
        </q-card-actions>
      </q-form>
    </q-card>
  </div>
</template>

<script setup>
/*
 * Login page (WO-94 Step 12): actually calls authStore.login and routes to
 * /dashboard (or the ?redirect target) on success.
 */
import { ref } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { useAuthStore } from 'stores/auth'
import { useTenantStore } from 'stores/tenant'
import { useNotify } from 'composables/useNotify'
import { getApiErrorMessage } from 'services/api'

const email = ref('')
const password = ref('')
const loading = ref(false)

const auth = useAuthStore()
const tenant = useTenantStore()
const router = useRouter()
const route = useRoute()
const notify = useNotify()

async function onSubmit () {
  loading.value = true
  try {
    await auth.login({ email: email.value, password: password.value })
    notify.success('Signed in successfully')
    // Unified login (WO-112): honour an explicit ?redirect, else send each role to its
    // home — staff (any role) to the admin dashboard, customers (no role) to the storefront.
    if (typeof route.query.redirect === 'string' && route.query.redirect) {
      router.push(route.query.redirect)
    } else {
      router.push(auth.isStaff ? { path: '/dashboard' } : { name: 'shop-home' })
    }
  } catch (err) {
    notify.error(getApiErrorMessage(err))
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
