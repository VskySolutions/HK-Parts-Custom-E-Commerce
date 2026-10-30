<template>
  <q-page class="app-page">
    <AppListHeader
      title="Attributes"
      subtitle="Manage the global attribute library used for variants and storefront filters."
      :breadcrumbs="[{ label: 'Home', icon: 'o_home', to: '/dashboard' }, { label: 'Catalog' }, { label: 'Attributes' }]"
      :show-add="false"
      show-back
      @back="router.push('/dashboard')"
    />

    <q-card flat bordered class="app-tabs-toolbar q-mb-md">
      <div class="app-tabs-toolbar__body">
        <q-tabs
          v-model="tab"
          dense
          no-caps
          align="left"
          active-color="primary"
          indicator-color="primary"
          class="text-grey-7"
          @update:model-value="onTab"
        >
          <q-tab name="product" label="Product Attributes" icon="o_palette" />
          <q-tab name="specification" label="Specification Attributes" icon="o_tune" />
        </q-tabs>

        <div class="app-tabs-toolbar__actions">
          <q-input v-model="search" dense outlined debounce="400" placeholder="Search attributes" style="min-width: 220px">
            <template #prepend><q-icon name="o_search" /></template>
            <template v-if="search" #append><q-icon name="o_close" class="cursor-pointer" @click="search = ''" /></template>
          </q-input>
          <q-btn outline color="primary" no-caps icon="o_tune" label="Advanced" @click="panelRef?.openFilters()">
            <q-badge v-if="filterCount" color="red" floating>{{ filterCount }}</q-badge>
          </q-btn>
          <q-btn v-if="canWrite" color="primary" unelevated no-caps icon="o_add" label="Add attribute" @click="panelRef?.onAdd()" />
        </div>
      </div>
    </q-card>

    <component :is="activeComponent" ref="panelRef" :search="search" @filter-count="filterCount = $event" />
  </q-page>
</template>

<script setup>
/*
 * Catalog → Attributes (WO-15): the global attribute library admin screen, with two
 * sub-tabs — Product Attributes (variant-driving) and Specification Attributes (facets).
 * The tabs sit on the left of a bordered card, the active panel's search/filter/add toolbar on
 * the right of the same row; only the active panel is mounted (dynamic component), which drives the
 * shared toolbar via an exposed onAdd/openFilters + a filter-count event. Deep-linkable via ?tab=.
 */
import { ref, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { usePermissions } from 'composables/usePermissions'
import AppListHeader from 'components/common/AppListHeader.vue'
import ProductAttributesPanel from 'modules/catalog/components/ProductAttributesPanel.vue'
import SpecificationAttributesPanel from 'modules/catalog/components/SpecificationAttributesPanel.vue'

const route = useRoute()
const router = useRouter()
const { has } = usePermissions()
const canWrite = computed(() => has('Catalog.Write'))

const tab = ref(route.query.tab === 'specification' ? 'specification' : 'product')
const search = ref('')
const filterCount = ref(0)
const panelRef = ref(null)

const activeComponent = computed(() =>
  tab.value === 'specification' ? SpecificationAttributesPanel : ProductAttributesPanel
)

function onTab (value) {
  search.value = ''
  filterCount.value = 0
  router.replace({ query: { ...route.query, tab: value } })
}
</script>
