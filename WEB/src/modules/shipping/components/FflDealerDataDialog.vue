<template>
  <!-- Update Dealer Data (FFL-94/95): whether the dealer accepts transfers and the last transfer fee.
       Pre-filled with the stored values; the fee is only changed when one is entered. -->
  <q-dialog :model-value="modelValue" @update:model-value="$emit('update:modelValue', $event)">
    <q-card style="width: 460px; max-width: 95vw">
      <q-card-section class="text-h6">Dealer Data for {{ dealer?.name || 'dealer' }}</q-card-section>
      <q-separator />
      <q-card-section v-if="dealer" class="column q-gutter-md">
        <div class="text-body2">
          <div>{{ dealer.street }}, {{ dealer.city }}, {{ dealer.state }} {{ dealer.zip }}</div>
          <div class="text-caption text-grey-7">FFL {{ dealer.licenseNumber }}</div>
        </div>
        <AppSelect
          v-model="allowsTransfers"
          label="Allows Transfers?"
          :options="[{ label: 'Yes', value: true }, { label: 'No', value: false }]"
          hint="No hides this dealer from every dealer search."
        />
        <AppTextField
          v-model="fee"
          label="Transfer Fee"
          type="number"
          min="0"
          step="0.01"
          prefix="$"
          :hint="dealer.lastTransferFee != null ? `Current: $${Number(dealer.lastTransferFee).toFixed(2)}. Leave empty to keep it.` : 'Leave empty if unknown.'"
          :error="!!feeError"
          :error-message="feeError"
        />
      </q-card-section>
      <q-separator />
      <q-card-actions align="right">
        <q-btn flat no-caps label="Cancel" @click="$emit('update:modelValue', false)" />
        <q-btn unelevated no-caps color="primary" label="Save" :loading="saving" :disable="!!feeError" @click="save" />
      </q-card-actions>
    </q-card>
  </q-dialog>
</template>

<script setup>
import { ref, computed, watch } from 'vue'
import { fflAdminApi } from 'modules/shipping/ffl-api'
import { getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'

const props = defineProps({
  modelValue: { type: Boolean, default: false },
  dealer: { type: Object, default: null }
})
const emit = defineEmits(['update:modelValue', 'saved'])
const notify = useNotify()

const allowsTransfers = ref(true)
const fee = ref('')
const saving = ref(false)

const feeError = computed(() => {
  if (fee.value === '' || fee.value == null) return ''
  const n = Number(fee.value)
  return Number.isFinite(n) && n >= 0 ? '' : 'Enter an amount of 0 or more.'
})

watch(() => [props.modelValue, props.dealer], () => {
  if (!props.modelValue || !props.dealer) return
  allowsTransfers.value = props.dealer.allowsTransfers !== false
  fee.value = ''
}, { immediate: true })

async function save () {
  if (!props.dealer || feeError.value) return
  saving.value = true
  try {
    const updated = await fflAdminApi.updateDealerData(props.dealer.key, {
      key: props.dealer.key,
      allowsTransfers: allowsTransfers.value,
      lastTransferFee: fee.value === '' || fee.value == null ? null : Number(fee.value)
    })
    notify.success('Dealer Data updated successfully')
    emit('saved', updated)
    emit('update:modelValue', false)
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    saving.value = false
  }
}
</script>
