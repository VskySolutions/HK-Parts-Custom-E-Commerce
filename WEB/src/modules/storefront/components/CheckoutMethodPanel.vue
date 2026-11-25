<template>
  <div class="sf-checkout-method">
    <q-card flat bordered>
      <div class="sf-checkout-method__bar text-white text-subtitle1 text-weight-medium">Checkout Methods</div>

      <q-card-section class="q-pa-lg">
        <div class="row q-col-gutter-xl">
          <!-- Yes, I have an account: sign in here and carry on with the cart and the saved details. -->
          <div class="col-12 col-md-6">
            <h2 class="sf-checkout-method__title">Yes, I have an account.</h2>
            <div class="text-body2 text-grey-8 q-mb-md">Please sign in below to continue.</div>
            <q-form greedy @submit.prevent="signIn">
              <AppTextField
                label="Email"
                type="email"
                required
                autocomplete="email"
                :model-value="email"
                :v="emailV"
                @update:model-value="email = $event"
              />
              <AppTextField
                class="q-mt-sm"
                label="Password"
                type="password"
                required
                autocomplete="current-password"
                :model-value="password"
                :v="passwordV"
                @update:model-value="password = $event"
              />
              <div class="q-mt-sm">
                <router-link :to="{ name: 'forgot-password' }" class="text-primary text-body2">Forgot Password?</router-link>
              </div>
              <div v-if="error" class="text-negative text-body2 q-mt-sm" role="alert" aria-live="assertive">{{ error }}</div>
              <q-btn
                type="submit"
                unelevated
                no-caps
                color="dark"
                class="q-mt-md sf-checkout-method__btn"
                label="Sign in"
                :loading="signingIn"
              />
            </q-form>
          </div>

          <!-- No, I don't have an account: guest checkout (U-01) or create an account. -->
          <div class="col-12 col-md-6">
            <h2 class="sf-checkout-method__title">No, I don't have an account.</h2>
            <template v-if="guestAllowed">
              <div class="text-body2 text-grey-8 q-mb-md">
                Checkout quickly as a guest or create an account to save your information.
              </div>
              <div class="row q-gutter-sm">
                <q-btn
                  unelevated
                  no-caps
                  color="accent"
                  class="sf-checkout-method__btn"
                  label="Checkout as guest"
                  @click="$emit('guest')"
                />
                <q-btn unelevated no-caps color="grey-8" class="sf-checkout-method__btn" label="Create an account" :to="registerTo" />
              </div>
            </template>
            <!-- U-05: guest checkout is off, or the cart holds an item that needs an account. -->
            <template v-else>
              <div class="text-body2 text-grey-8 q-mb-md" role="status">{{ guestBlockedReason }}</div>
              <q-btn unelevated no-caps color="grey-8" class="sf-checkout-method__btn" label="Create an account" :to="registerTo" />
            </template>
          </div>
        </div>
      </q-card-section>
    </q-card>

    <!-- The next step, shown greyed until a method is chosen (as on the legacy checkout). -->
    <div class="sf-checkout-method__next text-subtitle1 text-weight-medium q-mt-md">Billing Information</div>
  </div>
</template>

<script setup>
/*
 * Checkout method choice (docs/guest-checkout-requirements.md U-01, U-05, U-07): the first checkout screen for a
 * signed-out shopper. Left: sign in (email + password) without leaving checkout, so the cart and anything typed are
 * kept. Right: "Checkout as guest" or "Create an account". When guest checkout is not allowed, the guest button is
 * replaced by the reason. Emits `guest` when the shopper picks guest checkout and `signed-in` after a sign-in.
 */
import { ref, computed } from 'vue'
import { useAuthStore } from 'stores/auth'
import { getApiErrorMessage } from 'services/api'

defineProps({
  guestAllowed: { type: Boolean, default: true },
  guestBlockedReason: { type: String, default: '' },
  registerTo: { type: [Object, String], required: true }
})
const emit = defineEmits(['guest', 'signed-in'])

const auth = useAuthStore()
const email = ref('')
const password = ref('')
const signingIn = ref(false)
const error = ref('')
const touched = ref(false)

const emailOk = computed(() => /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.value.trim()))
function fieldV (message, ok) {
  const errors = touched.value && !ok ? [{ $message: message }] : []
  return { $error: errors.length > 0, $errors: errors, $touch: () => { touched.value = true } }
}
const emailV = computed(() => fieldV('Enter your email address', emailOk.value))
const passwordV = computed(() => fieldV('Enter your password', !!password.value))

async function signIn () {
  touched.value = true
  error.value = ''
  if (!emailOk.value || !password.value) return
  signingIn.value = true
  try {
    await auth.login({ email: email.value.trim(), password: password.value })
    password.value = ''
    emit('signed-in')
  } catch (e) {
    error.value = getApiErrorMessage(e)
  } finally {
    signingIn.value = false
  }
}
</script>

<style scoped lang="scss">
.sf-checkout-method__bar {
  background: var(--q-primary);
  padding: 10px 20px;
  letter-spacing: 0.5px;
}
.sf-checkout-method__title {
  font-size: 1.75rem;
  font-weight: 500;
  line-height: 1.2;
  margin: 0 0 12px;
}
.sf-checkout-method__btn {
  min-height: 44px;
  padding: 0 18px;
  font-weight: 600;
}
.sf-checkout-method__next {
  background: #e0e0e0;
  color: #616161;
  padding: 10px 20px;
  letter-spacing: 0.5px;
}
.body--dark .sf-checkout-method__next {
  background: rgba(255, 255, 255, 0.08);
  color: rgba(255, 255, 255, 0.6);
}
</style>
