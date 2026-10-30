<template>
  <div class="app-field">
    <AppFieldLabel v-if="label" :label="label" :required="required">
      <template v-if="$slots.hint" #hint><slot name="hint" /></template>
    </AppFieldLabel>

    <q-input
      dense
      outlined
      :model-value="modelValue"
      :type="reveal ? 'text' : 'password'"
      :error="hasError"
      :error-message="errorMessage"
      :autocomplete="autocomplete"
      hide-bottom-space
      v-bind="$attrs"
      @update:model-value="$emit('update:modelValue', $event)"
      @blur="onBlur"
    >
      <template v-if="$slots.prepend" #prepend><slot name="prepend" /></template>
      <template #append>
        <slot name="append" />
        <q-icon
          :name="reveal ? 'o_visibility_off' : 'o_visibility'"
          class="cursor-pointer"
          @click="reveal = !reveal"
        >
          <q-tooltip>{{ reveal ? 'Hide password' : 'Show password' }}</q-tooltip>
        </q-icon>
      </template>
    </q-input>

    <div v-if="strength && modelValue" class="app-password-strength q-mt-xs">
      <q-linear-progress
        :value="meter.score / 4"
        :color="meter.color"
        track-color="grey-3"
        size="4px"
        rounded
      />
      <div class="row justify-between items-center q-mt-xs">
        <span class="text-caption" :class="`text-${meter.color}`">{{ meter.label }}</span>
        <span class="text-caption text-grey-6">{{ policyHint }}</span>
      </div>
    </div>
  </div>
</template>

<script setup>
/*
 * AppPasswordField: the portal-wide password input. Wraps a dense/outlined q-input with the
 * standard AppFieldLabel and adds (1) a built-in show/hide mask toggle, (2) an optional
 * strength meter, and (3) error display driven by either a Vuelidate field (`:v`) OR Quasar
 * `:rules` (passed straight through to the q-input, so QForm validation still works).
 * Policy (8–16 chars, letter + number) lives in validators/index.js and is mirrored on the API.
 */
import { computed, ref, useSlots } from 'vue'
import AppFieldLabel from './AppFieldLabel.vue'
import { passwordStrength, PASSWORD_MIN, PASSWORD_MAX } from 'validators'

defineOptions({ inheritAttrs: false })

const props = defineProps({
  modelValue: { type: [String, Number], default: '' },
  label: { type: String, default: '' },
  required: { type: Boolean, default: false },
  v: { type: Object, default: null },
  // Show the strength meter (use on "new password" fields, not on "current password"/login).
  strength: { type: Boolean, default: false },
  autocomplete: { type: String, default: 'new-password' }
})

defineEmits(['update:modelValue'])
useSlots()

const reveal = ref(false)

const hasError = computed(() => !!(props.v && props.v.$error))
const errorMessage = computed(() => {
  if (props.v && Array.isArray(props.v.$errors) && props.v.$errors.length) {
    return props.v.$errors[0].$message
  }
  return undefined
})

const meter = computed(() => passwordStrength(props.modelValue))
const policyHint = `${PASSWORD_MIN}–${PASSWORD_MAX} chars`

function onBlur () {
  if (props.v && typeof props.v.$touch === 'function') props.v.$touch()
}
</script>
