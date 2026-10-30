<template>
  <q-dialog :model-value="modelValue" @update:model-value="$emit('update:modelValue', $event)">
    <q-card style="min-width: 380px; max-width: 90vw">
      <q-card-section class="row items-center q-pb-none">
        <div class="text-h6">Send test email</div>
        <q-space />
        <q-btn v-close-popup flat round dense icon="o_close" aria-label="Close" />
      </q-card-section>

      <q-form @submit.prevent="onSubmit">
        <q-card-section class="q-gutter-sm">
          <div class="text-body2 text-grey-7">
            Sends this template — rendered with sample data — to a recipient of your choice.
            No customer or order records are created.
          </div>

          <q-banner v-if="!smtpConfigured" dense class="bg-orange-1 text-orange-9 rounded-borders">
            <template #avatar><q-icon name="o_warning" color="orange-9" /></template>
            No SMTP account is assigned to the {{ category || 'this' }} category, so the test send will fail.
          </q-banner>

          <AppTextField
            v-model="form.recipientEmail"
            label="Recipient email"
            type="email"
            required
            :v="v$.recipientEmail"
            placeholder="you@example.com"
            autofocus
          />
        </q-card-section>

        <q-card-actions align="right" class="q-px-md q-pb-md">
          <q-btn v-close-popup flat label="Cancel" color="grey-8" />
          <q-btn
            unelevated
            type="submit"
            color="primary"
            icon="o_send"
            label="Send test"
            no-caps
            :loading="sending"
          />
        </q-card-actions>
      </q-form>
    </q-card>
  </q-dialog>
</template>

<script setup>
/*
 * TestSendDialog (WO-80, REQ-ENT-004): collects a recipient email and emits
 * `submit` with the trimmed address. The parent performs the test-send call and
 * decides whether to close (dispatch confirmed) or keep the dialog open (failure).
 */
import { reactive, watch } from 'vue'
import useVuelidate from '@vuelidate/core'
import { required, email } from 'validators'

const props = defineProps({
  modelValue: { type: Boolean, default: false },
  sending: { type: Boolean, default: false },
  smtpConfigured: { type: Boolean, default: true },
  category: { type: String, default: '' }
})

const emit = defineEmits(['update:modelValue', 'submit'])

const form = reactive({ recipientEmail: '' })
const rules = { recipientEmail: { required, email } }
const v$ = useVuelidate(rules, form)

watch(
  () => props.modelValue,
  (open) => {
    if (open) {
      form.recipientEmail = ''
      v$.value.$reset()
    }
  }
)

async function onSubmit () {
  const ok = await v$.value.$validate()
  if (!ok) return
  emit('submit', form.recipientEmail.trim())
}
</script>
