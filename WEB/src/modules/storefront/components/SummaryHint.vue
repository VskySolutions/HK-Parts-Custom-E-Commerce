<template>
  <q-icon
    name="o_info"
    :size="size"
    class="q-ml-xs text-grey-6 cursor-pointer sf-summary-hint"
    tabindex="0"
  >
    <q-tooltip
      anchor="top middle"
      self="bottom middle"
      :offset="[0, 6]"
      class="text-body2 sf-summary-tooltip"
    >
      <slot>{{ text }}</slot>
    </q-tooltip>
  </q-icon>
</template>

<script setup>
/*
 * SummaryHint: a small info icon that reveals a short definition tooltip on hover / focus / tap. Placed
 * beside each order-summary label on checkout so a shopper can see what a line means and how the final
 * order total is built up. Pass plain text via the `text` prop, or richer content via the default slot.
 */
defineProps({
  text: { type: String, default: '' },
  size: { type: String, default: '15px' }
})
</script>

<style scoped>
.sf-summary-hint {
  vertical-align: middle;
}
</style>

<style>
/* The tooltip teleports to <body>, so it can't be reached by scoped styles — keep it readable + bounded. */
.sf-summary-tooltip {
  max-width: 260px;
  line-height: 1.4;
}
</style>
