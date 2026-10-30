<template>
  <q-page class="app-page">
    <AppDetailHeader
      :title="isCreate ? 'New customer group' : (entity?.name || 'Customer group')"
      :breadcrumbs="[
        { label: 'Home', icon: 'o_home', to: '/dashboard' },
        { label: 'Customer groups', to: { name: 'admin-customer-groups' } },
        { label: isCreate ? 'New customer group' : (entity?.name || 'Customer group') }
      ]"
      :status="!isCreate && entity ? (form.isActive ? 'Active' : 'Inactive') : ''"
      :status-color="form.isActive ? 'positive' : 'grey'"
      show-back
      @back="router.push({ name: 'admin-customer-groups' })"
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
      Customer group not found.
    </q-banner>

    <template v-if="isCreate || entity">
      <div v-if="!isCreate" class="row items-center text-caption text-grey-7 q-mb-sm q-px-xs">
        <q-icon name="o_cloud_sync" size="16px" class="q-mr-xs" />
        Changes are saved automatically as you edit — no need to press save.
      </div>

      <!-- ============ CORE FIELDS (auto-saved) ============ -->
      <q-card flat bordered class="app-section q-pa-md q-gutter-y-sm">
        <AppTextField
          v-model="form.name"
          label="Name"
          required
          :v="v$.name"
          placeholder="e.g. Wholesale buyers"
          :disable="!canWrite"
        />

        <AppTextField
          v-model="form.description"
          label="Description"
          type="textarea"
          autogrow
          :v="v$.description"
          placeholder="Optional notes describing this group"
          :disable="!canWrite"
        />

        <div class="row q-col-gutter-sm items-start">
          <div class="col-12 col-md-6">
            <AppSelect
              :model-value="form.pricingRuleType"
              label="Pricing rule"
              :options="pricingRuleOptions"
              :disable="!canWrite"
              hint="How prices adjust for members of this group"
              @update:model-value="onRuleChange"
            />
          </div>
          <div v-if="form.pricingRuleType === 'PercentageDiscount'" class="col-12 col-md-6">
            <AppTextField
              v-model="form.discountPercent"
              label="Discount %"
              type="number"
              step="0.01"
              min="0"
              max="100"
              :v="v$.discountPercent"
              placeholder="e.g. 10"
              hint="Percentage off the base price (0–100)"
              :disable="!canWrite"
            />
          </div>
        </div>

        <q-separator class="q-my-sm" />
        <div class="row q-col-gutter-sm items-center">
          <div class="col-6 col-md-3">
            <AppTextField
              v-model="form.displayOrder"
              label="Display order"
              type="number"
              hint="Lower shows first"
              :disable="!canWrite"
            />
          </div>
          <div class="col-auto q-mt-md">
            <q-toggle v-model="form.isActive" label="Active" color="primary" :disable="!canWrite" />
          </div>
        </div>

        <template v-if="isCreate">
          <q-separator />
          <div class="row items-center q-pt-sm">
            <div class="text-caption text-grey-7">
              Create the group to unlock fixed group prices — all auto-saved from then on.
            </div>
            <q-space />
            <q-btn v-if="canWrite" unelevated color="primary" no-caps icon="o_check" label="Create group" :loading="creating" @click="create" />
          </div>
        </template>
      </q-card>

      <!-- ============ FIXED GROUP PRICES (separate sub-resource, saved explicitly) ============ -->
      <q-card
        v-if="!isCreate && form.pricingRuleType === 'FixedGroupPrice'"
        flat
        bordered
        class="app-section q-pa-md q-mt-md"
      >
        <div class="row items-center q-mb-sm">
          <AppFieldLabel label="Fixed group prices" class="col">
            <template #hint>Members of this group pay these prices instead of the base price</template>
          </AppFieldLabel>
          <q-btn v-if="canWrite" flat dense no-caps color="primary" icon="o_add" label="Add row" @click="addPriceRow" />
          <q-btn
            v-if="canWrite"
            unelevated
            dense
            no-caps
            color="primary"
            icon="o_save"
            label="Save prices"
            class="q-ml-sm"
            :loading="savingPrices"
            @click="savePrices"
          />
        </div>

        <div v-if="!priceRows.length" class="text-grey-6 text-caption q-mb-sm">
          No fixed prices yet — add a row to set a group-specific price for a product.
        </div>

        <div
          v-for="(row, i) in priceRows"
          :key="row.key"
          class="row q-col-gutter-sm items-start q-mb-xs no-wrap"
        >
          <div class="col">
            <q-select
              :model-value="row.product"
              dense
              outlined
              use-input
              clearable
              input-debounce="300"
              label="Product"
              :options="productOptions"
              option-label="label"
              option-value="value"
              :disable="!canWrite"
              @filter="onProductFilter"
              @update:model-value="(val) => onPickProduct(row, val)"
            >
              <template #no-option>
                <q-item><q-item-section class="text-grey-6">Type to search products</q-item-section></q-item>
              </template>
            </q-select>
          </div>
          <div class="col-3">
            <q-input
              v-model="row.productVariantId"
              dense
              outlined
              label="Variant ID"
              placeholder="Optional"
              hint="Blank = product-level price"
              :disable="!canWrite"
            />
          </div>
          <div class="col-2">
            <q-input v-model.number="row.price" dense outlined type="number" step="0.01" min="0" label="Price" :disable="!canWrite" />
          </div>
          <div class="col-auto q-pt-sm">
            <q-btn v-if="canWrite" flat round dense icon="o_delete" color="negative" @click="removePriceRow(i)">
              <q-tooltip>Remove row</q-tooltip>
            </q-btn>
          </div>
        </div>
      </q-card>
    </template>

    <AppRecordMeta entity-type="customer-group" :record-id="entity?.id" />
  </q-page>
</template>

<script setup>
/*
 * Customer Group create + manage page (WO-22; full-page auto-save via useDetailForm). The core scalar
 * fields (name / description / pricing rule / discount / active / display order) auto-save on change in
 * manage mode with the live status chip in the header. Fixed group prices are a separate sub-resource
 * saved explicitly (the "Save prices" button) through the group-prices endpoint — mirroring the product
 * tier-price editor — and are only shown once the group exists and the pricing rule is FixedGroupPrice.
 */
import { ref, computed } from 'vue'
import { useRouter } from 'vue-router'
import { customerGroupApi, pricingRuleOptions } from '../api'
import { productApi } from 'modules/catalog/api'
import { getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'
import { usePermissions } from 'composables/usePermissions'
import { useDetailForm } from 'composables/useDetailForm'
import { deleteConfirmation } from 'dialogs/delete_confirmation'
import { required, maxLength, minValue, maxValue } from 'validators'
import AppDetailHeader from 'components/common/AppDetailHeader.vue'
import AppTextField from 'components/common/AppTextField.vue'
import AppSelect from 'components/common/AppSelect.vue'
import AppFieldLabel from 'components/common/AppFieldLabel.vue'

const router = useRouter()
const notify = useNotify()
const { has } = usePermissions()
const canWrite = computed(() => has('Users.Write'))

function toNumberOrNull (value) {
  if (value === '' || value === null || value === undefined) return null
  const n = Number(value)
  return Number.isFinite(n) ? n : null
}

function buildPayload (form) {
  return {
    name: form.name,
    description: form.description || null,
    pricingRuleType: form.pricingRuleType,
    // A percentage only applies to the PercentageDiscount rule; null it out otherwise.
    discountPercent: form.pricingRuleType === 'PercentageDiscount' ? toNumberOrNull(form.discountPercent) : null,
    isActive: form.isActive,
    displayOrder: Number(form.displayOrder) || 0
  }
}

// ---- Fixed group-price editor state (a sub-resource; NOT part of the auto-saved core form) ----
const priceRows = ref([])       // [{ key, product: { label, value } | null, productId, productVariantId, price }]
const productOptions = ref([])  // shared autocomplete options for the product q-select
const savingPrices = ref(false)
let keySeq = 0
function nextKey () { return ++keySeq }

// Seed the editable rows from the group's saved prices; best-effort resolve product names for the labels.
function seedPrices (e) {
  priceRows.value = (e.groupPrices || []).map((gp) => ({
    key: nextKey(),
    product: { label: gp.productId, value: gp.productId },
    productId: gp.productId,
    productVariantId: gp.productVariantId || null,
    price: gp.price
  }))
  for (const row of priceRows.value) resolveProductLabel(row)
}

async function resolveProductLabel (row) {
  if (!row.productId) return
  try {
    const p = await productApi.get(row.productId)
    if (p && p.name) row.product = { label: p.name, value: row.productId }
  } catch { /* keep the id as the label */ }
}

const {
  form, v$, entity, loading, creating, isCreate, id, saveStatus, create, markSaved
} = useDetailForm({
  createRouteName: 'admin-customer-group-new',
  detailRouteName: 'admin-customer-group-detail',
  entityLabel: 'customer group',
  api: customerGroupApi,
  buildPayload,
  empty: {
    name: '', description: '', pricingRuleType: 'None', discountPercent: null, isActive: true, displayOrder: 0
  },
  rules: {
    name: { required, maxLength: maxLength(200) },
    description: { maxLength: maxLength(512) },
    discountPercent: { minValue: minValue(0), maxValue: maxValue(100) }
  },
  afterLoad: (e) => seedPrices(e),
  resetExtra: () => { priceRows.value = [] }
})

// Clear the percentage when the rule no longer uses it, so a hidden (possibly out-of-range) value
// can't silently block the core auto-save.
function onRuleChange (val) {
  form.pricingRuleType = val
  if (val !== 'PercentageDiscount') form.discountPercent = null
}

// ---- Product autocomplete (q-select @filter) ----
async function onProductFilter (val, update) {
  try {
    const result = await productApi.list({ search: val || undefined, page: 1, pageSize: 20 })
    const items = Array.isArray(result) ? result : result?.items || []
    update(() => { productOptions.value = items.map((p) => ({ label: p.name, value: p.id })) })
  } catch {
    update(() => { productOptions.value = [] })
  }
}

function onPickProduct (row, val) {
  row.product = val || null
  row.productId = val ? val.value : null
}

function addPriceRow () {
  priceRows.value.push({ key: nextKey(), product: null, productId: null, productVariantId: null, price: null })
}

async function removePriceRow (i) {
  if (!(await deleteConfirmation('this price row', {
    title: 'Remove row',
    okLabel: 'Remove',
    message: 'Remove this group-price row? The change is applied when you save prices.'
  }))) return
  priceRows.value.splice(i, 1)
}

// Replace-semantics: send the whole set of valid rows to the group-prices endpoint.
async function savePrices () {
  const prices = priceRows.value
    .filter((r) => r.productId && r.price !== null && r.price !== '' && r.price !== undefined)
    .map((r) => ({
      productId: r.productId,
      productVariantId: (r.productVariantId && String(r.productVariantId).trim()) || null,
      price: Number(r.price)
    }))
  savingPrices.value = true
  try {
    entity.value = await customerGroupApi.setGroupPrices(id.value, prices)
    seedPrices(entity.value)
    markSaved()
    notify.success('Group prices saved')
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    savingPrices.value = false
  }
}
</script>
