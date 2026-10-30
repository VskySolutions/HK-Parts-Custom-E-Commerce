<template>
  <q-btn-dropdown
    flat
    no-caps
    dense
    icon="o_language"
    :label="$q.screen.gt.sm ? currentLabel : ''"
    aria-label="Select language"
  >
    <q-list style="min-width: 160px">
      <q-item
        v-for="loc in locales"
        :key="loc.value"
        v-close-popup
        clickable
        :active="loc.value === locale"
        active-class="text-primary"
        @click="select(loc.value)"
      >
        <q-item-section>{{ loc.label }}</q-item-section>
        <q-item-section v-if="loc.value === locale" side>
          <q-icon name="o_check" color="primary" />
        </q-item-section>
      </q-item>
    </q-list>
  </q-btn-dropdown>
</template>

<script setup>
/*
 * Language selector (WO-19): switches the global vue-i18n locale. Only en-US is
 * available today; the `locales` list is structured so more can be added by
 * registering the locale under src/i18n and appending an entry here.
 */
import { computed } from 'vue'
import { useQuasar } from 'quasar'
import { useI18n } from 'vue-i18n'

const $q = useQuasar()
const { locale } = useI18n({ useScope: 'global' })

const locales = [
  { value: 'en-US', label: 'English' }
]

const currentLabel = computed(() => {
  const match = locales.find((l) => l.value === locale.value)
  return match ? match.label : locale.value
})

function select (value) {
  locale.value = value
}
</script>
