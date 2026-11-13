<template>
  <q-dialog :model-value="modelValue" :persistent="submitting" @update:model-value="(v) => emit('update:modelValue', v)" @before-show="reset">
    <q-card class="blacklist-dialog">
      <!-- Red header bar -->
      <q-card-section class="blacklist-dialog__header row items-center no-wrap bg-negative text-white">
        <div class="text-subtitle1 text-weight-bold col ellipsis">Add Customer to Blacklist</div>
        <q-btn flat round dense icon="o_close" :disable="submitting" v-close-popup />
      </q-card-section>

      <q-form ref="formRef" greedy @submit.prevent="submit">
        <q-card-section class="q-pt-lg q-gutter-y-md">
          <!-- Read-only customer info (display only; the server reloads the customer by id). -->
          <div class="blacklist-dialog__info rounded-borders q-pa-md q-gutter-y-sm">
            <div class="row no-wrap items-start">
              <q-icon name="o_person" size="20px" color="positive" class="q-mr-md q-mt-xs" />
              <div class="col">
                <div class="text-caption text-grey-7">Customer</div>
                <div class="text-body2 text-weight-bold">{{ fullName || '—' }}</div>
              </div>
            </div>
            <div class="row no-wrap items-start">
              <q-icon name="o_mail" size="20px" color="positive" class="q-mr-md q-mt-xs" />
              <div class="col ellipsis">
                <div class="text-caption text-grey-7">Email</div>
                <div class="text-body2 ellipsis">{{ customer?.email || '—' }}</div>
              </div>
            </div>
          </div>

          <div>
            <AppFieldLabel label="Reason for Blocking" required class="text-weight-bold" />
            <q-input
              v-model="form.reason"
              outlined
              dense
              type="textarea"
              rows="3"
              :maxlength="rules.reasonMax"
              placeholder="e.g. Chargeback, fraudulent order, repeated returns..."
              :rules="reasonRules"
              :disable="submitting"
              hide-bottom-space
              input-style="resize: vertical"
            />
          </div>

          <div>
            <div class="app-field-label row items-baseline no-wrap q-mb-xs text-weight-bold">
              Internal Notes<span class="text-caption text-grey-6 text-weight-regular q-ml-xs">(optional)</span>
            </div>
            <q-input
              v-model="form.internalNotes"
              outlined
              dense
              type="textarea"
              rows="2"
              :maxlength="rules.notesMax"
              :disable="submitting"
              input-style="resize: vertical"
            />
          </div>
        </q-card-section>

        <q-card-actions align="right" class="q-px-md q-pb-md q-pt-none">
          <q-btn unelevated color="grey-9" label="Cancel" :disable="submitting" v-close-popup />
          <q-btn type="submit" unelevated color="negative" icon="o_block" label="Block Customer" :loading="submitting" :disable="submitting" />
        </q-card-actions>
      </q-form>
    </q-card>
  </q-dialog>
</template>

<script setup>
/*
 * "Add Customer to Blacklist" modal (Customers list → Actions → Blacklist). Shows the selected row's name/email
 * read-only, captures a required reason + optional internal notes, and POSTs only the customer id + those fields.
 * The form is reset on every open (and Cancel just closes), so nothing carries over between customers.
 * Emits `blocked` after a successful save; the list shows the toast and refreshes.
 */
import { ref, reactive, computed, nextTick } from 'vue'
import { getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'
import { customerAdminApi, customerBlacklistRules as rules } from 'modules/customers/api'
import AppFieldLabel from 'components/common/AppFieldLabel.vue'

const props = defineProps({
  modelValue: { type: Boolean, default: false },
  // The grid row: { id, firstName, lastName, email }.
  customer: { type: Object, default: null }
})
const emit = defineEmits(['update:modelValue', 'blocked'])

const notify = useNotify()
const formRef = ref(null)
const submitting = ref(false)
const form = reactive({ reason: '', internalNotes: '' })

const fullName = computed(() => `${props.customer?.firstName || ''} ${props.customer?.lastName || ''}`.trim())

const reasonRules = [
  (v) => !!(v && v.trim()) || 'Reason for blocking is required',
  (v) => !v || v.trim().length <= rules.reasonMax || `Maximum ${rules.reasonMax} characters`
]

async function reset () {
  form.reason = ''
  form.internalNotes = ''
  submitting.value = false
  await nextTick()
  formRef.value?.resetValidation()
}

async function submit () {
  if (submitting.value || !props.customer) return
  const valid = await formRef.value?.validate()
  if (!valid) return
  submitting.value = true
  try {
    await customerAdminApi.blacklist(props.customer.id, {
      reason: form.reason.trim(),
      internalNotes: form.internalNotes.trim() || null
    })
    emit('update:modelValue', false)
    emit('blocked', props.customer)
  } catch (e) {
    notify.error(getApiErrorMessage(e))
  } finally { submitting.value = false }
}
</script>

<style scoped lang="scss">
.blacklist-dialog {
  width: 480px;
  max-width: 95vw;
}
.blacklist-dialog__header {
  padding-top: 12px;
  padding-bottom: 12px;
}
// Light, bordered info panel for the read-only customer details.
.blacklist-dialog__info {
  background: #f5f7fa;
  border: 1px solid #e3e8ef;
}
</style>
