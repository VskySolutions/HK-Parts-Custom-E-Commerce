<template>
  <div class="app-field">
    <AppFieldLabel v-if="label" :label="label" :required="required">
      <template v-if="$slots.hint" #hint><slot name="hint" /></template>
    </AppFieldLabel>

    <q-input
      dense
      outlined
      :model-value="modelValue"
      :error="hasError"
      :error-message="errorMessage"
      hide-bottom-space
      v-bind="$attrs"
      @update:model-value="$emit('update:modelValue', $event)"
      @blur="onBlur"
    >
      <template v-for="name in passthroughSlots" #[name]="slotProps" :key="name">
        <slot :name="name" v-bind="slotProps || {}" />
      </template>
    </q-input>
  </div>
</template>

<script setup>
/*
 * AppTextField (WO-94 Step 10): dense/outlined q-input wrapped with a 15px
 * AppFieldLabel and Vuelidate error display. Pass a Vuelidate field via `v`
 * (e.g. :v="v$.name") to drive :error / :error-message and $touch on blur.
 * Any extra q-input props/slots (prepend/append/...) pass straight through.
 */
import { computed, useSlots } from 'vue'
import AppFieldLabel from './AppFieldLabel.vue'

defineOptions({ inheritAttrs: false })

const props = defineProps({
  modelValue: { type: [String, Number], default: '' },
  label: { type: String, default: '' },
  required: { type: Boolean, default: false },
  v: { type: Object, default: null }
})

defineEmits(['update:modelValue'])

const slots = useSlots()

// Forward every slot except the label "hint" (consumed above).
const passthroughSlots = computed(() =>
  Object.keys(slots).filter((name) => name !== 'hint')
)

const hasError = computed(() => !!(props.v && props.v.$error))
const errorMessage = computed(() => {
  if (props.v && Array.isArray(props.v.$errors) && props.v.$errors.length) {
    return props.v.$errors[0].$message
  }
  return undefined
})

function onBlur () {
  if (props.v && typeof props.v.$touch === 'function') props.v.$touch()
}
</script>
