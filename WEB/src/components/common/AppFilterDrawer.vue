<template>
  <q-drawer
    :model-value="modelValue"
    side="right"
    bordered
    overlay
    elevated
    :width="width"
    :breakpoint="0"
    @update:model-value="$emit('update:modelValue', $event)"
  >
    <div class="column no-wrap full-height">
      <!-- Header -->
      <div class="row items-center q-px-md q-py-sm bg-primary text-white">
        <q-icon name="o_tune" class="q-mr-sm" />
        <div class="text-subtitle1 text-weight-medium col ellipsis">{{ title }}</div>
        <q-btn flat round dense icon="o_close" aria-label="Close" @click="$emit('update:modelValue', false)" />
      </div>
      <q-separator />

      <!-- Body: filter fields (work on change) -->
      <q-scroll-area class="col">
        <div class="q-pa-md column q-gutter-md">
          <slot />
        </div>
      </q-scroll-area>

      <!-- Footer: Clear all only (no apply — filters apply on change) -->
      <q-separator />
      <div class="row justify-between items-center q-pa-md">
        <q-btn flat no-caps color="grey-8" icon="o_restart_alt" label="Clear all" @click="$emit('clear')" />
        <q-btn flat no-caps color="primary" label="Done" @click="$emit('update:modelValue', false)" />
      </div>
    </div>
  </q-drawer>
</template>

<script setup>
/*
 * AppFilterDrawer: the portal-standard advanced-filter drawer. Filters live in the
 * default slot and apply on change (no Apply button); the footer carries only a
 * "Clear all" action (+ Done to close). Pair with a Quick Search + "Advanced"
 * button in the list header.
 */
defineProps({
  modelValue: { type: Boolean, default: false },
  title: { type: String, default: 'Filters' },
  width: { type: Number, default: 340 }
})

defineEmits(['update:modelValue', 'clear'])
</script>
