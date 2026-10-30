<template>
  <q-page class="app-page">
    <AppDetailHeader
      :title="isCreate ? 'New discount' : (entity?.name || 'Discount')"
      :breadcrumbs="[
        { label: 'Home', icon: 'o_home', to: '/dashboard' },
        { label: 'Promotions', to: { name: 'admin-promotions' } },
        { label: isCreate ? 'New discount' : (entity?.name || 'Discount') }
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
      Discount not found.
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
          <q-tab name="general" icon="o_local_offer" label="General" />
        </q-tabs>

        <q-separator />

        <q-tab-panels v-model="tab" animated keep-alive>
          <q-tab-panel name="general" class="q-gutter-y-sm">
            <AppTextField v-model="form.name" label="Name" required :v="v$.name" placeholder="e.g. Summer Sale 10%" :disable="!canWrite" />

            <div class="row q-col-gutter-sm">
              <div class="col-6"><AppSelect v-model="form.scope" label="Scope" :options="discountScopeOptions" :disable="!canWrite" /></div>
              <div class="col-6"><AppSelect v-model="form.type" label="Type" :options="discountTypeOptions" :disable="!canWrite" /></div>
            </div>

            <AppTextField v-model="form.value" :label="form.type === 'Percentage' ? 'Value (%)' : 'Value (amount)'" type="number" step="0.01" required :v="v$.value" placeholder="0" :disable="!canWrite" />

            <AppSelect v-if="form.scope === 'Product'" v-model="form.productId" label="Product" clearable :options="productOptions" placeholder="Select a product" :disable="!canWrite" />
            <AppSelect v-if="form.scope === 'Category'" v-model="form.categoryId" label="Category" clearable :options="categoryOptions" placeholder="Select a category" :disable="!canWrite" />

            <div class="row q-col-gutter-sm">
              <div class="col-6"><AppFieldLabel label="Starts" /><q-input v-model="form.startDate" dense outlined type="date" :disable="!canWrite" clearable /></div>
              <div class="col-6"><AppFieldLabel label="Ends" /><q-input v-model="form.endDate" dense outlined type="date" :disable="!canWrite" clearable /></div>
            </div>

            <AppTextField v-model="form.minimumOrderValue" label="Minimum order value" type="number" step="0.01" placeholder="Optional threshold" :disable="!canWrite" />

            <div class="column q-gutter-xs q-mt-sm">
              <q-toggle v-model="form.requiresCoupon" label="Requires a coupon code" color="primary" :disable="!canWrite" />
              <div class="text-caption text-grey-6 q-mb-xs">
                When on, this discount applies only when a customer enters a linked coupon code (set up under Coupons). When off, it applies automatically to every eligible cart.
              </div>
              <q-toggle v-model="form.isExclusive" label="Exclusive (cannot combine with other discounts)" color="primary" :disable="!canWrite" />
              <q-toggle v-model="form.isActive" label="Active" color="primary" :disable="!canWrite" />
            </div>
          </q-tab-panel>
        </q-tab-panels>

        <template v-if="isCreate">
          <q-separator />
          <q-card-actions class="q-pa-md">
            <div class="text-caption text-grey-7">Create the discount — changes auto-save from then on.</div>
            <q-space />
            <q-btn v-if="canWrite" unelevated color="primary" no-caps icon="o_check" label="Create discount" :loading="creating" @click="create" />
          </q-card-actions>
        </template>
      </q-card>
    </template>

    <AppRecordMeta entity-type="discount" :record-id="entity?.id" />
  </q-page>
</template>

<script setup>
/*
 * Discount create + manage page (full-page auto-save via useDetailForm). Pulled out of the Promotions
 * tabbed page. Date inputs map to startDateUtc/endDateUtc; scope drives the product/category target.
 */
import { ref, computed } from 'vue'
import { useRouter } from 'vue-router'
import { usePermissions } from 'composables/usePermissions'
import { useDetailForm } from 'composables/useDetailForm'
import { required, maxLength } from 'validators'
import { discountApi, discountScopeOptions, discountTypeOptions } from 'modules/pricing/api'
import { productApi, categoryApi } from 'modules/catalog/api'
import AppDetailHeader from 'components/common/AppDetailHeader.vue'
import AppTextField from 'components/common/AppTextField.vue'
import AppSelect from 'components/common/AppSelect.vue'
import AppFieldLabel from 'components/common/AppFieldLabel.vue'

const router = useRouter()
const { has } = usePermissions()
const canWrite = computed(() => has('Catalog.Write'))

const tab = ref('general')

const productOptions = ref([])
const categoryOptions = ref([])
async function loadOptions () {
  try {
    const [p, c] = await Promise.all([
      productApi.list({ page: 1, pageSize: 200 }).catch(() => []),
      categoryApi.list({ page: 1, pageSize: 200 }).catch(() => [])
    ])
    productOptions.value = (Array.isArray(p) ? p : p?.items || []).map((x) => ({ label: x.name, value: x.id }))
    categoryOptions.value = (Array.isArray(c) ? c : c?.items || []).map((x) => ({ label: x.name, value: x.id }))
  } catch (e) { /* selects stay empty */ }
}
loadOptions()

function toDateInput (v) { return v ? new Date(v).toISOString().slice(0, 10) : '' }
function toNumberOrNull (v) { if (v === '' || v == null) return null; const n = Number(v); return Number.isFinite(n) ? n : null }

function buildPayload (f) {
  return {
    name: (f.name || '').trim(),
    scope: f.scope,
    type: f.type,
    value: toNumberOrNull(f.value) || 0,
    productId: f.scope === 'Product' ? f.productId || null : null,
    categoryId: f.scope === 'Category' ? f.categoryId || null : null,
    startDateUtc: f.startDate || null,
    endDateUtc: f.endDate || null,
    minimumOrderValue: toNumberOrNull(f.minimumOrderValue),
    isExclusive: f.isExclusive,
    isActive: f.isActive,
    requiresCoupon: f.requiresCoupon
  }
}

const {
  form, v$, entity, loading, creating, isCreate, saveStatus, create
} = useDetailForm({
  createRouteName: 'discount-new',
  detailRouteName: 'discount-detail',
  entityLabel: 'discount',
  api: discountApi,
  buildPayload,
  empty: {
    name: '', scope: 'CartTotal', type: 'Percentage', value: null,
    productId: null, categoryId: null, startDate: '', endDate: '', minimumOrderValue: null,
    isExclusive: false, isActive: true, requiresCoupon: false
  },
  rules: { name: { required, maxLength: maxLength(200) }, value: { required } },
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
