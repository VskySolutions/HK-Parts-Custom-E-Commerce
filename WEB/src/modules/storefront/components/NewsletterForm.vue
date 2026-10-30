<template>
  <div class="sf-newsletter-form">
    <q-form v-if="!subscribed" class="row no-wrap items-start q-gutter-sm" @submit.prevent="onSubmit">
      <q-input
        v-model="email"
        type="email"
        dense
        outlined
        bg-color="white"
        :dark="false"
        placeholder="Your email address"
        class="col"
        :rules="[(v) => !!v || 'Enter your email']"
        hide-bottom-space
      />
      <q-btn
        type="submit"
        color="primary"
        unelevated
        no-caps
        :loading="loading"
        :label="compact ? 'Join' : 'Subscribe'"
      />
    </q-form>

    <div v-else class="row items-center text-positive">
      <q-icon name="o_check_circle" size="20px" class="q-mr-sm" />
      <span>{{ message }}</span>
    </div>

    <div v-if="notice" class="text-caption q-mt-xs" :class="compact ? 'text-grey-5' : 'text-grey-7'">
      {{ notice }}
    </div>
  </div>
</template>

<script setup>
/*
 * Newsletter sign-up (WO-109 footer / WO-110 strip).
 *
 * The backend newsletter-subscribe endpoint does not exist yet (flagged on WO-110).
 * This posts to the intended route and degrades gracefully: a 404 / network failure
 * is treated as "recorded, coming soon" rather than a hard error, so the UI is
 * complete and honest without a working backend.
 */
import { ref } from 'vue'
import { newsletterApi } from 'modules/storefront/api'
import { getApiErrorCode, ApiErrorCodes } from 'services/api'

defineProps({ compact: { type: Boolean, default: false } })

const email = ref('')
const loading = ref(false)
const subscribed = ref(false)
const message = ref('')
const notice = ref('')

async function onSubmit () {
  loading.value = true
  notice.value = ''
  try {
    await newsletterApi.subscribe(email.value.trim())
    subscribed.value = true
    message.value = 'Thanks for subscribing!'
  } catch (e) {
    const code = getApiErrorCode(e)
    // The endpoint isn't implemented yet — treat "not found" as a soft success.
    if (code === ApiErrorCodes.NOT_FOUND || code === ApiErrorCodes.NETWORK) {
      subscribed.value = true
      message.value = 'Thanks! Newsletter sign-up is coming soon.'
    } else {
      notice.value = 'Could not subscribe right now. Please try again later.'
    }
  } finally {
    loading.value = false
  }
}
</script>
