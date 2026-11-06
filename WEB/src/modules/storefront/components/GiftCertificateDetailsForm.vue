<template>
  <q-form ref="formRef" class="sf-gc-details q-mb-md" @submit.prevent>
    <div class="text-subtitle2 q-mb-xs">Gift certificate details</div>
    <div class="text-caption text-grey-7 q-mb-sm">We'll email the certificate code to the recipient, with you in copy.</div>
    <div class="row q-col-gutter-sm">
      <div class="col-12 col-sm-6">
        <q-input v-model="model.gcSenderName" dense outlined label="Your name *" maxlength="75" lazy-rules :rules="[req('Your name'), max(75)]" />
      </div>
      <div class="col-12 col-sm-6">
        <q-input v-model="model.gcSenderEmail" dense outlined type="email" label="Your email *" maxlength="75" lazy-rules :rules="[req('Your email'), emailRule, max(75)]" />
      </div>
      <div class="col-12 col-sm-6">
        <q-input v-model="model.gcRecipientName" dense outlined label="Recipient name *" maxlength="75" lazy-rules :rules="[req('Recipient name'), max(75)]" />
      </div>
      <div class="col-12 col-sm-6">
        <q-input v-model="model.gcRecipientEmail" dense outlined type="email" label="Recipient email *" maxlength="75" lazy-rules :rules="[req('Recipient email'), emailRule, max(75)]" />
      </div>
      <div class="col-12">
        <q-input v-model="model.gcMessage" dense outlined type="textarea" autogrow label="Gift message (optional)" maxlength="1000" counter :rules="[max(1000)]" />
      </div>
    </div>
  </q-form>
</template>

<script setup>
/*
 * Sender / recipient fields for an ONLINE gift certificate product (the server validates them again on
 * add-to-cart). The parent calls validate() before adding and spreads payload() into the cart request.
 */
import { ref, reactive } from 'vue'

const formRef = ref(null)
const model = reactive({ gcSenderName: '', gcSenderEmail: '', gcRecipientName: '', gcRecipientEmail: '', gcMessage: '' })

const req = (label) => (v) => !!(v && String(v).trim()) || `${label} is required`
const max = (n) => (v) => !v || String(v).length <= n || `At most ${n} characters`
const emailRule = (v) => !v || /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(String(v).trim()) || 'Enter a valid email'

async function validate () {
  return formRef.value ? await formRef.value.validate() : false
}

function payload () {
  return {
    gcSenderName: model.gcSenderName.trim(),
    gcSenderEmail: model.gcSenderEmail.trim(),
    gcRecipientName: model.gcRecipientName.trim(),
    gcRecipientEmail: model.gcRecipientEmail.trim(),
    gcMessage: model.gcMessage.trim() || null
  }
}

/** Clears the recipient so the next add is a fresh certificate (lines never merge); keeps the sender. */
function resetRecipient () {
  model.gcRecipientName = ''
  model.gcRecipientEmail = ''
  model.gcMessage = ''
  formRef.value?.resetValidation()
}

defineExpose({ validate, payload, resetRecipient })
</script>
