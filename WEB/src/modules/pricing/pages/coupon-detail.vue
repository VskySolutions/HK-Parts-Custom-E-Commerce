<template>
  <q-page class="app-page">
    <AppDetailHeader
      :title="isCreate ? 'New coupon' : (entity?.code || 'Coupon')"
      :breadcrumbs="[
        { label: 'Home', icon: 'o_home', to: '/dashboard' },
        { label: 'Promotions', to: { name: 'admin-promotions' } },
        { label: isCreate ? 'New coupon' : (entity?.code || 'Coupon') }
      ]"
      :status="!isCreate && entity ? (form.isActive ? 'Active' : 'Off') : ''"
      :status-color="form.isActive ? 'positive' : 'grey'"
      show-back
      @back="router.push({ name: 'admin-promotions' })"
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
      Coupon not found.
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
          <q-tab name="general" icon="o_confirmation_number" label="General" />
        </q-tabs>

        <q-separator />

        <q-tab-panels v-model="tab" animated keep-alive>
          <q-tab-panel name="general" class="q-gutter-y-sm">
            <AppTextField v-model="form.code" label="Code" required :v="v$.code" placeholder="e.g. SUMMER10" :disable="!canWrite" />
            <AppSelect v-model="form.discountId" label="Linked discount" required :v="v$.discountId" :options="discountOptions" placeholder="Select the discount this code applies" :disable="!canWrite" />

            <div class="row q-col-gutter-sm">
              <div class="col-6"><AppSelect v-model="form.usageType" label="Usage" :options="couponUsageOptions" :disable="!canWrite" /></div>
              <div class="col-6"><AppTextField v-if="form.usageType === 'Limited'" v-model="form.maxRedemptions" label="Max redemptions" type="number" placeholder="e.g. 100" :disable="!canWrite" /></div>
            </div>

            <div class="row q-col-gutter-sm">
              <div class="col-6"><AppFieldLabel label="Starts" /><q-input v-model="form.startDate" dense outlined type="date" :disable="!canWrite" clearable /></div>
              <div class="col-6"><AppFieldLabel label="Ends" /><q-input v-model="form.endDate" dense outlined type="date" :disable="!canWrite" clearable /></div>
            </div>

            <q-toggle v-model="form.isActive" label="Active" color="primary" class="q-mt-sm" :disable="!canWrite" />
          </q-tab-panel>
        </q-tab-panels>

        <template v-if="isCreate">
          <q-separator />
          <q-card-actions class="q-pa-md">
            <div class="text-caption text-grey-7">Create the coupon — changes auto-save from then on.</div>
            <q-space />
            <q-btn v-if="canWrite" unelevated color="primary" no-caps icon="o_check" label="Create coupon" :loading="creating" @click="create" />
          </q-card-actions>
        </template>
      </q-card>
    </template>

    <AppRecordMeta entity-type="coupon" :record-id="entity?.id" />
  </q-page>
</template>

<script setup>
/*
 * Coupon create + manage page (full-page auto-save via useDetailForm). Pulled out of the Promotions
 * tabbed page. A coupon links a code to a discount, with a usage policy and active window.
 */
import { ref, computed } from 'vue'
import { useRouter } from 'vue-router'
import { usePermissions } from 'composables/usePermissions'
import { useDetailForm } from 'composables/useDetailForm'
import { required, maxLength } from 'validators'
import { couponApi, couponUsageOptions, discountApi } from 'modules/pricing/api'
import AppDetailHeader from 'components/common/AppDetailHeader.vue'
import AppTextField from 'components/common/AppTextField.vue'
import AppSelect from 'components/common/AppSelect.vue'
import AppFieldLabel from 'components/common/AppFieldLabel.vue'

const router = useRouter()
const { has } = usePermissions()
const canWrite = computed(() => has('Catalog.Write'))

const tab = ref('general')

const discountOptions = ref([])
async function loadDiscounts () {
  try {
    const res = await discountApi.list({ page: 1, pageSize: 200 })
    const items = Array.isArray(res) ? res : res?.items || []
    discountOptions.value = items.map((d) => ({ label: `${d.name} (${d.type === 'Percentage' ? d.value + '%' : d.value})`, value: d.id }))
  } catch (e) { discountOptions.value = [] }
}
loadDiscounts()

function toDateInput (v) { return v ? new Date(v).toISOString().slice(0, 10) : '' }

function buildPayload (f) {
  return {
    code: (f.code || '').trim(),
    discountId: f.discountId,
    usageType: f.usageType,
    maxRedemptions: f.usageType === 'Limited' ? (Number(f.maxRedemptions) || null) : null,
    startDateUtc: f.startDate || null,
    endDateUtc: f.endDate || null,
    isActive: f.isActive
  }
}

const {
  form, v$, entity, loading, creating, isCreate, saveStatus, create
} = useDetailForm({
  createRouteName: 'coupon-new',
  detailRouteName: 'coupon-detail',
  entityLabel: 'coupon',
  api: couponApi,
  buildPayload,
  empty: { code: '', discountId: null, usageType: 'SingleUse', maxRedemptions: null, startDate: '', endDate: '', isActive: true },
  rules: { code: { required, maxLength: maxLength(64) }, discountId: { required } },
  hydrateForm: (f, e) => {
    f.startDate = toDateInput(e.startDateUtc)
    f.endDate = toDateInput(e.endDateUtc)
  }
})
</script>

<style scoped lang="scss">
.app-detail-tabs {
  :deep(.q-tab) { min-height: 44px; }
}
</style>
