<template>
  <q-drawer
    :model-value="modelValue"
    side="right"
    bordered
    overlay
    elevated
    :width="width"
    :breakpoint="0"
    @update:model-value="onDrawerToggle"
  >
    <div class="column no-wrap full-height">
      <!-- Header -->
      <div class="row items-center q-px-md q-py-sm bg-primary text-white">
        <div class="text-subtitle1 text-weight-medium col ellipsis">{{ title }}</div>
        <q-btn flat round dense icon="o_close" aria-label="Close" @click="close" />
      </div>
      <q-separator />

      <!-- Read-only body -->
      <q-scroll-area class="col">
        <div class="app-drawer-body">
          <slot />
        </div>
      </q-scroll-area>

      <!-- Footer -->
      <q-separator />
      <div class="row justify-end items-center q-gutter-sm q-pa-md">
        <slot name="footer" :close="close">
          <q-btn
            v-if="showEdit"
            flat
            color="primary"
            icon="o_tune"
            :label="editLabel"
            @click="$emit('edit')"
          />
          <q-btn unelevated color="grey-8" :label="closeLabel" @click="close" />
        </slot>
      </div>
    </div>
  </q-drawer>
</template>

<script setup>
/*
 * AppViewDrawer (WO-94 Step 10): read-only right-side detail drawer. Emits
 * `edit` (optional Edit button) and closes via update:modelValue(false).
 */
defineProps({
  modelValue: { type: Boolean, default: false },
  title: { type: String, default: '' },
  width: { type: Number, default: 460 },
  showEdit: { type: Boolean, default: false },
  editLabel: { type: String, default: 'Edit' },
  closeLabel: { type: String, default: 'Close' }
})

const emit = defineEmits(['update:modelValue', 'edit'])

function close () {
  emit('update:modelValue', false)
}

function onDrawerToggle (val) {
  emit('update:modelValue', val)
}
</script>
