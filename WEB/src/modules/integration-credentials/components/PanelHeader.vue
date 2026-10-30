<template>
  <q-card flat bordered class="panel-header q-mb-md">
    <div class="row items-center no-wrap q-px-md q-py-sm">
      <q-icon :name="item.icon" size="26px" class="q-mr-sm text-primary" />
      <div class="col ic-title-col">
        <div class="row items-center no-wrap ic-title-row">
          <span class="text-subtitle1 text-weight-medium ellipsis">{{ item.label }}</span>

          <q-badge v-if="pricing" :color="pricing.color" rounded class="ic-price-badge q-ml-sm">
            <q-icon :name="pricing.icon" size="12px" class="q-mr-xs" />{{ pricing.label }}
            <q-tooltip anchor="top middle" self="bottom middle" :offset="[0, 6]">{{ pricing.tip }}</q-tooltip>
          </q-badge>

          <q-btn
            v-if="item.website"
            flat round dense size="sm"
            color="primary"
            icon="o_open_in_new"
            class="q-ml-xs"
            type="a"
            :href="item.website"
            target="_blank"
            rel="noopener noreferrer"
          >
            <q-tooltip anchor="top middle" self="bottom middle" :offset="[0, 6]">
              Open the {{ item.label }} console — sign up &amp; get your API credentials
            </q-tooltip>
          </q-btn>
        </div>
        <div class="text-caption text-grey-7 ellipsis-2-lines">{{ item.description }}</div>
      </div>
      <div class="row items-center q-gutter-sm no-wrap q-ml-md">
        <slot name="actions" />
      </div>
    </div>
  </q-card>
</template>

<script setup>
/* PanelHeader: card-styled title row (icon + label + Free/Paid badge + external sign-up link + description)
 * with an `actions` slot for the panel's buttons (Reload / Add / New / Save). Shared by every Integrations-hub
 * panel for a consistent look. The badge and link come from the item's `pricing` / `website` metadata. */
import { computed } from 'vue'

const props = defineProps({ item: { type: Object, required: true } })

// Free/Paid pill presentation, keyed off the integration's `pricing` (absent for internal settings panels).
const PRICING = {
  free: { label: 'Free', color: 'green-6', icon: 'o_money_off', tip: 'Free to integrate — the provider offers free API access.' },
  paid: { label: 'Paid', color: 'deep-orange-6', icon: 'o_paid', tip: 'Paid service — transaction, usage or subscription fees apply.' }
}
const pricing = computed(() => PRICING[props.item.pricing] || null)
</script>

<style scoped>
.panel-header {
  border-radius: 8px;
}
.ic-title-col {
  min-width: 0;
}
.ic-title-row .ic-price-badge {
  font-size: 10px;
  font-weight: 600;
  letter-spacing: 0.3px;
  text-transform: uppercase;
  padding: 2px 6px;
}
</style>
