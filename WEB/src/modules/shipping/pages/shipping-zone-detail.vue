<template>
  <q-page class="app-page">
    <AppDetailHeader
      :title="isCreate ? 'New shipping zone' : (entity?.name || 'Shipping zone')"
      :breadcrumbs="[
        { label: 'Home', icon: 'o_home', to: '/dashboard' },
        { label: 'Shipping', to: { name: 'admin-shipping' } },
        { label: isCreate ? 'New zone' : (entity?.name || 'Shipping zone') }
      ]"
      :status="!isCreate && entity ? (form.isEnabled ? 'On' : 'Off') : ''"
      :status-color="form.isEnabled ? 'positive' : 'grey'"
      show-back
      @back="router.push({ name: 'admin-shipping', query: { tab: 'zones' } })"
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
      Shipping zone not found.
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
          <q-tab name="general" icon="o_map" label="General" />
        </q-tabs>

        <q-separator />

        <q-tab-panels v-model="tab" animated keep-alive>
          <q-tab-panel name="general" class="q-gutter-y-sm">
            <AppTextField v-model="form.name" label="Name" required :v="v$.name" placeholder="e.g. US East Coast" :disable="!canWrite" />
            <AppTextField v-model="form.countryCode" label="Country (ISO-2)" required :v="v$.countryCode" placeholder="US" maxlength="2" :disable="!canWrite" />
            <AppTextField v-model="form.region" label="Region / State" placeholder="Optional" :disable="!canWrite" />
            <div class="row q-col-gutter-sm">
              <div class="col-6"><AppTextField v-model="form.postalCodeStart" label="Postal from" placeholder="Optional" :disable="!canWrite" /></div>
              <div class="col-6"><AppTextField v-model="form.postalCodeEnd" label="Postal to" placeholder="Optional" :disable="!canWrite" /></div>
            </div>
            <q-toggle v-model="form.isEnabled" label="Enabled" color="primary" class="q-mt-sm" :disable="!canWrite" />
          </q-tab-panel>
        </q-tab-panels>

        <template v-if="isCreate">
          <q-separator />
          <q-card-actions class="q-pa-md">
            <div class="text-caption text-grey-7">Create the zone — changes auto-save from then on.</div>
            <q-space />
            <q-btn v-if="canWrite" unelevated color="primary" no-caps icon="o_check" label="Create zone" :loading="creating" @click="create" />
          </q-card-actions>
        </template>
      </q-card>
    </template>

    <AppRecordMeta entity-type="shipping-zone" :record-id="entity?.id" />
  </q-page>
</template>

<script setup>
/* Shipping zone create + manage page (full-page auto-save via useDetailForm). */
import { ref, computed } from 'vue'
import { useRouter } from 'vue-router'
import { usePermissions } from 'composables/usePermissions'
import { useDetailForm } from 'composables/useDetailForm'
import { required, maxLength } from 'validators'
import { shippingZoneApi } from 'modules/shipping/api'
import AppDetailHeader from 'components/common/AppDetailHeader.vue'
import AppTextField from 'components/common/AppTextField.vue'

const router = useRouter()
const { has } = usePermissions()
const canWrite = computed(() => has('Stores.Write'))

const tab = ref('general')

function buildPayload (f) {
  return {
    name: (f.name || '').trim(),
    countryCode: (f.countryCode || '').toUpperCase(),
    region: f.region || null,
    postalCodeStart: f.postalCodeStart || null,
    postalCodeEnd: f.postalCodeEnd || null,
    isEnabled: f.isEnabled
  }
}

const {
  form, v$, entity, loading, creating, isCreate, saveStatus, create
} = useDetailForm({
  createRouteName: 'shipping-zone-new',
  detailRouteName: 'shipping-zone-detail',
  entityLabel: 'zone',
  api: shippingZoneApi,
  buildPayload,
  empty: { name: '', countryCode: '', region: '', postalCodeStart: '', postalCodeEnd: '', isEnabled: true },
  rules: { name: { required, maxLength: maxLength(200) }, countryCode: { required } }
})
</script>

<style scoped lang="scss">
.app-detail-tabs {
  :deep(.q-tab) { min-height: 44px; }
}
</style>
