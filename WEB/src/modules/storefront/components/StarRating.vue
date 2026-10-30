<template>
  <span class="sf-stars" :aria-label="`Rated ${rounded} out of 5`">
    <q-icon
      v-for="n in 5"
      :key="n"
      :name="iconFor(n)"
    />
    <span v-if="showCount && count != null" class="sf-stars__count">({{ count }})</span>
  </span>
</template>

<script setup>
/*
 * Display-only star rating (WO-111). Renders a 0–5 average as full/half/empty
 * stars. Product rating data does not exist in the catalog DTOs yet (flagged on
 * WO-111); callers render this only when a rating is present.
 */
import { computed } from 'vue'

const props = defineProps({
  value: { type: Number, default: 0 },
  count: { type: Number, default: null },
  showCount: { type: Boolean, default: true }
})

const rounded = computed(() => Math.round((props.value || 0) * 2) / 2)

// MDI icons (mdi-v5 is loaded; the filled `material-icons` font is NOT, so bare
// `star` ligatures would render broken). MDI carries distinct solid/half/outline
// star glyphs, which the outlined-only material font cannot provide.
function iconFor (n) {
  if (rounded.value >= n) return 'mdi-star'
  if (rounded.value >= n - 0.5) return 'mdi-star-half-full'
  return 'mdi-star-outline'
}
</script>
