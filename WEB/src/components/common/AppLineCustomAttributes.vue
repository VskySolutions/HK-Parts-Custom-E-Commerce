<template>
  <div v-if="values.length" class="app-line-attrs">
    <div v-for="attr in values" :key="attr.attributeId" class="app-line-attrs__row">
      <span class="app-line-attrs__key">{{ attr.name }}:</span>
      <span class="app-line-attrs__val">{{ attr.value }}</span>
    </div>
  </div>
</template>

<script setup>
/*
 * The custom-input values a buyer typed for one cart/order line (e.g. "Engraving: For Ana"),
 * rendered the same way everywhere a line appears — cart drawer, cart page, checkout summary,
 * storefront order history, admin order detail. Takes the line's `customAttributes` array straight
 * from the API; renders nothing when the line has none.
 */
import { computed } from 'vue'

const props = defineProps({
  // [{ attributeId, name, value }]
  attributes: { type: Array, default: () => [] }
})

const values = computed(() =>
  (props.attributes || []).filter((a) => a && String(a.value ?? '').trim())
)
</script>

<style scoped lang="scss">
.app-line-attrs {
  font-size: 12px;
  line-height: 1.5;
  color: #6b7280;
  margin-top: 2px;
}
.app-line-attrs__key {
  font-weight: 600;
  margin-right: 4px;
}
.app-line-attrs__val {
  word-break: break-word;
}
</style>
