<template>
  <div class="app-field">
    <AppFieldLabel v-if="label" :label="label" :required="required" />

    <q-input
      dense
      outlined
      :model-value="displayValue"
      :error="hasError"
      :error-message="errorMessage"
      readonly
      hide-bottom-space
      v-bind="$attrs"
      @blur="onBlur"
    >
      <template #append>
        <q-icon name="o_event" class="cursor-pointer">
          <q-popup-proxy cover transition-show="scale" transition-hide="scale">
            <q-date
              :model-value="modelValue"
              :mask="mask"
              today-btn
              @update:model-value="onPick"
            >
              <div class="row items-center justify-end q-gutter-sm">
                <q-btn v-close-popup label="Clear" flat color="grey-8" @click="onPick(null)" />
                <q-btn v-close-popup label="OK" flat color="primary" />
              </div>
            </q-date>
          </q-popup-proxy>
        </q-icon>
      </template>
    </q-input>
  </div>
</template>

<script setup>
/*
 * AppDateField (WO-94 Step 10): dense date picker built on q-input + q-date.
 * Model value is a date string in `mask` format (default yyyy-MM-dd). The
 * human-readable display is formatted with date-fns.
 */
import { computed } from 'vue'
import { parse, isValid, format } from 'date-fns'
import AppFieldLabel from './AppFieldLabel.vue'

defineOptions({ inheritAttrs: false })

const props = defineProps({
  modelValue: { type: String, default: '' },
  label: { type: String, default: '' },
  required: { type: Boolean, default: false },
  mask: { type: String, default: 'YYYY-MM-DD' },
  displayFormat: { type: String, default: 'dd MMM yyyy' },
  v: { type: Object, default: null }
})

const emit = defineEmits(['update:modelValue'])

// q-date mask (Quasar tokens) vs date-fns tokens — parse using an ISO layout.
const displayValue = computed(() => {
  if (!props.modelValue) return ''
  const parsed = parse(props.modelValue, 'yyyy-MM-dd', new Date())
  return isValid(parsed) ? format(parsed, props.displayFormat) : props.modelValue
})

function onPick (val) {
  emit('update:modelValue', val || '')
  onBlur()
}

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
