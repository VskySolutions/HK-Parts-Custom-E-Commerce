<template>
  <q-page class="app-page">
    <AppListHeader
      title="Promotions"
      :breadcrumbs="[{ label: 'Home', icon: 'o_home', to: '/dashboard' }, { label: 'Promotions' }]"
      :show-add="false"
      show-back
      @back="router.push('/dashboard')"
    />
    <q-card flat bordered class="app-tabs-toolbar q-mb-md">
      <div class="app-tabs-toolbar__body">
        <q-tabs v-model="tab" dense no-caps align="left" active-color="primary" indicator-color="primary" class="text-grey-7" @update:model-value="onTab">
          <q-tab name="discounts" label="Discounts" icon="o_local_offer" />
          <q-tab name="coupons" label="Coupon Codes" icon="o_confirmation_number" />
        </q-tabs>

        <div class="app-tabs-toolbar__actions">
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

    <component :is="activeComponent" ref="panelRef" :search="search" @filter-count="filterCount = $event" />
  </q-page>
</template>

<script setup>
/* Promotions admin (WO-115): Discounts + Coupon Codes sub-tabs in a bordered card, with a shared
 * toolbar next to the tabs driving the active panel (dynamic component). */
import { ref, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { usePermissions } from 'composables/usePermissions'
import AppListHeader from 'components/common/AppListHeader.vue'
import DiscountsPanel from 'modules/pricing/components/DiscountsPanel.vue'
import CouponsPanel from 'modules/pricing/components/CouponsPanel.vue'

const route = useRoute()
const router = useRouter()
const { has } = usePermissions()
const canWrite = computed(() => has('Catalog.Write'))

const tab = ref(route.query.tab === 'coupons' ? 'coupons' : 'discounts')
const search = ref('')
const filterCount = ref(0)
const panelRef = ref(null)

const isCoupons = computed(() => tab.value === 'coupons')
const activeComponent = computed(() => (isCoupons.value ? CouponsPanel : DiscountsPanel))
const searchPlaceholder = computed(() => (isCoupons.value ? 'Search codes' : 'Search discounts'))
const addLabel = computed(() => (isCoupons.value ? 'New coupon' : 'New discount'))

function onTab (value) {
  search.value = ''
  filterCount.value = 0
  router.replace({ query: { ...route.query, tab: value } })
}
</script>
