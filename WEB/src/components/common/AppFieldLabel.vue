<template>
  <div class="app-field-label row items-center no-wrap q-mb-xs">
    <span>{{ label }}</span>
    <span v-if="required" class="text-negative q-ml-xs" aria-hidden="true">*</span>
    <q-space />
    <slot name="hint" />
  </div>
</template>

<script setup>
/* 15px form field label (WO-94 Step 10). Class .app-field-label -> typography.scss */
defineProps({
  label: { type: String, default: '' },
  required: { type: Boolean, default: false }
})
</script>
