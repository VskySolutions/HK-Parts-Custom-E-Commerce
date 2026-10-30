<template>
  <AppTextField
    :model-value="modelValue"
    :label="label"
    placeholder="#RRGGBB"
    maxlength="32"
    :v="v"
    @update:model-value="$emit('update:modelValue', $event)"
  >
    <template #append>
      <div class="color-swatch q-mr-xs" :style="{ background: modelValue || '#e0e0e0' }" />
      <q-icon name="o_colorize" class="cursor-pointer" aria-label="Pick colour">
        <q-popup-proxy cover transition-show="scale" transition-hide="scale">
          <q-color
            :model-value="modelValue || '#000000'"
            format-model="hex"
            @update:model-value="$emit('update:modelValue', $event)"
          />
        </q-popup-proxy>
      </q-icon>
    </template>
  </AppTextField>
</template>

<script setup>
/*
 * ColorField (WO-9): a hex-colour input built on the globally registered
 * AppTextField, with a live swatch and a q-color popup picker in the append
 * slot. Emits the hex string via update:modelValue and forwards a Vuelidate
 * field via `v` for error display.
 */
defineProps({
  modelValue: { type: String, default: '' },
  label: { type: String, default: '' },
  v: { type: Object, default: null }
})

defineEmits(['update:modelValue'])
</script>

<style scoped>
.color-swatch {
  width: 20px;
  height: 20px;
  border-radius: 4px;
  border: 1px solid rgba(0, 0, 0, 0.15);
}
</style>
