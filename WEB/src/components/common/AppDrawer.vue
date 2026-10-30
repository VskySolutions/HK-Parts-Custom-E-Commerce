<template>
  <q-drawer
    :model-value="modelValue"
    side="right"
    bordered
    overlay
    elevated
    :width="width"
    :breakpoint="0"
    @update:model-value="(val) => emit('update:modelValue', val)"
  >
    <div class="column no-wrap full-height">
      <!-- Fixed header -->
      <div class="app-drawer__header row items-center q-px-md q-py-sm bg-primary text-white">
        <div class="text-subtitle1 text-weight-medium col ellipsis">{{ title }}</div>
        <q-btn
          flat
          round
          dense
          icon="mdi-close"
          aria-label="Close"
          @click="close"
        />
      </div>

      <q-separator />

      <!-- Scrollable content -->
      <q-scroll-area class="col">
        <div class="q-pa-md">
          <slot />
        </div>
      </q-scroll-area>

      <q-separator />

      <!-- Fixed footer -->
      <div class="app-drawer__footer row justify-end items-center q-gutter-sm q-pa-md">
        <q-btn flat label="Cancel" color="grey-8" @click="close" />
        <q-btn
          unelevated
          color="primary"
          label="Save"
          icon="mdi-content-save"
          :loading="saving"
          @click="emit('save')"
        />
      </div>
    </div>
  </q-drawer>
</template>

<script setup>
/*
 * AppDrawer - reusable right-side form drawer.
 *
 * - v-model controls open/close.
 * - Emits `save` (footer Save) and `update:modelValue=false` / `cancel` on close.
 * - Auto-save: when `formData` changes it is debounced-persisted (300ms) to a
 *   cookie keyed by `storageKey` (falls back to `title`). Call the exposed
 *   `clear()` after a successful submit to drop the draft.
 */
import { watch, onBeforeUnmount } from 'vue'
import { useQuasar } from 'quasar'

const props = defineProps({
  modelValue: { type: Boolean, default: false },
  title: { type: String, default: '' },
  saving: { type: Boolean, default: false },
  width: { type: Number, default: 420 },
  formData: { type: Object, default: () => ({}) },
  storageKey: { type: String, default: '' }
})

const emit = defineEmits(['update:modelValue', 'save', 'cancel'])

const $q = useQuasar()

function cookieKey () {
  const key = props.storageKey || props.title || 'form'
  return `drawer_${key.replace(/\s+/g, '_').toLowerCase()}`
}

function close () {
  emit('update:modelValue', false)
  emit('cancel')
}

let timer = null

watch(
  () => props.formData,
  (val) => {
    if (!val) return
    if (timer) clearTimeout(timer)
    timer = setTimeout(() => {
      $q.cookies.set(cookieKey(), val, { expires: 365, path: '/', sameSite: 'Lax' })
    }, 300)
  },
  { deep: true }
)

// Drop the persisted draft (call after a successful submit).
function clear () {
  if (timer) clearTimeout(timer)
  $q.cookies.remove(cookieKey(), { path: '/' })
}

onBeforeUnmount(() => {
  if (timer) clearTimeout(timer)
})

defineExpose({ clear })
</script>

<style scoped>
.app-drawer__header,
.app-drawer__footer {
  flex: 0 0 auto;
}
</style>
