<template>
  <q-page class="app-page">
    <AppDetailHeader
      :title="listing ? listing.title : 'eBay listing'"
      :breadcrumbs="[
        { label: 'Home', icon: 'o_home', to: '/dashboard' },
        { label: 'eBay Listings', to: { name: 'ebay-listings' } },
        { label: listing ? listing.sku : 'Listing' }
      ]"
      :status="listing ? statusBadge.label : ''"
      :status-color="statusBadge.color"
      show-back
      @back="goBack"
    >
      <template #actions>
        <a v-if="listing?.ebayItemUrl" :href="listing.ebayItemUrl" target="_blank" rel="noopener" class="q-mr-sm text-primary">
          eBay item {{ listing.ebayItemId }} <q-icon name="o_open_in_new" size="14px" />
        </a>
        <q-btn v-if="canWrite && listing && listing.status !== 'Ended'" outline no-caps color="negative" icon="o_cancel" label="End listing" @click="endListing" />
      </template>
    </AppDetailHeader>

    <q-inner-loading :showing="loading" />

    <q-banner v-if="listing?.lastError" class="bg-red-1 text-negative rounded-borders q-mb-md">
      <template #avatar><q-icon name="o_error" color="negative" /></template>
      <div class="text-weight-medium">Last eBay error</div>
      <div style="white-space: pre-line">{{ listing.lastError }}</div>
    </q-banner>
    <q-banner v-if="listing?.status === 'Ended'" class="bg-grey-2 rounded-borders q-mb-md">
      This listing ended {{ listing.endedAtUtc ? formatDate(listing.endedAtUtc) : '' }}. List the product again to create a new listing.
    </q-banner>

    <q-form v-if="listing" @submit.prevent="save">
      <q-card flat bordered class="app-section">
        <q-tabs v-model="tab" align="left" active-color="primary" indicator-color="primary" class="text-grey-7" no-caps inline-label>
          <q-tab name="general" icon="o_info" label="General" />
          <q-tab name="aspects" icon="o_list_alt" :label="`Item Specifics${missingRequired.length ? ` (${missingRequired.length} required)` : ''}`" />
          <q-tab name="policies" icon="o_policy" label="Seller Policies" />
          <q-tab v-if="showFitment" name="fitment" icon="o_directions_car" :label="`Vehicle Fitment (${listing.fitment.length})`" />
        </q-tabs>
        <q-separator />

        <q-tab-panels v-model="tab" animated keep-alive>
          <!-- ============ GENERAL ============ -->
          <q-tab-panel name="general" class="q-gutter-y-xs">
            <div class="text-caption text-grey-7">
              Product: <b>{{ listing.productName }}</b> · Category: <b>{{ listing.categoryName || listing.categoryId }}</b>
              · Store inventory: <b>{{ listing.storeInventory }}</b> · Sold on eBay: <b>{{ listing.quantitySold }}</b>
            </div>
            <div class="row q-col-gutter-md">
              <div class="col-12 col-md-4">
                <AppSelect v-model="form.status" label="Status" required :options="statusOptions" emit-value map-options :disable="readOnly"
                  :hint="listing.isPublished ? 'Published: use End listing to stop selling' : 'Active publishes to eBay on save'" />
              </div>
              <div class="col-12 col-md-4">
                <AppTextField v-model="form.sku" label="SKU" required maxlength="50" :v="v$.sku" :readonly="listing.isPublished" :disable="readOnly"
                  :hint="listing.isPublished ? 'Read-only once published' : ''" />
              </div>
              <div class="col-12 col-md-4">
                <AppSelect v-model="form.conditionId" label="Condition" required :options="conditions" option-value="conditionId" option-label="description"
                  emit-value map-options :v="v$.conditionId" :loading="metaLoading" :disable="readOnly" />
              </div>
            </div>
            <AppTextField v-model="form.title" label="Title" required maxlength="80" counter :v="v$.title" :disable="readOnly" />
            <AppTextField v-model="form.subtitle" label="Subtitle" maxlength="55" counter :v="v$.subtitle" :disable="readOnly"
              hint="eBay may charge a fee for subtitles" />
            <div class="row q-col-gutter-md">
              <div class="col-12 col-md-6">
                <AppTextField v-model.number="form.price" label="Price" required type="number" min="0" step="0.01" :v="v$.price" :disable="readOnly">
                  <template #prepend><span class="text-grey-7">$</span></template>
                </AppTextField>
              </div>
              <div class="col-12 col-md-6">
                <AppTextField v-model.number="form.quantity" label="Quantity" required type="number" min="0" step="1" :v="v$.quantity" :disable="readOnly"
                  hint="Kept in step with store inventory by the sync" />
              </div>
            </div>
            <AppRichText v-model="form.description" label="Description" required :v="v$.description" :disable="readOnly" />
            <div v-if="v$.description.$error" class="text-negative text-caption">{{ v$.description.$errors[0].$message }}</div>

            <div class="q-mt-md">
              <AppFieldLabel label="Images" />
              <div v-if="!listing.images.length" class="text-grey-6 text-body2">No product images. eBay requires at least one; add images on the product.</div>
              <div class="row q-gutter-sm">
                <q-img v-for="(img, i) in listing.images" :key="i" :src="$media ? $media(img.url) : img.url" :alt="img.altText || ''"
                  style="width: 96px; height: 96px" fit="contain" class="rounded-borders bg-grey-2" />
              </div>
              <div class="text-caption text-grey-7 q-mt-xs">The product's images, in display order (up to 12). Edit them on the product.</div>
            </div>
          </q-tab-panel>

          <!-- ============ ITEM SPECIFICS ============ -->
          <q-tab-panel name="aspects">
            <q-banner v-if="metaError" class="bg-orange-1 text-orange-10 rounded-borders q-mb-md">{{ metaError }}</q-banner>
            <div class="row q-col-gutter-md">
              <div v-for="a in aspectDefs" :key="a.name" class="col-12 col-md-6">
                <AppFieldLabel :label="a.name" :required="a.required" />
                <q-select
                  v-if="a.values.length"
                  v-model="aspectValues[a.name]"
                  dense outlined
                  :options="filteredValues(a)"
                  :multiple="a.multiple"
                  :use-chips="a.multiple"
                  :use-input="a.freeText"
                  :new-value-mode="a.freeText ? 'add-unique' : undefined"
                  input-debounce="0"
                  clearable
                  hide-bottom-space
                  :error="!!aspectErrors[a.name]"
                  :error-message="aspectErrors[a.name]"
                  :disable="readOnly"
                  @filter="(val, update) => filterAspect(a, val, update)"
                >
                  <template #option="scope">
                    <q-item v-bind="scope.itemProps"><q-item-section>{{ scope.opt }}</q-item-section></q-item>
                  </template>
                </q-select>
                <q-input
                  v-else
                  v-model="aspectValues[a.name]"
                  dense outlined
                  :maxlength="a.maxLength || 65"
                  hide-bottom-space
                  :error="!!aspectErrors[a.name]"
                  :error-message="aspectErrors[a.name]"
                  :disable="readOnly"
                  :hint="a.multiple ? 'Separate several values with commas' : ''"
                />
              </div>
              <!-- Saved values for aspects eBay no longer suggests for this category -->
              <div v-for="name in extraAspectNames" :key="'extra-' + name" class="col-12 col-md-6">
                <AppFieldLabel :label="name" />
                <q-input v-model="aspectValues[name]" dense outlined hint="Not in eBay's list for this category" :disable="readOnly" />
              </div>
            </div>
            <div v-if="!aspectDefs.length && !metaLoading && !metaError" class="text-grey-6">eBay lists no item specifics for this category.</div>
          </q-tab-panel>

          <!-- ============ SELLER POLICIES ============ -->
          <q-tab-panel name="policies">
            <div class="row q-col-gutter-md">
              <div v-for="p in policyFields" :key="p.key" class="col-12 col-md-4">
                <AppSelect v-model="form[p.key]" :label="p.label" required :options="policies[p.type] || []" option-value="id" option-label="name"
                  emit-value map-options :v="v$[p.key]" :loading="metaLoading" :disable="readOnly" />
              </div>
            </div>
          </q-tab-panel>

          <!-- ============ VEHICLE FITMENT ============ -->
          <q-tab-panel v-if="showFitment" name="fitment">
            <q-banner v-if="compat.maxCompatibleVehicles && listing.fitment.length > compat.maxCompatibleVehicles" class="bg-orange-1 text-orange-10 rounded-borders q-mb-md">
              This product has {{ listing.fitment.length }} fitment rows but the category accepts {{ compat.maxCompatibleVehicles }}.
              Only the first {{ compat.maxCompatibleVehicles }} will be sent to eBay.
            </q-banner>
            <q-table flat bordered dense :rows="listing.fitment" :columns="fitmentColumns" row-key="key" :rows-per-page-options="[25, 50, 0]"
              no-data-label="No vehicle fitment recorded for this product" />
          </q-tab-panel>
        </q-tab-panels>

        <q-separator />
        <q-card-actions class="q-pa-md">
          <q-btn flat no-caps label="Cancel" @click="goBack" />
          <q-space />
          <q-btn v-if="!readOnly" unelevated no-caps color="primary" icon="o_check" type="submit" :loading="saving"
            :label="form.status === 'Active' ? (listing.isPublished ? 'Save & revise on eBay' : 'Save & publish to eBay') : 'Save draft'" />
        </q-card-actions>
      </q-card>
    </q-form>
  </q-page>
</template>

<script setup>
/*
 * Edit an eBay listing (one q-form over tabs). Draft saves are local only; Active publishes the first time
 * (inventory item → compatibility → offer → publish) and revises afterwards. eBay business errors come back
 * as 422 and are listed under "ERROR - Your listing has valid eBay verification:" while the page stays put.
 */
import { ref, reactive, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { date } from 'quasar'
import useVuelidate from '@vuelidate/core'
import { required, maxLength, minValue, integer, helpers } from 'validators'
import { getApiErrorMessage } from 'services/api'
import { ebayApi, STATUS_BADGE, notifyEbayErrors } from 'modules/ebay/api'
import { usePermissions } from 'composables/usePermissions'
import { useNotify } from 'composables/useNotify'
import { confirmation } from 'dialogs/confirmation'
import AppDetailHeader from 'components/common/AppDetailHeader.vue'
import AppFieldLabel from 'components/common/AppFieldLabel.vue'
import AppRichText from 'components/common/AppRichText.vue'

const SAVE_ERROR_HEADER = 'ERROR - Your listing has valid eBay verification:'

const route = useRoute()
const router = useRouter()
const notify = useNotify()
const { has } = usePermissions()
const canWrite = computed(() => has('Ebay.Write'))

const listing = ref(null)
const loading = ref(false)
const saving = ref(false)
const tab = ref('general')

const form = reactive({
  status: 'Draft', title: '', subtitle: '', sku: '', price: null, quantity: 0, conditionId: null,
  description: '', paymentPolicyId: null, fulfillmentPolicyId: null, returnPolicyId: null
})
const aspectDefs = ref([])
const aspectFilters = reactive({})
const aspectValues = reactive({})
const aspectErrors = reactive({})
const conditions = ref([])
const policies = reactive({ Payment: [], Fulfillment: [], Return: [] })
const compat = ref({ supported: false, maxCompatibleVehicles: 0 })
const metaLoading = ref(false)
const metaError = ref('')

const readOnly = computed(() => !canWrite.value || listing.value?.status === 'Ended')
const statusBadge = computed(() => STATUS_BADGE[listing.value?.status] || { color: 'grey', label: listing.value?.status || '' })
const statusOptions = computed(() => listing.value?.isPublished
  ? [{ label: 'Active', value: 'Active' }]
  : [{ label: 'Draft (Inactive)', value: 'Draft' }, { label: 'Active', value: 'Active' }])
const showFitment = computed(() => listing.value?.fitmentEnabled && compat.value.supported)
const extraAspectNames = computed(() => Object.keys(aspectValues).filter((n) => !aspectDefs.value.some((a) => a.name === n)))
const missingRequired = computed(() => aspectDefs.value.filter((a) => a.required && valuesOf(a.name).length === 0).map((a) => a.name))

const policyFields = [
  { key: 'paymentPolicyId', type: 'Payment', label: 'Payment Policy' },
  { key: 'fulfillmentPolicyId', type: 'Fulfillment', label: 'Shipping Policy' },
  { key: 'returnPolicyId', type: 'Return', label: 'Return Policy' }
]
const fitmentColumns = [
  { name: 'year', label: 'Year', field: 'year', align: 'left', sortable: true },
  { name: 'make', label: 'Make', field: 'make', align: 'left', sortable: true },
  { name: 'model', label: 'Model', field: 'model', align: 'left', sortable: true },
  { name: 'trim', label: 'Trim', field: 'trim', align: 'left', format: (v) => v || '—' },
  { name: 'engine', label: 'Engine', field: 'engine', align: 'left', format: (v) => v || '—' }
]

const formatDate = (v) => date.formatDate(v, 'YYYY-MM-DD HH:mm')

// ---- validation ---------------------------------------------------------------------------------------
const $externalResults = reactive({})
const positive = helpers.withMessage('Price must be greater than 0', (v) => v === null || v === '' || Number(v) > 0)
const req = (msg) => helpers.withMessage(msg, required)
const rules = {
  title: { required: req('Title is required'), maxLength: maxLength(80) },
  subtitle: { maxLength: maxLength(55) },
  sku: { required: req('SKU is required'), maxLength: maxLength(50) },
  price: { required: req('Price is required'), positive },
  quantity: { required: req('Quantity is required'), integer, minValue: minValue(0) },
  conditionId: { required: req('Condition is required') },
  description: { required: req('Description is required') },
  paymentPolicyId: { required: req('Payment policy is required') },
  fulfillmentPolicyId: { required: req('Shipping policy is required') },
  returnPolicyId: { required: req('Return policy is required') }
}
const v$ = useVuelidate(rules, form, { $externalResults })

function valuesOf (name) {
  const v = aspectValues[name]
  if (Array.isArray(v)) return v.map((x) => String(x).trim()).filter(Boolean)
  if (v === null || v === undefined || String(v).trim() === '') return []
  const def = aspectDefs.value.find((a) => a.name === name)
  // A free-text multi-value input accepts "a, b, c".
  return def?.multiple && !def.values.length ? String(v).split(',').map((x) => x.trim()).filter(Boolean) : [String(v).trim()]
}

function validateAspects (requireAll) {
  Object.keys(aspectErrors).forEach((k) => delete aspectErrors[k])
  if (!requireAll) return true
  missingRequired.value.forEach((name) => { aspectErrors[name] = `${name} is required by eBay` })
  return missingRequired.value.length === 0
}

function filterAspect (aspect, val, update) {
  update(() => { aspectFilters[aspect.name] = (val || '').toLowerCase() })
}

// eBay's suggestions narrowed by what the admin typed (capped so huge lists stay responsive).
function filteredValues (aspect) {
  const needle = aspectFilters[aspect.name]
  const list = needle ? aspect.values.filter((v) => v.toLowerCase().includes(needle)) : aspect.values
  return list.slice(0, 200)
}

// ---- load ---------------------------------------------------------------------------------------------
function fill (dto) {
  listing.value = dto
  Object.assign(form, {
    status: dto.status === 'Error' ? 'Active' : dto.status === 'Ended' ? 'Draft' : dto.status,
    title: dto.title,
    subtitle: dto.subtitle || '',
    sku: dto.sku,
    price: dto.price,
    quantity: dto.quantity,
    conditionId: dto.conditionId,
    description: dto.description || '',
    paymentPolicyId: dto.paymentPolicyId,
    fulfillmentPolicyId: dto.fulfillmentPolicyId,
    returnPolicyId: dto.returnPolicyId
  })
  Object.keys(aspectValues).forEach((k) => delete aspectValues[k])
  for (const a of dto.aspects || []) aspectValues[a.name] = a.values
}

function normalizeAspectValues () {
  // Single-value aspects hold a string; multi-value ones an array (saved values included even when eBay doesn't suggest them).
  for (const def of aspectDefs.value) {
    const current = aspectValues[def.name]
    if (def.multiple && def.values.length) aspectValues[def.name] = Array.isArray(current) ? current : current ? [current] : []
    else if (Array.isArray(current)) aspectValues[def.name] = def.multiple ? current.join(', ') : (current[0] ?? null)
    else if (current === undefined) aspectValues[def.name] = def.multiple && def.values.length ? [] : null
  }
}

async function loadMetadata () {
  metaLoading.value = true
  metaError.value = ''
  const categoryId = listing.value.categoryId
  const results = await Promise.allSettled([
    ebayApi.aspects(categoryId),
    ebayApi.conditions(categoryId),
    ebayApi.policies('Payment'),
    ebayApi.policies('Fulfillment'),
    ebayApi.policies('Return'),
    listing.value.fitmentEnabled ? ebayApi.compatibility(categoryId) : Promise.resolve({ supported: false, maxCompatibleVehicles: 0 })
  ])
  const [aspects, conds, pay, ship, ret, comp] = results
  if (aspects.status === 'fulfilled') {
    aspectDefs.value = [...aspects.value].sort((a, b) => Number(b.required) - Number(a.required) || a.name.localeCompare(b.name))
    normalizeAspectValues()
  }
  if (conds.status === 'fulfilled') conditions.value = conds.value
  if (pay.status === 'fulfilled') policies.Payment = pay.value
  if (ship.status === 'fulfilled') policies.Fulfillment = ship.value
  if (ret.status === 'fulfilled') policies.Return = ret.value
  if (comp.status === 'fulfilled') compat.value = comp.value
  const failed = results.find((r) => r.status === 'rejected')
  if (failed) metaError.value = `Some eBay data could not be loaded: ${getApiErrorMessage(failed.reason)}`
  metaLoading.value = false
}

async function load () {
  loading.value = true
  try {
    fill(await ebayApi.getListing(route.params.id))
    v$.value.$reset()
    loadMetadata()
  } catch (err) {
    listing.value = null
    notify.error(getApiErrorMessage(err))
  } finally {
    loading.value = false
  }
}

// ---- save / end ---------------------------------------------------------------------------------------
function payload () {
  return {
    status: form.status,
    title: form.title.trim(),
    subtitle: form.subtitle || null,
    sku: form.sku.trim(),
    price: Number(form.price),
    quantity: Number(form.quantity),
    conditionId: form.conditionId,
    description: form.description,
    paymentPolicyId: form.paymentPolicyId,
    fulfillmentPolicyId: form.fulfillmentPolicyId,
    returnPolicyId: form.returnPolicyId,
    aspects: Object.keys(aspectValues).map((name) => ({ name, values: valuesOf(name) })).filter((a) => a.values.length),
    rowVersion: listing.value.rowVersion
  }
}

function applyServerErrors (err) {
  const errors = err?.response?.data?.errors
  if (!errors || typeof errors !== 'object' || Array.isArray(errors)) return false
  for (const [key, messages] of Object.entries(errors)) {
    const text = Array.isArray(messages) ? messages[0] : String(messages)
    const aspect = /^Aspects\[(.+)\]$/.exec(key)
    if (aspect) { aspectErrors[aspect[1]] = text; continue }
    const field = key.charAt(0).toLowerCase() + key.slice(1)
    if (field in form) $externalResults[field] = Array.isArray(messages) ? messages : [text]
    else notify.error(text)
  }
  return true
}

async function save () {
  v$.value.$clearExternalResults()
  const fieldsOk = await v$.value.$validate()
  const aspectsOk = validateAspects(form.status === 'Active')
  if (!fieldsOk || !aspectsOk) {
    if (!aspectsOk && fieldsOk) tab.value = 'aspects'
    notify.error('Please correct the highlighted fields.')
    return
  }

  saving.value = true
  try {
    await ebayApi.saveListing(listing.value.id, payload())
    notify.success(form.status === 'Active'
      ? (listing.value.isPublished ? 'Your eBay listing has been revised' : 'Your listing is live on eBay')
      : 'Draft saved')
    goBack()
  } catch (err) {
    const status = err?.response?.status
    if (status === 422) {
      notifyEbayErrors(err, SAVE_ERROR_HEADER)
      // The local data was kept; reload for the new rowVersion, status and last error.
      const fresh = await ebayApi.getListing(listing.value.id).catch(() => null)
      if (fresh) { listing.value = fresh; form.status = 'Active' }
    } else if (status === 409) {
      const reload = await confirmation({ title: 'Changed by someone else', message: 'This listing was changed by someone else. Reload?', okLabel: 'Reload' })
      if (reload) await load()
    } else if (status === 400 && applyServerErrors(err)) {
      notify.error('Please correct the highlighted fields.')
      if (Object.keys(aspectErrors).length) tab.value = 'aspects'
    } else {
      notify.error(getApiErrorMessage(err))
    }
  } finally {
    saving.value = false
  }
}

async function endListing () {
  const ok = await confirmation({
    title: 'End this eBay listing?',
    message: listing.value.isPublished
      ? `It will be withdrawn from eBay (item ${listing.value.ebayItemId}). You can list the product again later.`
      : 'The draft will be closed. You can list the product again later.',
    okLabel: 'End listing',
    color: 'negative'
  })
  if (!ok) return
  try {
    await ebayApi.endListing(listing.value.id)
    notify.success('Your eBay listing has been ended')
    goBack()
  } catch (err) {
    notifyEbayErrors(err)
  }
}

function goBack () { router.push({ name: 'ebay-listings' }) }

onMounted(load)
</script>
