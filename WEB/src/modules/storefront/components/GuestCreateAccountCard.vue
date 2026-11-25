<template>
  <q-card flat bordered class="sf-guest-account">
    <q-card-section>
      <!-- U-72: the email already has an account: offer sign-in, never a second account. -->
      <template v-if="accountExists">
        <div class="text-subtitle1 text-weight-bold">You already have an account</div>
        <div class="text-body2 text-grey-8 q-mt-xs">
          Sign in with {{ email }} to see this and all your other orders in one place.
        </div>
        <q-btn
          unelevated
          color="primary"
          no-caps
          class="full-width q-mt-md"
          label="Sign in"
          :to="{ path: '/auth/login', query: { redirect: '/shop/account/orders' } }"
        />
      </template>

      <!-- Done: a verification email is on its way. -->
      <template v-else-if="createdEmail">
        <div class="row items-center no-wrap">
          <q-icon name="o_mark_email_read" color="positive" size="32px" class="q-mr-sm" />
          <div class="text-subtitle1 text-weight-bold">Check your inbox</div>
        </div>
        <div class="text-body2 text-grey-8 q-mt-sm">
          We sent a link to <strong>{{ createdEmail }}</strong>. Verify your email to sign in. Your guest orders will
          then show in your account.
        </div>
      </template>

      <!-- U-70: just a password: the account takes the order's email, name and addresses. -->
      <template v-else>
        <div class="text-subtitle1 text-weight-bold">Save your details for next time</div>
        <div class="text-body2 text-grey-8 q-mt-xs">
          Create an account with {{ email || 'your order email' }}. Just pick a password: we'll use the name and
          addresses from this order.
        </div>
        <q-form class="q-mt-md" @submit.prevent="submit">
          <AppPasswordField
            label="Password"
            required
            strength
            :model-value="password"
            :v="passwordV"
            @update:model-value="password = $event"
          />
          <div v-if="error" class="text-negative text-body2 q-mt-sm" role="alert" aria-live="assertive">{{ error }}</div>
          <q-btn
            type="submit"
            unelevated
            color="primary"
            no-caps
            class="full-width q-mt-md"
            icon="o_person_add"
            label="Create account"
            :loading="busy"
          />
        </q-form>
      </template>
    </q-card-section>
  </q-card>
</template>

<script setup>
/*
 * Guest → account (docs/guest-checkout-requirements.md U-70/U-72): set a password to turn a guest order into an
 * account. The account is created unverified; the verification email it triggers is what links the guest's orders
 * (U-71, N-07). Shown on the guest order page and on the checkout confirmation (U-53).
 */
import { ref, computed } from 'vue'
import { guestOrderApi } from 'modules/storefront/api'
import { getApiErrorMessage } from 'services/api'

const props = defineProps({
  token: { type: String, required: true },
  email: { type: String, default: '' },
  accountExists: { type: Boolean, default: false }
})

const password = ref('')
const busy = ref(false)
const error = ref('')
const touched = ref(false)
const createdEmail = ref('')

// Same policy as the server (8–16 characters, at least one letter and one number).
const passwordOk = computed(() => {
  const p = password.value || ''
  return p.length >= 8 && p.length <= 16 && /[A-Za-z]/.test(p) && /\d/.test(p)
})
const passwordV = computed(() => {
  const errors = touched.value && !passwordOk.value
    ? [{ $message: 'Use 8–16 characters with at least one letter and one number' }]
    : []
  return { $error: errors.length > 0, $errors: errors, $touch: () => { touched.value = true } }
})

async function submit () {
  touched.value = true
  error.value = ''
  if (!passwordOk.value) return
  busy.value = true
  try {
    const res = await guestOrderApi.createAccount(props.token, password.value)
    createdEmail.value = res?.email || props.email
  } catch (e) {
    error.value = getApiErrorMessage(e)
  } finally {
    busy.value = false
  }
}
</script>
