<template>
  <q-btn-dropdown
    v-if="currencies.length"
    flat
    no-caps
    dense
    icon="o_payments"
    :label="$q.screen.gt.sm ? currentLabel : ''"
    aria-label="Select currency"
  >
    <q-list style="min-width: 180px">
      <q-item
        v-for="cur in currencies"
        :key="cur.code"
        v-close-popup
        clickable
        :active="cur.code === selectedCode"
        active-class="text-primary"
        @click="select(cur.code)"
      >
        <q-item-section avatar>
          <span class="text-weight-medium">{{ cur.symbol || cur.code }}</span>
        </q-item-section>
        <q-item-section>
          {{ cur.code }}
          <q-item-label v-if="cur.isBase" caption>Base currency</q-item-label>
        </q-item-section>
        <q-item-section v-if="cur.code === selectedCode" side>
          <q-icon name="o_check" color="primary" />
        </q-item-section>
      </q-item>
    </q-list>
  </q-btn-dropdown>
</template>

<script setup>
/*
 * Currency selector (WO-26, AC-PRP-003.2): a dropdown of the enabled storefront
 * currencies backed by useCurrency. Hidden until at least one currency loads.
 * Selecting a currency updates the shared selection so every useCurrency.format()
 * across the storefront re-renders converted prices.
 */
import { computed, onMounted } from 'vue'
import { useQuasar } from 'quasar'
import { useCurrency } from 'modules/storefront/composables/useCurrency'

const $q = useQuasar()
const { currencies, selected, selectedCode, load, select } = useCurrency()

const currentLabel = computed(() => (selected.value ? selected.value.code : ''))

onMounted(load)
</script>
