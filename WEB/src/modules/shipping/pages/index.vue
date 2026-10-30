<template>
  <q-page class="app-page">
    <AppListHeader title="Shipping" :breadcrumbs="[{ label: 'Home', icon: 'o_home', to: '/dashboard' }, { label: 'Shipping' }]" :show-add="false" show-back @back="router.push('/dashboard')">
      <template v-if="saveStatus" #actions>
        <q-chip
          :icon="saveStatus.icon"
          :color="saveStatus.chip"
          :text-color="saveStatus.text"
          square
          dense
          class="text-caption q-my-none"
        >
          <q-spinner v-if="saveStatus.spin" size="14px" class="q-mr-xs" />
          {{ saveStatus.label }}
        </q-chip>
      </template>
    </AppListHeader>
    <q-card flat bordered class="app-tabs-toolbar q-mb-md">
      <div class="app-tabs-toolbar__body">
        <q-tabs v-model="tab" dense no-caps align="left" active-color="primary" indicator-color="primary" class="text-grey-7" @update:model-value="selectTab">
          <q-tab name="config" label="Configuration" icon="o_settings" />
          <q-tab name="methods" label="Methods" icon="o_local_shipping" :disable="!!methodsBlocked">
            <q-tooltip v-if="methodsBlocked">{{ methodsBlocked }}</q-tooltip>
          </q-tab>
          <q-tab name="zones" label="Zones" icon="o_map" />
        </q-tabs>

        <div v-if="isList" class="app-tabs-toolbar__actions">
          <q-input v-model="search" dense outlined debounce="400" :placeholder="searchPlaceholder" style="min-width: 220px">
            <template #prepend><q-icon name="o_search" /></template>
            <template v-if="search" #append><q-icon name="o_close" class="cursor-pointer" @click="search = ''" /></template>
          </q-input>
          <q-btn outline color="primary" no-caps icon="o_tune" label="Advanced" @click="panelRef?.openFilters()">
            <q-badge v-if="filterCount" color="red" floating>{{ filterCount }}</q-badge>
          </q-btn>
          <q-btn v-if="canWrite" color="primary" unelevated no-caps icon="o_add" :label="addLabel" @click="panelRef?.onAdd()" />
        </div>
      </div>
    </q-card>

    <component
      :is="activeComponent"
      ref="panelRef"
      :search="search"
      @filter-count="filterCount = $event"
      @save-status="saveStatus = $event"
      @methods-blocked="methodsBlocked = $event"
    />
  </q-page>
</template>

<script setup>
/* Shipping admin (WO-116): rate-source configuration + custom methods + zones sub-tabs in a bordered card.
 * Methods/Zones share the toolbar next to the tabs, which drives the active panel (dynamic component);
 * Configuration is not a list, so the toolbar is hidden there. */
import { ref, computed, watch, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { usePermissions } from 'composables/usePermissions'
import { shippingConfigApi, shippingMethodsBlockedReason } from 'modules/shipping/api'
import AppListHeader from 'components/common/AppListHeader.vue'
import ShippingMethodsPanel from 'modules/shipping/components/ShippingMethodsPanel.vue'
import ShippingZonesPanel from 'modules/shipping/components/ShippingZonesPanel.vue'
import ShippingConfigPanel from 'modules/shipping/components/ShippingConfigPanel.vue'

const route = useRoute()
const router = useRouter()
const { has } = usePermissions()
const canWrite = computed(() => has('Stores.Write'))

const TABS = { config: ShippingConfigPanel, methods: ShippingMethodsPanel, zones: ShippingZonesPanel }

const tab = ref(TABS[route.query.tab] ? route.query.tab : 'config')
const search = ref('')
const filterCount = ref(0)
const panelRef = ref(null)
// Only the auto-saving Configuration panel publishes a status; the list panels leave this null.
const saveStatus = ref(null)
// Why the Methods tab is unavailable, '' while it is usable — see shippingMethodsBlockedReason. Assume
// usable until we know: disabling the tab late beats flashing it disabled on every load.
const methodsBlocked = ref('')

const isZones = computed(() => tab.value === 'zones')
const isList = computed(() => tab.value !== 'config')
const activeComponent = computed(() => TABS[tab.value] || ShippingConfigPanel)
const searchPlaceholder = computed(() => (isZones.value ? 'Search zones' : 'Search methods'))
const addLabel = computed(() => (isZones.value ? 'New zone' : 'New method'))

/* q-tabs writes `tab` through v-model before calling this; a programmatic switch has to set it itself. */
function selectTab (value) {
  tab.value = value
  search.value = ''
  filterCount.value = 0
  // The list panels never emit a status, so clear it here or the chip would linger from Configuration.
  saveStatus.value = null
  router.replace({ query: { ...route.query, tab: value } })
}

/* The Configuration panel owns the config and publishes this while it is mounted, so only a landing on
 * another tab has to ask the server — the usual load still makes a single GET. */
async function loadMethodsState () {
  try {
    methodsBlocked.value = shippingMethodsBlockedReason(await shippingConfigApi.get())
  } catch {
    // Leave the tab enabled — a failed probe shouldn't be the thing that locks an admin out of Methods.
  }
}

// Nothing stops a ?tab=methods deep link (the method detail page returns through one), so once we know the
// tab is unavailable, move off it rather than leaving it active and greyed.
watch(methodsBlocked, (reason) => { if (reason && tab.value === 'methods') selectTab('config') })

onMounted(() => { if (tab.value !== 'config') loadMethodsState() })
</script>
