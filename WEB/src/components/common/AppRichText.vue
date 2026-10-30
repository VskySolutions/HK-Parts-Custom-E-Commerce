<template>
  <div class="app-field app-richtext">
    <AppFieldLabel v-if="label" :label="label" :required="required">
      <template v-if="$slots.hint || hint" #hint>
        <slot name="hint">{{ hint }}</slot>
      </template>
    </AppFieldLabel>

    <div class="app-richtext__wrap" :class="{ 'app-richtext__wrap--error': hasError }">
      <q-editor
        ref="editorRef"
        :model-value="modelValue || ''"
        :min-height="minHeight"
        :toolbar="toolbar"
        :definitions="definitions"
        flat
        content-class="app-richtext__content"
        @update:model-value="onInput"
        @blur="onBlur"
      />
      <div v-if="showPlaceholder" class="app-richtext__placeholder">{{ placeholder }}</div>
    </div>

    <div v-if="errorMessage" class="app-richtext__error">{{ errorMessage }}</div>
  </div>
</template>

<script setup>
/*
 * AppRichText: the portal-standard rich text editor (WYSIWYG) that replaces plain
 * "description" textareas across the app. Wraps q-editor with an AppFieldLabel
 * (label + hint), an empty-state placeholder, and Vuelidate error display (pass
 * :v="v$.field"). v-model is HTML.
 *
 * Paste hygiene: the editor exposes NO font-family / font-size controls, so any inline
 * font styling in the content can only have come from pasting (IDEs, Word, Google Docs).
 * That foreign font was being saved into the HTML and then rendered in a different font
 * everywhere the content is shown. On paste we strip font-family / font-size / font and
 * <font> tags (plus Word/Docs junk) so stored content always uses the app's font family.
 */
import { computed, ref, onMounted, onBeforeUnmount } from 'vue'
import AppFieldLabel from './AppFieldLabel.vue'

const props = defineProps({
  modelValue: { type: String, default: '' },
  label: { type: String, default: '' },
  required: { type: Boolean, default: false },
  hint: { type: String, default: '' },
  placeholder: { type: String, default: 'Write a description…' },
  minHeight: { type: String, default: '8rem' },
  v: { type: Object, default: null }
})

const emit = defineEmits(['update:modelValue'])

const editorRef = ref(null)

const toolbar = [
    ['bold', 'italic', 'underline', 'strike'],
    [{ label: 'Format', icon: 'o_format_size', list: 'no-icons', options: ['p', 'h2', 'h3', 'h4'] }],
    ['unordered', 'ordered'],
    ['quote', 'link'],
    ['undo', 'redo'],
    ['removeFormat']
  ]

const definitions = {
  link: { label: 'Link', icon: 'o_link' }
}

// Placeholder shows only when the editor has no meaningful content.
const showPlaceholder = computed(() => {
  const v = props.modelValue || ''
  const stripped = v.replace(/<[^>]*>/g, '').replace(/&nbsp;/g, '').trim()
  return stripped.length === 0
})

const hasError = computed(() => !!(props.v && props.v.$error))
const errorMessage = computed(() => {
  if (props.v && Array.isArray(props.v.$errors) && props.v.$errors.length) {
    return props.v.$errors[0].$message
  }
  return undefined
})

function onInput (value) {
  emit('update:modelValue', value)
}

function onBlur () {
  if (props.v && typeof props.v.$touch === 'function') props.v.$touch()
}

// --- Paste sanitisation -------------------------------------------------------
// Inline declarations we always drop from pasted markup (the toolbar can't set them,
// so they're only ever pasted-in and break font consistency).
const DROP_DECLS = new Set(['font-family', 'font-size', 'font'])

function cleanNode (el) {
  const style = el.getAttribute('style')
  if (style) {
    const kept = style
      .split(';')
      .map((s) => s.trim())
      .filter(Boolean)
      .filter((decl) => {
        const prop = decl.slice(0, decl.indexOf(':')).trim().toLowerCase()
        return prop && !DROP_DECLS.has(prop) && !prop.startsWith('mso-')
      })
    if (kept.length) el.setAttribute('style', kept.join('; '))
    else el.removeAttribute('style')
  }
  // <font face|size>, and doc-specific classes (e.g. Word "MsoNormal") that carry fonts.
  el.removeAttribute('face')
  el.removeAttribute('size')
  el.removeAttribute('class')
}

function unwrap (node) {
  const parent = node.parentNode
  if (!parent) return
  while (node.firstChild) parent.insertBefore(node.firstChild, node)
  parent.removeChild(node)
}

function sanitizePasted (html) {
  try {
    const doc = new DOMParser().parseFromString(html, 'text/html')
    // Drop non-content wrappers Word/Docs inject (style blocks are the usual font source).
    doc.querySelectorAll('style, meta, link, title, script').forEach((n) => n.remove())
    doc.body.querySelectorAll('*').forEach(cleanNode)
    doc.body.querySelectorAll('font').forEach(unwrap)
    return doc.body.innerHTML
  } catch (e) {
    // Defensive fallback: crude regex strip of the font bits.
    return html
      .replace(/\sface="[^"]*"/gi, '')
      .replace(/font-(family|size)\s*:[^;"']*;?/gi, '')
  }
}

function escapeText (text) {
  return text
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/\r?\n/g, '<br>')
}

function onPaste (e) {
  const cb = e.clipboardData || window.clipboardData
  if (!cb) return
  const html = cb.getData('text/html')
  if (!html) return // plain-text paste carries no font styling — let the browser handle it

  e.preventDefault()
  let cleaned = sanitizePasted(html)
  // If stripping left no real content, fall back to the plain-text alternative.
  if (!cleaned.replace(/<[^>]*>/g, '').replace(/&nbsp;/g, '').trim()) {
    cleaned = escapeText(cb.getData('text/plain') || '')
  }
  // execCommand keeps the caret/undo stack intact and fires `input`, so q-editor
  // re-reads the content and emits update:model-value on its own.
  document.execCommand('insertHTML', false, cleaned)
}

function contentEl () {
  return editorRef.value && typeof editorRef.value.getContentEl === 'function'
    ? editorRef.value.getContentEl()
    : null
}

onMounted(() => {
  const el = contentEl()
  // Capture phase so we strip fonts before any default/native paste insertion.
  if (el) el.addEventListener('paste', onPaste, true)
})

onBeforeUnmount(() => {
  const el = contentEl()
  if (el) el.removeEventListener('paste', onPaste, true)
})
</script>

<style scoped lang="scss">
.app-richtext__wrap {
  position: relative;
  border: 1px solid rgba(0, 0, 0, 0.24);
  border-radius: 4px;
  overflow: hidden;
}
.app-richtext__wrap--error {
  border-color: var(--q-negative);
}
.app-richtext__placeholder {
  position: absolute;
  left: 12px;
  bottom: 10px;
  color: rgba(0, 0, 0, 0.45);
  font-size: 13px;
  pointer-events: none;
}
.app-richtext__error {
  color: var(--q-negative);
  font-size: 11px;
  margin-top: 4px;
}
:deep(.app-richtext__content) {
  min-height: 6rem;
  // Always render in the app font while editing (WYSIWYG matches the saved output).
  font-family: inherit;
}
</style>
