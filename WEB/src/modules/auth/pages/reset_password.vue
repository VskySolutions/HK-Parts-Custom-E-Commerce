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
            <AppPasswordField
              v-model="password"
              label="New password"
              strength
              :rules="passwordRules()"
            />
            <AppPasswordField
              v-model="confirm"
              label="Confirm new password"
              :rules="[matchRule(() => password)]"
            />
          </q-card-section>

          <q-card-actions class="q-px-md q-pb-md column q-gutter-sm">
            <q-btn type="submit" color="primary" class="full-width" label="Update password" no-caps unelevated :loading="loading" :disable="!hasToken" />
            <q-btn flat no-caps color="grey-8" class="full-width" label="Back to sign in" :to="{ name: 'login' }" />
          </q-card-actions>
        </q-form>
      </template>

      <q-card-section v-else class="text-center q-gutter-sm">
        <q-icon name="o_lock_reset" color="positive" size="48px" />
        <div class="text-subtitle1 text-weight-medium">Password updated</div>
        <div class="text-grey-7">You can now sign in with your new password.</div>
        <q-btn color="primary" unelevated no-caps label="Sign in" :to="{ name: 'login' }" />
      </q-card-section>
    </q-card>
  </div>
</template>

<script setup>
/*
 * Admin reset-password: consumes the ?token from the emailed link and sets a new password via
 * authApi.resetPassword. Uses the shared AppPasswordField (mask toggle + strength) and the
 * shared password policy (passwordRules / matchRule).
 */
import { ref, computed } from 'vue'
import { useRoute } from 'vue-router'
import { authApi, getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'
import { passwordRules, matchRule } from 'validators'

const route = useRoute()
const notify = useNotify()

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
    await authApi.resetPassword(token.value, password.value)
    done.value = true
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
