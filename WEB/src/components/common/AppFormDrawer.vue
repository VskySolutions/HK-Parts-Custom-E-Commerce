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
    <div class="column no-wrap full-height app-form-drawer">
      <!-- Left-edge resize handle -->
      <div class="app-drawer-resizer" @mousedown.prevent="startResize" />

      <!-- Header -->
      <div class="row items-center q-px-md q-py-sm bg-primary text-white">
        <div class="text-subtitle1 text-weight-medium col ellipsis">{{ title }}</div>
        <q-btn flat round dense icon="o_close" aria-label="Close" @click="onCancel" />
      </div>
      <q-separator />

      <!-- Body -->
      <q-scroll-area class="col">
        <q-form ref="formRef" class="app-drawer-body" @submit.prevent="onSubmit">
          <slot :submit="onSubmit" :cancel="onCancel" />
        </q-form>
      </q-scroll-area>

      <!-- Footer -->
      <q-separator />
      <div class="row justify-end items-center q-gutter-sm q-pa-md">
        <slot name="footer" :submit="onSubmit" :cancel="onCancel">
          <q-btn flat :label="cancelLabel" color="grey-8" @click="onCancel" />
          <q-btn
            unelevated
            color="primary"
            :label="submitLabel"
            icon="o_save"
            :loading="saving"
            @click="onSubmit"
          />
        </slot>
      </div>
    </div>
  </q-drawer>
</template>

<script setup>
/*
 * AppFormDrawer (WO-94 Step 10): right-side form drawer with a resizable width
 * persisted to LocalStorage. Wraps content in a q-form; Save validates the form
 * then emits `submit`. Close / Cancel emit `cancel` + update:modelValue(false).
 */
import { ref, onBeforeUnmount } from 'vue'
import { STORAGE_KEYS, getItem, setItem } from 'services/storage'

const props = defineProps({
  modelValue: { type: Boolean, default: false },
  title: { type: String, default: '' },
  saving: { type: Boolean, default: false },
  submitLabel: { type: String, default: 'Save' },
  cancelLabel: { type: String, default: 'Cancel' },
  minWidth: { type: Number, default: 320 },
  maxWidth: { type: Number, default: 720 },
  defaultWidth: { type: Number, default: 460 },
  validate: { type: Boolean, default: true }
})

const emit = defineEmits(['update:modelValue', 'submit', 'cancel'])

const formRef = ref(null)

const stored = Number(getItem(STORAGE_KEYS.FORM_DRAWER_WIDTH, props.defaultWidth))
const width = ref(
  Number.isFinite(stored) && stored >= props.minWidth ? stored : props.defaultWidth
)

// ---- Resize ---------------------------------------------------------------
let startX = 0
let startWidth = 0

function onMove (e) {
  const delta = startX - e.clientX // dragging left widens the right drawer
  let next = startWidth + delta
  next = Math.min(Math.max(next, props.minWidth), props.maxWidth)
  width.value = next
}

function stopResize () {
  window.removeEventListener('mousemove', onMove)
  window.removeEventListener('mouseup', stopResize)
  setItem(STORAGE_KEYS.FORM_DRAWER_WIDTH, width.value)
}

function startResize (e) {
  startX = e.clientX
  startWidth = width.value
  window.addEventListener('mousemove', onMove)
  window.addEventListener('mouseup', stopResize)
}

onBeforeUnmount(stopResize)

// ---- Submit / cancel ------------------------------------------------------
async function onSubmit () {
  if (props.validate && formRef.value && typeof formRef.value.validate === 'function') {
    const ok = await formRef.value.validate()
    if (!ok) return
  }
  emit('submit')
}

function onCancel () {
  emit('cancel')
  emit('update:modelValue', false)
}

function onDrawerToggle (val) {
  emit('update:modelValue', val)
  if (!val) emit('cancel')
}

defineExpose({
  resetValidation: () => formRef.value && formRef.value.resetValidation && formRef.value.resetValidation()
})
</script>
