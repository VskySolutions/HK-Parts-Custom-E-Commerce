<template>
  <q-card flat bordered class="cp-card q-pa-md">
    <q-card-section class="text-center q-pb-none">
      <q-icon name="o_password" size="42px" color="primary" />
      <div class="text-h5 q-mt-sm">Change password</div>
      <div class="text-subtitle2 text-grey-7">
        {{ auth.mustChangePassword ? 'You must set a new password to continue.' : 'Update your account password.' }}
      </div>
    </q-card-section>

    <q-form @submit.prevent="onSubmit">
      <q-card-section class="q-gutter-md">
        <AppPasswordField v-model="currentPassword" label="Current password"
          autocomplete="current-password" :rules="[(v) => !!v || 'Required']" />
        <AppPasswordField v-model="newPassword" label="New password" strength
          :rules="passwordRules()" />
        <AppPasswordField v-model="confirmPassword" label="Confirm new password"
          :rules="[matchRule(() => newPassword)]" />
      </q-card-section>

      <q-card-actions class="q-px-md q-pb-md">
        <q-btn flat color="grey-8" label="Sign out" @click="onSignOut" />
        <q-space />
        <q-btn type="submit" color="primary" unelevated label="Update password" :loading="loading" />
      </q-card-actions>
    </q-form>
  </q-card>
</template>

<script setup>
/*
 * Change password (WO-94 Step 12). Clears mustChangePassword and returns to the
 * dashboard on success.
 */
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from 'stores/auth'
import { authApi, getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'
import { passwordRules, matchRule } from 'validators'

const currentPassword = ref('')
const newPassword = ref('')
const confirmPassword = ref('')
const loading = ref(false)

const auth = useAuthStore()
const router = useRouter()
const notify = useNotify()

async function onSubmit () {
  loading.value = true
  try {
    await authApi.changePassword({
      currentPassword: currentPassword.value,
      newPassword: newPassword.value
    })
    auth.setMustChangePassword(false)
    notify.success('Password updated')
    router.push('/dashboard')
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    loading.value = false
  }
}

async function onSignOut () {
  await auth.logout()
  router.push('/auth/login')
}
</script>

<style scoped>
.cp-card {
  width: 420px;
  max-width: 92vw;
}
</style>
