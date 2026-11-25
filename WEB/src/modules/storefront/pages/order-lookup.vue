<template>
  <q-page class="q-pa-md storefront-container">
    <q-card flat bordered class="q-mx-auto sf-lookup-card">
      <q-card-section>
        <h1 class="text-h5 text-weight-bold q-my-none">Find your order</h1>
        <div class="text-body2 text-grey-8 q-mt-sm">
          Checked out as a guest? Enter your order number and the email address you used. No account needed.
        </div>
      </q-card-section>

      <q-form greedy class="q-px-md q-pb-md" @submit.prevent="submit">
        <AppTextField
          label="Order number"
          required
          placeholder="ORD-…"
          autocomplete="off"
          :model-value="orderNumber"
          :v="orderV"
          @update:model-value="orderNumber = $event"
        />

        <q-btn-toggle
          v-model="mode"
          class="q-my-md"
          no-caps
          unelevated
          spread
          toggle-color="primary"
          color="grey-2"
          text-color="grey-8"
          :options="[{ label: 'Use email', value: 'email' }, { label: 'Use billing ZIP', value: 'zip' }]"
        />

        <AppTextField
          v-if="mode === 'email'"
          label="Email address"
          type="email"
          required
          autocomplete="email"
          :model-value="email"
          :v="emailV"
          @update:model-value="email = $event"
        />
        <AppTextField
          v-else
          label="Billing ZIP / postal code"
          required
          autocomplete="postal-code"
          :model-value="zip"
          :v="zipV"
          @update:model-value="zip = $event"
        />

        <div v-if="error" class="text-negative text-body2 q-mt-md" role="alert" aria-live="assertive">{{ error }}</div>

        <q-btn
          type="submit"
          unelevated
          color="primary"
          no-caps
          class="full-width q-mt-md"
          label="Find my order"
          :loading="busy"
        />

        <div class="text-caption text-grey-7 q-mt-md text-center">
          Have an account?
          <router-link :to="{ path: '/auth/login', query: { redirect: '/shop/account/orders' } }">Sign in</router-link>
          to see all your orders.
        </div>
      </q-form>
    </q-card>
  </q-page>
</template>

<script setup>
/*
 * Guest order lookup (docs/guest-checkout-requirements.md U-60/U-62): order number + email, or + billing ZIP.
 * A match returns a signed token and opens the read-only order page. Any mismatch gets the same message, and
 * the API refuses after 5 failures per IP in 15 minutes. The form is reCAPTCHA-protected like checkout (N-08).
 */
import { ref, computed } from 'vue'
import { useRouter } from 'vue-router'
import { guestOrderApi } from 'modules/storefront/api'
import { useRecaptcha } from 'modules/storefront/composables/useRecaptcha'
import { getApiErrorMessage } from 'services/api'

const router = useRouter()
const { getToken } = useRecaptcha()

const orderNumber = ref('')
const email = ref('')
const zip = ref('')
const mode = ref('email')
const busy = ref(false)
const error = ref('')
const touched = ref(false)

function fieldV (message, ok) {
  const errors = touched.value && !ok ? [{ $message: message }] : []
  return { $error: errors.length > 0, $errors: errors, $touch: () => { touched.value = true } }
}
const orderOk = computed(() => !!orderNumber.value.trim())
const emailOk = computed(() => /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.value.trim()))
const zipOk = computed(() => zip.value.trim().length >= 3)
const orderV = computed(() => fieldV('Enter your order number', orderOk.value))
const emailV = computed(() => fieldV('Enter the email used for the order', emailOk.value))
const zipV = computed(() => fieldV('Enter the billing ZIP code', zipOk.value))

async function submit () {
  touched.value = true
  error.value = ''
  if (!orderOk.value || (mode.value === 'email' ? !emailOk.value : !zipOk.value)) return
  busy.value = true
  try {
    const res = await guestOrderApi.lookup({
      orderNumber: orderNumber.value.trim(),
      email: mode.value === 'email' ? email.value.trim() : null,
      postalCode: mode.value === 'zip' ? zip.value.trim() : null,
      recaptchaToken: await getToken('guestCheckout')
    })
    router.push({ name: 'shop-guest-order', query: { token: res.token } })
  } catch (e) {
    error.value = getApiErrorMessage(e)
  } finally {
    busy.value = false
  }
}
</script>

<style scoped>
.sf-lookup-card {
  max-width: 480px;
}
</style>
