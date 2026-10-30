<template>
  <div class="app-field">
    <AppFieldLabel v-if="label" :label="label" :required="required" />

    <q-select
      dense
      outlined
      emit-value
      map-options
      :model-value="modelValue"
      :options="options"
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
    </q-select>
  </div>
</template>

<script setup>
/*
 * AppSelect (WO-94 Step 10): dense/outlined q-select with AppFieldLabel and
 * Vuelidate error display. Defaults to emit-value + map-options.
 */
import { computed, useSlots } from 'vue'
import AppFieldLabel from './AppFieldLabel.vue'

defineOptions({ inheritAttrs: false })

const props = defineProps({
  modelValue: { type: [String, Number, Array, Object, Boolean], default: null },
  label: { type: String, default: '' },
  required: { type: Boolean, default: false },
  options: { type: Array, default: () => [] },
  v: { type: Object, default: null }
})

defineEmits(['update:modelValue'])

const slots = useSlots()
const passthroughSlots = computed(() => Object.keys(slots))

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
