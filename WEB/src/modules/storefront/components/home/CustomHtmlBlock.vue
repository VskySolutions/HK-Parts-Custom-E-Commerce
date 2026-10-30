<template>
  <div v-if="safeHtml" class="sf-section">
    <div class="sf-container">
      <!-- eslint-disable-next-line vue/no-v-html -->
      <div class="sf-html" v-html="safeHtml" />
    </div>
  </div>
</template>

<script setup>
/*
 * Custom HTML block (WO-100) — renders the CustomHtmlBlock home-page section's raw
 * markup. The authored HTML is run through a MINIMAL client-side sanitiser before
 * v-html: it strips <script> tags, inline on* event handler attributes and
 * javascript: URIs. This is defence-in-depth only — the authoritative sanitisation
 * is expected server-side when the section is authored/saved.
 */
import { computed } from 'vue'
import { sanitizeCmsHtml } from 'modules/storefront/catalog-cms-api'

const props = defineProps({
  html: { type: String, default: '' }
})

// Reuse the shared storefront sanitiser (DOMParser-based, regex fallback) rather than an inline
// one — defence-in-depth only; authoritative sanitisation happens server-side at authoring time.
const safeHtml = computed(() => sanitizeCmsHtml(props.html))
</script>

<style scoped lang="scss">
// Content is injected via v-html (unscoped), so reach it with :deep().
.sf-html { color: var(--sf-text); line-height: 1.6; }
.sf-html :deep(img) { max-width: 100%; height: auto; }
.sf-html :deep(h1),
.sf-html :deep(h2),
.sf-html :deep(h3) { color: var(--sf-heading); }
.sf-html :deep(a) { color: var(--sf-accent); }
.sf-html :deep(table) { max-width: 100%; overflow-x: auto; display: block; }
</style>
