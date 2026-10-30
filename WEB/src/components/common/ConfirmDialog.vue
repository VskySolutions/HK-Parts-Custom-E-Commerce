<template>
  <q-dialog
    :model-value="modelValue"
    @update:model-value="(val) => emit('update:modelValue', val)"
    @hide="onHide"
  >
    <q-card style="min-width: 340px; max-width: 90vw">
      <q-card-section class="row items-center no-wrap">
        <q-icon :name="icon" :color="color" size="28px" class="q-mr-sm" />
        <div class="text-h6">{{ title }}</div>
      </q-card-section>

      <q-card-section class="q-pt-none text-body2">
        {{ message }}
      </q-card-section>

      <q-card-actions align="right">
        <q-btn flat :label="cancelLabel" color="grey-8" @click="onCancel" />
        <q-btn unelevated :label="confirmLabel" :color="color" @click="onConfirm" />
      </q-card-actions>
    </q-card>
  </q-dialog>
</template>

<script setup>
/*
 * ConfirmDialog - reusable confirmation dialog used across admin screens.
 * v-model controls visibility; emits `confirm` / `cancel`.
 */
defineProps({
  modelValue: { type: Boolean, default: false },
  title: { type: String, default: 'Please confirm' },
  message: { type: String, default: 'Are you sure you want to proceed?' },
  confirmLabel: { type: String, default: 'Confirm' },
  cancelLabel: { type: String, default: 'Cancel' },
  color: { type: String, default: 'primary' },
  icon: { type: String, default: 'mdi-help-circle' }
})

const emit = defineEmits(['update:modelValue', 'confirm', 'cancel'])

// Guards against emitting `cancel` twice when closing via a confirm/cancel button.
let resolved = false

function onConfirm () {
  resolved = true
  emit('confirm')
  emit('update:modelValue', false)
}

function onCancel () {
  resolved = true
  emit('cancel')
  emit('update:modelValue', false)
}

function onHide () {
  if (!resolved) {
    emit('cancel')
  }
  resolved = false
}
</script>
