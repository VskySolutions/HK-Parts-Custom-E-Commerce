<template>
  <q-page class="app-page">
    <AppDetailHeader
      :title="isCreate ? 'New shipping method' : (entity?.name || 'Shipping method')"
      :breadcrumbs="[
        { label: 'Home', icon: 'o_home', to: '/dashboard' },
        { label: 'Shipping', to: { name: 'admin-shipping' } },
        { label: isCreate ? 'New method' : (entity?.name || 'Shipping method') }
      ]"
      :status="!isCreate && entity ? (form.isEnabled ? 'On' : 'Off') : ''"
      :status-color="form.isEnabled ? 'positive' : 'grey'"
      show-back
      @back="router.push({ name: 'admin-shipping', query: { tab: 'methods' } })"
    >
      <template #actions>
        <q-chip
          v-if="saveStatus"
          :icon="saveStatus.icon"
          :color="saveStatus.chip"
          :text-color="saveStatus.text"
          square
          dense
          class="q-mr-sm text-caption"
        >
          <q-spinner v-if="saveStatus.spin" size="14px" class="q-mr-xs" />
          {{ saveStatus.label }}
        </q-chip>
      </template>
    </AppDetailHeader>

    <q-inner-loading :showing="loading" color="primary" />

    <q-banner v-if="!loading && !isCreate && !entity" class="bg-grey-2 rounded-borders">
      Shipping method not found.
    </q-banner>

    <template v-if="isCreate || entity">
      <div v-if="!isCreate" class="row items-center text-caption text-grey-7 q-mb-sm q-px-xs">
        <q-icon name="o_cloud_sync" size="16px" class="q-mr-xs" />
        Changes are saved automatically as you edit — no need to press save.
      </div>

      <q-card flat bordered class="app-section">
        <q-tabs
          v-model="tab"
          align="left"
          active-color="primary"
          indicator-color="primary"
          class="text-grey-7 app-detail-tabs"
          no-caps
          inline-label
        >
          <q-tab name="general" icon="o_local_shipping" label="General" />
          <q-tab name="zones" icon="o_map" label="Zone rates" :disable="isCreate" />
        </q-tabs>

        <q-separator />

        <q-tab-panels v-model="tab" animated keep-alive>
          <!-- ============ GENERAL ============ -->
          <q-tab-panel name="general" class="q-gutter-y-sm">
            <AppTextField v-model="form.name" label="Name" required :v="v$.name" placeholder="e.g. Standard Shipping" :disable="!canWrite" />
            <AppSelect v-model="form.methodType" label="Type" :options="shippingMethodTypeOptions" :disable="!canWrite" />

            <AppTextField v-if="form.methodType === 'FlatRate'" v-model="form.flatRate" label="Flat rate" type="number" step="0.01" placeholder="e.g. 5.00" :disable="!canWrite" />
            <AppTextField v-if="form.methodType === 'FreeShipping'" v-model="form.freeShippingThreshold" label="Free over" type="number" step="0.01" placeholder="Order total for free shipping" :disable="!canWrite" />

            <!-- Weight/price rate tiers -->
            <template v-if="isTiered">
              <div class="row items-center justify-between q-mt-sm q-mb-xs">
                <AppFieldLabel :label="form.methodType === 'WeightBased' ? 'Weight tiers (kg)' : 'Price tiers (order total)'" />
                <q-btn v-if="canWrite" flat dense no-caps color="primary" icon="o_add" label="Add tier" @click="addTier" />
              </div>
              <div v-for="(t, i) in tiers" :key="i" class="row items-center q-col-gutter-sm q-mb-xs no-wrap">
                <div class="col"><q-input v-model.number="t.upTo" dense outlined type="number" step="0.01" :label="form.methodType === 'WeightBased' ? 'Up to kg' : 'Up to total'" :disable="!canWrite" @update:model-value="syncTiers" /></div>
                <div class="col"><q-input v-model.number="t.rate" dense outlined type="number" step="0.01" label="Rate" :disable="!canWrite" @update:model-value="syncTiers" /></div>
                <div class="col-auto"><q-btn v-if="canWrite" flat dense round size="sm" icon="o_delete" color="negative" @click="removeTier(i)" /></div>
              </div>
              <div v-if="!tiers.length" class="text-grey-6 text-caption q-mb-sm">No tiers yet — add one or more brackets.</div>
            </template>

            <q-separator class="q-my-md" />
            <div class="row q-col-gutter-sm items-center">
              <div class="col-6 col-md-3"><AppTextField v-model="form.transitDays" label="Delivery time (days)" type="number" placeholder="e.g. 5" :disable="!canWrite" /></div>
              <div class="col-6 col-md-3"><AppTextField v-model="form.displayOrder" label="Display order" type="number" :disable="!canWrite" /></div>
              <div class="col-auto q-mt-md"><q-toggle v-model="form.isEnabled" label="Enabled" color="primary" :disable="!canWrite" /></div>
            </div>
            <div class="text-caption text-grey-7">
              Shown to customers at checkout, and used to score this method when shipping selection is automatic.
              Leave blank if unknown.
            </div>
          </q-tab-panel>

          <!-- ============ ZONE RATES ============ -->
          <q-tab-panel name="zones" class="q-gutter-y-sm">
            <div class="text-body2 text-grey-7 q-mb-sm">Optional per-zone override rate (blank = the method's default rate applies).</div>
            <div v-if="!form.zoneRates.length" class="text-grey-6 text-caption q-mb-sm">No shipping zones defined yet.</div>
            <div v-for="z in form.zoneRates" :key="z.shippingZoneId" class="row items-center q-col-gutter-sm q-mb-xs no-wrap">
              <div class="col text-body2">{{ z.name }}</div>
              <div class="col-4"><q-input v-model.number="z.rate" dense outlined type="number" step="0.01" placeholder="Rate" :disable="!canWrite" /></div>
            </div>
          </q-tab-panel>
        </q-tab-panels>

        <template v-if="isCreate">
          <q-separator />
          <q-card-actions class="q-pa-md">
            <div class="text-caption text-grey-7">Create the method to unlock per-zone rates — all auto-saved from then on.</div>
            <q-space />
            <q-btn v-if="canWrite" unelevated color="primary" no-caps icon="o_check" label="Create method" :loading="creating" @click="create" />
          </q-card-actions>
        </template>
      </q-card>
    </template>

    <AppRecordMeta entity-type="shipping-method" :record-id="entity?.id" />
  </q-page>
</template>

<script setup>
/*
 * Shipping method create + manage page (full-page auto-save via useDetailForm). Tiers serialize into
 * form.tiersJson and per-zone override rates live in form.zoneRates, so both auto-save via the deep
 * form watch. Zone list is loaded and merged with any existing overrides.
 */
import { ref, computed } from 'vue'
import { useRouter } from 'vue-router'
import { usePermissions } from 'composables/usePermissions'
import { useDetailForm } from 'composables/useDetailForm'
import { required, maxLength } from 'validators'
import { shippingMethodApi, shippingZoneApi, shippingMethodTypeOptions } from 'modules/shipping/api'
import AppDetailHeader from 'components/common/AppDetailHeader.vue'
import AppTextField from 'components/common/AppTextField.vue'
import AppSelect from 'components/common/AppSelect.vue'
import AppFieldLabel from 'components/common/AppFieldLabel.vue'

const router = useRouter()
const { has } = usePermissions()
const canWrite = computed(() => has('Stores.Write'))

const tab = ref('general')

function num (v) { if (v === '' || v == null) return null; const n = Number(v); return Number.isFinite(n) ? n : null }

// Tier grid (view) — serialized into form.tiersJson so edits auto-save.
const tiers = ref([])
function parseTiers (json) {
  try { const a = json ? JSON.parse(json) : []; return Array.isArray(a) ? a.map((t) => ({ upTo: t.upTo ?? null, rate: t.rate ?? null })) : [] } catch (e) { return [] }
}
function syncTiers () {
  const clean = tiers.value.filter((t) => t.upTo != null && t.rate != null).map((t) => ({ upTo: Number(t.upTo), rate: Number(t.rate) }))
  form.tiersJson = clean.length ? JSON.stringify(clean) : null
}
function addTier () { tiers.value.push({ upTo: null, rate: null }) }
function removeTier (i) { tiers.value.splice(i, 1); syncTiers() }

// Per-zone rates: form.zoneRates = [{ shippingZoneId, name, rate }], merged from all zones + existing.
const allZones = ref([])
async function loadZones () {
  try {
    const r = await shippingZoneApi.list({ page: 1, pageSize: 200 })
    allZones.value = Array.isArray(r) ? r : r?.items || []
  } catch (e) { allZones.value = [] }
}
function syncZones (existing) {
  const byZone = {}
  for (const zr of existing || []) byZone[zr.shippingZoneId] = zr.rate
  form.zoneRates = allZones.value.map((z) => ({ shippingZoneId: z.id, name: z.name, rate: byZone[z.id] ?? null }))
}

const isTiered = computed(() => form.methodType === 'WeightBased' || form.methodType === 'PriceBased')

function buildPayload (f) {
  return {
    name: (f.name || '').trim(),
    methodType: f.methodType,
    flatRate: f.methodType === 'FlatRate' ? num(f.flatRate) : null,
    freeShippingThreshold: f.methodType === 'FreeShipping' ? num(f.freeShippingThreshold) : null,
    tiersJson: f.tiersJson || null,
    transitDays: num(f.transitDays),
    isEnabled: f.isEnabled,
    displayOrder: num(f.displayOrder) || 0,
    zoneRates: (f.zoneRates || []).filter((z) => z.rate != null && z.rate !== '').map((z) => ({ shippingZoneId: z.shippingZoneId, rate: Number(z.rate) }))
  }
}

const {
  form, v$, entity, loading, creating, isCreate, saveStatus, create
} = useDetailForm({
  createRouteName: 'shipping-method-new',
  detailRouteName: 'shipping-method-detail',
  entityLabel: 'method',
  api: shippingMethodApi,
  buildPayload,
  empty: {
    name: '', methodType: 'FlatRate', flatRate: null, freeShippingThreshold: null,
    tiersJson: null, transitDays: null, isEnabled: true, displayOrder: 0, zoneRates: []
  },
  rules: { name: { required, maxLength: maxLength(200) } },
  afterLoad: async (e) => {
    tiers.value = parseTiers(e.tiersJson)
    await loadZones()
    syncZones(e.zoneRates || [])
  },
  resetExtra: () => {
    tiers.value = []
    loadZones().then(() => syncZones([]))
  }
})
</script>

<style scoped lang="scss">
.app-detail-tabs {
  :deep(.q-tab) { min-height: 44px; }
}
</style>
