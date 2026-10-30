<template>
  <q-card flat bordered class="app-section q-mb-md" :class="{ 'app-section--danger': danger }">
    <q-card-section v-if="hasHeader" class="row items-center q-gutter-sm app-section__head">
      <slot name="header">
        <div class="col">
          <div class="app-section__title" :class="{ 'text-negative': danger }">{{ title }}</div>
          <div v-if="subtitle" class="text-muted text-body2">{{ subtitle }}</div>
        </div>
      </slot>
      <slot name="actions" />
    </q-card-section>

    <q-separator v-if="hasHeader && separator" />

    <q-card-section>
      <slot />
    </q-card-section>

    <q-card-actions v-if="$slots.footer" align="right">
      <slot name="footer" />
    </q-card-actions>
  </q-card>
</template>

<script setup>
/*
 * AppSection: the standardized internal-page content card — a bordered rounded card with a
 * primary-coloured title, optional actions (top-right) and footer (bottom-right) slots, and a
 * `danger` variant (red border + red title) for destructive "Danger zone" panels. Use it to
 * compose detail pages (Basic information / Status / Danger zone) consistently across the portal.
 */
import { computed, useSlots } from 'vue'

const props = defineProps({
  title: { type: String, default: '' },
  subtitle: { type: String, default: '' },
  danger: { type: Boolean, default: false },
  separator: { type: Boolean, default: false }
})

const slots = useSlots()
const hasHeader = computed(() => !!(props.title || slots.header || slots.actions))
</script>
