<template>
  <div class="account-auth-page">
    <q-card flat bordered class="account-auth-card">
      <q-card-section class="q-pb-none">
        <div class="text-h6 text-weight-bold">Create your account</div>
        <div class="text-grey-7">It only takes a minute.</div>
      </q-card-section>

      <template v-if="!done">
        <q-form @submit.prevent="onSubmit">
          <q-card-section class="q-gutter-md">
            <div>
              <div class="row q-col-gutter-md">
                <div class="col-12 col-sm-6">
                  <AppTextField
                    v-model="firstName"
                    label="First name"
                    required
                    :rules="[(v) => !!v || 'First Name is Required']"
                  />
                </div>
                <div class="col-12 col-sm-6">
                  <AppTextField
                    v-model="lastName"
                    label="Last name"
                    required
                    :rules="[(v) => !!v || 'Last Name is Required']"
                  />
                </div>
              </div>
            </div>
            <AppTextField
              v-model="email"
              label="Email"
              type="email"
              required
              autocomplete="email"
              :rules="[(v) => !!v || 'Email is required']"
            />
            <AppPhoneInput
              label="Phone (optional)"
              :model-value="phoneNumber"
              @update:model-value="phoneNumber = $event"
            />
            <AppPasswordField v-model="password" label="Password" required strength :rules="passwordRules()" />
          </q-card-section>

          <q-card-actions class="q-px-md q-pb-md column q-gutter-sm">
            <q-btn type="submit" color="primary" no-caps unelevated label="Create account" :loading="loading" class="full-width" />
            <div class="text-center text-grey-7 text-caption">
              Already have an account?
              <router-link class="text-primary" :to="{ name: 'shop-login' }">Sign in</router-link>
            </div>
          </q-card-actions>
        </q-form>
      </template>

      <q-card-section v-else class="text-center q-gutter-sm">
        <q-icon name="o_mark_email_read" color="positive" size="48px" />
        <div class="text-subtitle1 text-weight-medium">Check your inbox</div>
        <div class="text-grey-7">
          We sent a verification link to <strong>{{ email }}</strong>. Confirm your email, then sign in.
        </div>
        <q-btn color="primary" flat no-caps label="Go to sign in" :to="{ name: 'shop-login' }" />
      </q-card-section>
    </q-card>
  </div>
</template>

<script setup>
import { ref } from 'vue'
import { useQuasar } from 'quasar'
import { useCustomerAuthStore } from 'stores/customerAuth'
import { getApiErrorMessage } from 'services/api'
import { passwordRules } from 'validators'
import AppPhoneInput from 'components/common/AppPhoneInput.vue'

const $q = useQuasar()
const auth = useCustomerAuthStore()

const firstName = ref('')
const lastName = ref('')
const email = ref('')
const phoneNumber = ref('')
const password = ref('')
const loading = ref(false)
const done = ref(false)

async function onSubmit () {
  loading.value = true
  try {
    await auth.register({
      firstName: firstName.value.trim(),
      lastName: lastName.value.trim(),
      email: email.value.trim(),
      phoneNumber: phoneNumber.value.trim() || null,
      password: password.value
    })
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
