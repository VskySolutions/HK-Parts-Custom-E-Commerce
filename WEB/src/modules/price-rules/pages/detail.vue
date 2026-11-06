<template>
  <q-page class="app-page">
    <AppDetailHeader
      :title="isCreate ? 'New price rule' : (loadedName || 'Price rule')"
      :breadcrumbs="[
        { label: 'Home', icon: 'o_home', to: '/dashboard' },
        { label: 'Price Rules', to: { name: 'catalog-price-rules' } },
        { label: isCreate ? 'New price rule' : (loadedName || 'Price rule') }
      ]"
      :status="!isCreate && loaded ? (loaded.effectiveState || '') : ''"
      :status-color="EFFECTIVE_STATE_COLORS[loaded?.effectiveState] || 'grey'"
      show-back
      @back="goBack"
    />

    <q-inner-loading :showing="loading" color="primary" />

    <q-banner v-if="!loading && !isCreate && !loaded" class="bg-grey-2 rounded-borders">
      Price rule not found.
    </q-banner>

    <q-form v-if="isCreate || loaded" class="q-gutter-y-md" @submit.prevent="save">
      <!-- ============ RULE INFORMATION ============ -->
      <q-card flat bordered class="app-section">
        <q-card-section class="text-subtitle1 text-weight-medium">Rule Information</q-card-section>
        <q-separator />
        <q-card-section class="q-gutter-y-xs">
          <AppTextField v-model="form.name" label="Name" required maxlength="100" counter :v="v$.name" placeholder="e.g. Brakes 10% off" :disable="!canWrite" />

          <div class="row q-col-gutter-md">
            <div class="col-12 col-md-4">
              <AppSelect v-model="form.isActive" label="Status" :options="statusOptions" emit-value map-options :disable="!canWrite" />
            </div>
            <div class="col-12 col-md-4">
              <AppSelect v-model="form.stopFurtherRules" label="Stop Other Rules from Processing?" :options="yesNoOptions" emit-value map-options :disable="!canWrite" />
            </div>
            <div class="col-12 col-md-4">
              <AppTextField v-model.number="form.priority" label="Priority" type="number" min="0" step="1" hint="Lower runs first" :v="v$.priority" :disable="!canWrite" />
            </div>
          </div>

          <AppTextField v-model="form.description" label="Description" type="textarea" autogrow :disable="!canWrite" />

          <div class="row q-col-gutter-md">
            <div class="col-12 col-md-6">
              <AppDateField v-model="form.startDate" label="Start Date" clearable :v="v$.startDate" :disable="!canWrite" />
            </div>
            <div class="col-12 col-md-6">
              <AppDateField v-model="form.endDate" label="End Date" clearable :v="v$.endDate" :disable="!canWrite" />
            </div>
          </div>

          <div class="row q-col-gutter-md">
            <div class="col-12 col-md-6">
              <AppSelect v-model="form.discountType" label="Discount Type" :options="discountTypeOptions" emit-value map-options :disable="!canWrite" />
            </div>
            <div class="col-12 col-md-6">
              <AppTextField
                v-model.number="form.discountAmount"
                label="Discount Amount"
                required
                type="number"
                min="0"
                step="0.01"
                :v="v$.discountAmount"
                :disable="!canWrite"
              >
                <template v-if="form.discountType === 'FixedAmount'" #prepend><span class="text-grey-7">$</span></template>
                <template v-else #append><span class="text-grey-7">%</span></template>
              </AppTextField>
            </div>
          </div>
        </q-card-section>
      </q-card>

      <!-- ============ RULE CONDITIONS ============ -->
      <q-card flat bordered class="app-section">
        <q-card-section class="text-subtitle1 text-weight-medium">Rule Conditions</q-card-section>
        <q-separator />
        <q-card-section class="q-gutter-y-md">
          <AppSelect
            v-model="form.customerGroupIds"
            label="Customer Groups"
            required
            multiple
            use-chips
            emit-value
            map-options
            option-value="id"
            option-label="name"
            :options="groupOptions"
            :v="v$.customerGroupIds"
            :disable="!canWrite"
          />

          <div>
            <AppFieldLabel label="Categories" required />
            <div class="text-caption text-grey-7 q-mb-xs">
              Only products assigned <b>directly</b> to a ticked category are included (sub-categories are not implied), plus their variants. Gift cards are never discounted.
            </div>
            <q-input v-model="treeFilter" dense outlined placeholder="Filter categories" class="q-mb-sm" clearable>
              <template #prepend><q-icon name="o_search" /></template>
            </q-input>
            <div class="tree-box rounded-borders" :class="{ 'tree-box--error': v$.categoryIds.$error }">
              <q-tree
                v-model:ticked="form.categoryIds"
                v-model:expanded="expanded"
                :nodes="categoryTree"
                node-key="id"
                label-key="name"
                children-key="children"
                tick-strategy="strict"
                :filter="treeFilter || ''"
                :disable="!canWrite"
                dense
                no-nodes-label="No categories"
                no-results-label="No matching categories"
              />
            </div>
            <div v-if="v$.categoryIds.$error" class="text-negative text-caption q-mt-xs">
              {{ v$.categoryIds.$errors[0].$message }}
            </div>
            <div v-else class="text-caption text-grey-7 q-mt-xs">{{ form.categoryIds.length }} selected</div>
          </div>
        </q-card-section>
      </q-card>

      <!-- ============ PREVIEW ============ -->
      <q-card v-if="preview" flat bordered class="app-section">
        <q-card-section class="row items-center">
          <div class="text-subtitle1 text-weight-medium">Preview</div>
          <q-space />
          <div class="text-body2">
            <b>{{ preview.productCount }}</b> product(s) affected · {{ preview.priceRowCount }} price row(s)
          </div>
        </q-card-section>
        <q-separator />
        <q-table
          flat
          dense
          :rows="preview.sample"
          :columns="previewColumns"
          row-key="key"
          :rows-per-page-options="[0]"
          hide-pagination
          no-data-label="No products match these conditions"
        />
        <q-card-section v-if="preview.priceRowCount > preview.sample.length" class="text-caption text-grey-7">
          Showing the first {{ preview.sample.length }} rows.
        </q-card-section>
      </q-card>

      <div class="row items-center q-gutter-sm">
        <q-btn flat no-caps label="Cancel" @click="goBack" />
        <q-space />
        <q-btn outline no-caps color="primary" icon="o_preview" label="Preview" :loading="previewing" @click="runPreview" />
        <q-btn v-if="canWrite" unelevated no-caps color="primary" icon="o_check" :label="isCreate ? 'Create rule' : 'Save'" type="submit" :loading="saving" />
      </div>
    </q-form>
  </q-page>
</template>

<script setup>
/*
 * Price rule create / edit (explicit save, then back to the list). Client rules mirror the API's
 * FluentValidation; server 400s are mapped onto the matching fields via Vuelidate $externalResults, and a
 * 409 (stale rowVersion) offers to reload. Saving queues a background rebuild of the rule's prices.
 */
import { ref, reactive, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import useVuelidate from '@vuelidate/core'
import { required, maxLength, minValue, integer, helpers } from 'validators'
import { getApiErrorMessage } from 'services/api'
import { priceRuleApi, RETAIL_GROUP_ID, EFFECTIVE_STATE_COLORS } from 'modules/price-rules/api'
import { usePermissions } from 'composables/usePermissions'
import { useNotify } from 'composables/useNotify'
import { confirmation } from 'dialogs/confirmation'
import AppDetailHeader from 'components/common/AppDetailHeader.vue'
import AppFieldLabel from 'components/common/AppFieldLabel.vue'
import AppDateField from 'components/common/AppDateField.vue'

const route = useRoute()
const router = useRouter()
const notify = useNotify()
const { has } = usePermissions()
const canWrite = computed(() => has('Catalog.Write'))

const isCreate = computed(() => !route.params.id)

const statusOptions = [{ label: 'Active', value: true }, { label: 'Inactive', value: false }]
const yesNoOptions = [{ label: 'No', value: false }, { label: 'Yes', value: true }]
const discountTypeOptions = [{ label: 'Percent', value: 'Percent' }, { label: 'Fixed Amount', value: 'FixedAmount' }]

const empty = () => ({
  name: '',
  description: '',
  isActive: true,
  stopFurtherRules: false,
  priority: 0,
  startDate: '',
  endDate: '',
  discountType: 'Percent',
  discountAmount: null,
  customerGroupIds: [RETAIL_GROUP_ID],
  categoryIds: [],
  rowVersion: null,
  storeId: null
})

const form = reactive(empty())
const loaded = ref(null)
const loadedName = ref('')
const loading = ref(false)
const saving = ref(false)
const previewing = ref(false)
const preview = ref(null)

const groupOptions = ref([])
const categoryTree = ref([])
const expanded = ref([])
const treeFilter = ref('')

// ---- validation -------------------------------------------------------------------------------------
const $externalResults = reactive({})

const notBeforeStart = helpers.withMessage(
  'End date must be on or after the start date',
  (value) => !value || !form.startDate || value >= form.startDate
)
const percentMax = helpers.withMessage(
  'A percent discount cannot exceed 100',
  (value) => form.discountType !== 'Percent' || value === null || value === '' || Number(value) <= 100
)
const positive = helpers.withMessage('Must be greater than 0', (value) => value === null || value === '' || Number(value) > 0)
const atLeastOne = (what) => helpers.withMessage(`Select at least one ${what}`, (value) => Array.isArray(value) && value.length > 0)

const rules = {
  name: { required: helpers.withMessage('Name is required', required), maxLength: maxLength(100) },
  priority: { integer, minValue: minValue(0) },
  startDate: {},
  endDate: { notBeforeStart },
  discountAmount: { required: helpers.withMessage('Discount amount is required', required), positive, percentMax },
  customerGroupIds: { atLeastOne: atLeastOne('customer group') },
  categoryIds: { atLeastOne: atLeastOne('category') }
}

const v$ = useVuelidate(rules, form, { $externalResults })

// Server property names (FluentValidation, PascalCase) → form keys.
function applyServerErrors (err) {
  const errors = err?.response?.data?.errors
  if (!errors || typeof errors !== 'object') return false
  let mapped = false
  for (const [key, messages] of Object.entries(errors)) {
    const field = key.charAt(0).toLowerCase() + key.slice(1)
    if (field in form) {
      $externalResults[field] = Array.isArray(messages) ? messages : [String(messages)]
      mapped = true
    }
  }
  return mapped
}

function clearServerErrors () {
  v$.value.$clearExternalResults()
}

// ---- payload ----------------------------------------------------------------------------------------
function payload () {
  return {
    storeId: form.storeId || null,
    name: (form.name || '').trim(),
    description: form.description || null,
    isActive: form.isActive,
    priority: Number(form.priority) || 0,
    stopFurtherRules: form.stopFurtherRules,
    startDate: form.startDate || null,
    endDate: form.endDate || null,
    discountType: form.discountType,
    discountAmount: Number(form.discountAmount),
    customerGroupIds: [...form.customerGroupIds],
    categoryIds: [...form.categoryIds],
    rowVersion: form.rowVersion
  }
}

// ---- load -------------------------------------------------------------------------------------------
function collectIds (nodes, out = []) {
  for (const n of nodes || []) {
    out.push(n.id)
    collectIds(n.children, out)
  }
  return out
}

function fill (dto) {
  loaded.value = dto
  loadedName.value = dto.name
  Object.assign(form, {
    name: dto.name,
    description: dto.description || '',
    isActive: dto.isActive,
    stopFurtherRules: dto.stopFurtherRules,
    priority: dto.priority,
    startDate: dto.startDate || '',
    endDate: dto.endDate || '',
    discountType: dto.discountType,
    discountAmount: dto.discountAmount,
    customerGroupIds: dto.customerGroupIds || [],
    categoryIds: dto.categoryIds || [],
    rowVersion: dto.rowVersion,
    storeId: dto.storeId || null
  })
}

async function load () {
  loading.value = true
  try {
    const [groups, tree, dto] = await Promise.all([
      priceRuleApi.customerGroups(),
      priceRuleApi.categoryTree(),
      isCreate.value ? Promise.resolve(null) : priceRuleApi.get(route.params.id)
    ])
    groupOptions.value = groups || []
    categoryTree.value = tree || []
    // Expand everything so ticked children are visible; the filter box narrows large trees.
    expanded.value = collectIds(categoryTree.value)
    if (dto) fill(dto)
    v$.value.$reset()
  } catch (err) {
    loaded.value = null
    notify.error(getApiErrorMessage(err))
  } finally {
    loading.value = false
  }
}

// ---- actions ----------------------------------------------------------------------------------------
async function save () {
  clearServerErrors()
  if (!(await v$.value.$validate())) return

  saving.value = true
  try {
    if (isCreate.value) {
      await priceRuleApi.create(payload())
      notify.success('Price Rule created successfully')
    } else {
      await priceRuleApi.update(route.params.id, payload())
      notify.success('Price Rule updated successfully')
    }
    goBack()
  } catch (err) {
    const status = err?.response?.status
    if (status === 409) {
      const reload = await confirmation({
        title: 'Changed by someone else',
        message: 'This rule was changed by someone else. Reload?',
        okLabel: 'Reload'
      })
      if (reload) await load()
    } else if (status === 400 && applyServerErrors(err)) {
      notify.error('Please correct the highlighted fields.')
    } else {
      notify.error(getApiErrorMessage(err))
    }
  } finally {
    saving.value = false
  }
}

async function runPreview () {
  clearServerErrors()
  // Preview needs the pricing inputs, not a name.
  const fields = [v$.value.discountAmount, v$.value.customerGroupIds, v$.value.categoryIds]
  fields.forEach((f) => f.$touch())
  if (fields.some((f) => f.$invalid)) return

  previewing.value = true
  try {
    const result = await priceRuleApi.preview(payload())
    const groupName = (id) => groupOptions.value.find((g) => g.id === id)?.name
    preview.value = {
      ...result,
      sample: (result.sample || []).map((r) => ({
        ...r,
        key: `${r.productId}:${r.productVariantId || ''}:${r.customerGroupId}`,
        groupName: r.groupName || groupName(r.customerGroupId)
      }))
    }
  } catch (err) {
    if (!(err?.response?.status === 400 && applyServerErrors(err))) notify.error(getApiErrorMessage(err))
  } finally {
    previewing.value = false
  }
}

const money = (v) => Number(v || 0).toLocaleString(undefined, { style: 'currency', currency: 'USD' })
const previewColumns = [
  { name: 'sku', label: 'SKU', field: 'sku', align: 'left', format: (v) => v || '—' },
  { name: 'name', label: 'Name', field: 'name', align: 'left' },
  { name: 'groupName', label: 'Group', field: 'groupName', align: 'left' },
  { name: 'price', label: 'Base price → Rule price', field: 'rulePrice', align: 'right', format: (_, r) => `${money(r.basePrice)} → ${money(r.rulePrice)}` }
]

function goBack () { router.push({ name: 'catalog-price-rules' }) }

onMounted(load)
</script>

<style scoped lang="scss">
.tree-box {
  border: 1px solid rgba(0, 0, 0, 0.12);
  max-height: 360px;
  overflow: auto;
  padding: 4px 8px;
}
.tree-box--error {
  border-color: var(--q-negative);
}
</style>
