<template>
  <q-dialog :model-value="modelValue" persistent @update:model-value="$emit('update:modelValue', $event)">
    <q-card style="width: 460px; max-width: 95vw">
      <q-card-section class="text-h6">Change guest email</q-card-section>
      <q-card-section class="q-pt-none">
        <AppTextField
          label="Email"
          type="email"
          required
          :model-value="email"
          :v="emailV"
          @update:model-value="email = $event"
        />
        <q-checkbox v-model="resend" label="Resend the order confirmation to this address" class="q-mt-sm" />
        <div class="text-caption text-grey-7 q-mt-xs">
          The change is recorded in the order history. The order moves to the guest record for the new email.
        </div>
      </q-card-section>
      <q-card-actions align="right">
        <q-btn flat no-caps label="Cancel" :disable="saving" @click="$emit('update:modelValue', false)" />
        <q-btn unelevated no-caps color="primary" label="Save" :loading="saving" @click="save" />
      </q-card-actions>
    </q-card>
  </q-dialog>
</template>

<script setup>
/*
 * Guest checkout A-22: fix a typo in a guest order's email and optionally resend the confirmation email.
 */
import { ref, computed, watch } from 'vue'
import { orderApi } from 'modules/orders/api'
import { useNotify } from 'composables/useNotify'
import { getApiErrorMessage } from 'services/api'

const props = defineProps({
  modelValue: { type: Boolean, default: false },
  orderId: { type: String, default: null },
  currentEmail: { type: String, default: '' }
})
const emit = defineEmits(['update:modelValue', 'saved'])

const notify = useNotify()
const email = ref('')
const resend = ref(true)
const saving = ref(false)
const touched = ref(false)

watch(() => props.modelValue, (open) => {
  if (open) {
    email.value = props.currentEmail || ''
    resend.value = true
    touched.value = false
  }
})

const valid = computed(() => /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(String(email.value || '').trim()))
const emailV = computed(() => {
  const errors = touched.value && !valid.value ? [{ $message: 'Enter a valid email' }] : []
  return { $error: errors.length > 0, $errors: errors, $touch: () => { touched.value = true } }
})

async function save () {
  touched.value = true
  if (!valid.value || !props.orderId) return
  saving.value = true
  try {
    const order = await orderApi.updateGuestEmail(props.orderId, { email: email.value.trim(), resendConfirmation: resend.value })
    notify.success(resend.value ? 'Email updated and confirmation resent.' : 'Email updated.')
    emit('saved', order)
    emit('update:modelValue', false)
  } catch (e) {
    notify.error(getApiErrorMessage(e))
  } finally {
    saving.value = false
  }
}
</script>
