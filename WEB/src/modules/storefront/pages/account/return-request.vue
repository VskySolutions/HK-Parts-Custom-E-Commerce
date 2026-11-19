<template>
  <q-card flat bordered class="q-pa-md">
    <div class="row items-center q-mb-md">
      <q-btn flat round dense icon="o_arrow_back" class="q-mr-sm" :to="{ name: 'shop-account-orders' }" />
      <div>
        <div class="text-h6">Return items</div>
        <div v-if="data" class="text-caption text-grey-7">
          Order {{ data.orderNumber }}<span v-if="data.windowClosesOnUtc"> · returns accepted until {{ formatDay(data.windowClosesOnUtc) }}</span>
        </div>
      </div>
    </div>

    <q-inner-loading :showing="loading" />

    <q-banner v-if="data && !data.eligible" rounded class="bg-orange-1 text-deep-orange">
      {{ data.reason || 'Nothing on this order can be returned.' }}
    </q-banner>

    <template v-if="data && data.eligible">
      <div class="text-subtitle2 q-mb-sm">1. What are you returning?</div>
      <q-list bordered separator class="rounded-borders">
        <q-item v-for="l in lines" :key="l.orderLineItemId" :class="{ 'text-grey-6': !l.eligible }">
          <q-item-section side top>
            <q-checkbox v-model="l.selected" :disable="!l.eligible" />
          </q-item-section>
          <q-item-section>
            <q-item-label class="text-weight-medium">{{ l.productName }}</q-item-label>
            <q-item-label caption>
              {{ l.sku || '' }} · {{ money(l.unitPaid) }} each · ordered {{ l.quantityOrdered }}
              <span v-if="l.quantityOnReturns"> · {{ l.quantityOnReturns }} already returned</span>
            </q-item-label>
            <q-item-label v-if="!l.eligible" caption class="text-negative">{{ l.ineligibleReason }}</q-item-label>
            <q-item-label v-else-if="l.requiresFfl" caption class="text-deep-orange">
              Firearm returns must be shipped by a licensed dealer. Submit the request and we'll contact you with instructions.
            </q-item-label>
            <q-item-label v-else-if="l.shipRestricted" caption class="text-deep-orange">
              This item can't go back by parcel carrier; we'll send return arrangements.
            </q-item-label>

            <div v-if="l.selected" class="row q-col-gutter-sm q-mt-sm">
              <div class="col-4 col-sm-2">
                <q-select v-model="l.quantity" :options="qtyOptions(l)" dense outlined label="Qty" />
              </div>
              <div class="col-8 col-sm-5">
                <q-select v-model="l.reasonId" :options="reasonOptions" emit-value map-options dense outlined label="Reason *" />
              </div>
              <div class="col-12 col-sm-5">
                <q-select v-model="l.condition" :options="conditionOptions" emit-value map-options dense outlined label="Item condition" />
              </div>
              <div class="col-12">
                <q-input
                  v-model="l.comment" type="textarea" autogrow dense outlined maxlength="1000"
                  :label="reasonById(l.reasonId)?.requiresComment ? 'Describe the problem *' : 'Comment (optional)'"
                />
              </div>
            </div>
          </q-item-section>
        </q-item>
      </q-list>

      <div class="text-subtitle2 q-mt-lg q-mb-sm">2. What would you like?</div>
      <q-option-group v-model="resolution" :options="resolutionOptions" type="radio" inline />

      <div class="text-subtitle2 q-mt-md q-mb-sm">3. How will you send it back?</div>
      <q-option-group v-model="returnMethod" :options="methodOptions" type="radio" inline />

      <div class="text-subtitle2 q-mt-md q-mb-sm">4. Photos <span v-if="photosRequired" class="text-negative">*</span></div>
      <q-file
        v-model="photos" multiple append max-files="5" max-file-size="10485760" counter use-chips
        accept=".jpg,.jpeg,.png,.webp,.heic" dense outlined
        :label="photosRequired ? 'Add photos of the damage or defect (required)' : 'Add photos (optional, up to 5)'"
        @rejected="onRejected"
      >
        <template #prepend><q-icon name="o_photo_camera" /></template>
      </q-file>

      <q-input v-model="comment" type="textarea" autogrow dense outlined maxlength="1000" label="Anything else we should know?" class="q-mt-md" />

      <q-card flat bordered class="q-pa-md q-mt-md bg-grey-1">
        <div class="text-subtitle2">Estimated refund</div>
        <div class="row justify-between text-body2 q-mt-xs"><span>Items</span><span>{{ money(estimate.items) }}</span></div>
        <div v-if="estimate.fee" class="row justify-between text-body2"><span>Restocking fee ({{ config.restockFeePercent }}%)</span><span>−{{ money(estimate.fee) }}</span></div>
        <div class="row justify-between text-body1 text-weight-medium q-mt-xs"><span>Estimate</span><span>{{ money(estimate.total) }}</span></div>
        <div class="text-caption text-grey-7 q-mt-xs">
          Plus the tax you paid on these items. The final amount is confirmed after we inspect the return.
          <span v-if="resolution === 'StoreCredit' && config.storeCreditBonusPercent"> Store credit adds a {{ config.storeCreditBonusPercent }}% bonus.</span>
        </div>
      </q-card>

      <q-checkbox v-model="acceptPolicy" class="q-mt-md">
        I have read and accept the
        <a v-if="config.policyUrl" :href="config.policyUrl" target="_blank" rel="noopener" @click.stop>return policy</a>
        <span v-else>return policy</span>
      </q-checkbox>

      <div class="row justify-end q-gutter-sm q-mt-md">
        <q-btn flat no-caps label="Cancel" :to="{ name: 'shop-account-orders' }" />
        <q-btn unelevated color="primary" no-caps label="Submit return request" :loading="submitting" :disable="!canSubmit" @click="submit" />
      </div>
    </template>
  </q-card>
</template>

<script setup>
/* Storefront return request form (docs/order-return-requirements.bak.md section 6.2). */
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useQuasar } from 'quasar'
import { returnsApi } from 'modules/storefront/account-api'
import { formatPrice } from 'modules/storefront/api'
import { getApiErrorMessage } from 'services/api'

const route = useRoute()
const router = useRouter()
const $q = useQuasar()

const loading = ref(true)
const submitting = ref(false)
const data = ref(null)
const config = ref({})
const reasons = ref([])
const lines = ref([])
const resolution = ref('Refund')
const returnMethod = ref('SelfShip')
const photos = ref([])
const comment = ref('')
const acceptPolicy = ref(false)

const conditionOptions = [
  { label: 'Unopened', value: 'Unopened' },
  { label: 'Opened, unused', value: 'OpenedUnused' },
  { label: 'Installed / used', value: 'InstalledUsed' }
]

const money = (v) => formatPrice(v || 0, data.value?.currencyCode || 'USD')
const formatDay = (v) => (v ? new Date(v).toLocaleDateString() : '')
const reasonById = (id) => reasons.value.find((r) => r.id === id)
const reasonOptions = computed(() => reasons.value.map((r) => ({ label: r.name, value: r.id })))
const qtyOptions = (l) => Array.from({ length: l.quantityReturnable }, (_, i) => i + 1)

const resolutionOptions = computed(() => [
  { label: 'Refund to my original payment', value: 'Refund' },
  ...(config.value.allowStoreCredit ? [{ label: 'Store credit' + (config.value.storeCreditBonusPercent ? ` (+${config.value.storeCreditBonusPercent}%)` : ''), value: 'StoreCredit' }] : []),
  ...(config.value.allowExchange ? [{ label: 'Exchange for the same item', value: 'Replacement' }] : [])
])

const methodOptions = computed(() => [
  ...(['PrepaidMerchant', 'PrepaidDeducted'].includes(config.value.labelMode) ? [{ label: 'Send me a prepaid label' + (config.value.labelMode === 'PrepaidDeducted' ? ' (cost deducted from the refund)' : ''), value: 'PrepaidLabel' }] : []),
  { label: "I'll ship it myself", value: 'SelfShip' },
  ...(config.value.allowDropOff ? [{ label: 'Drop it off at a store', value: 'DropOff' }] : [])
])

const selected = computed(() => lines.value.filter((l) => l.selected))
const photosRequired = computed(() => config.value.requirePhotos && selected.value.some((l) => reasonById(l.reasonId)?.requiresPhoto))

const estimate = computed(() => {
  let items = 0
  let feeBase = 0
  for (const l of selected.value) {
    const v = (l.unitPaid || 0) * (l.quantity || 0)
    items += v
    const r = reasonById(l.reasonId)
    if (r?.feeApplies && !r?.merchantFault) feeBase += v
  }
  const fee = Math.round(feeBase * (config.value.restockFeePercent || 0)) / 100
  return { items, fee, total: Math.max(0, items - fee) }
})

const canSubmit = computed(() =>
  selected.value.length > 0 &&
  selected.value.every((l) => l.reasonId && l.quantity > 0 && (!reasonById(l.reasonId)?.requiresComment || (l.comment || '').trim())) &&
  (!photosRequired.value || photos.value.length > 0) &&
  acceptPolicy.value)

function onRejected () {
  $q.notify({ type: 'warning', message: 'Photos must be JPG, PNG, WEBP or HEIC, at most 10 MB each, up to 5.' })
}

async function load () {
  loading.value = true
  try {
    const [d, c, r] = await Promise.all([
      returnsApi.returnable(route.params.orderId),
      returnsApi.config(),
      returnsApi.reasons()
    ])
    data.value = d
    config.value = c || {}
    reasons.value = r || []
    lines.value = (d.lines || []).map((l) => ({
      ...l,
      selected: false,
      quantity: l.quantityReturnable > 0 ? 1 : 0,
      reasonId: null,
      condition: 'Unopened',
      comment: ''
    }))
    if (!methodOptions.value.some((o) => o.value === returnMethod.value)) returnMethod.value = methodOptions.value[0]?.value
  } catch (e) {
    $q.notify({ type: 'negative', message: getApiErrorMessage(e) })
  } finally {
    loading.value = false
  }
}

async function submit () {
  submitting.value = true
  try {
    const rma = await returnsApi.submit({
      orderId: route.params.orderId,
      lines: selected.value.map((l) => ({
        orderLineItemId: l.orderLineItemId,
        quantity: l.quantity,
        reasonId: l.reasonId,
        comment: (l.comment || '').trim() || null,
        condition: l.condition
      })),
      requestedResolution: resolution.value,
      returnMethod: returnMethod.value,
      comment: comment.value.trim() || null,
      acceptPolicy: acceptPolicy.value
    }, photos.value)
    $q.notify({ type: 'positive', message: `Return ${rma.rmaNumber} submitted. We've emailed you the next steps.` })
    router.replace({ name: 'shop-account-return-detail', params: { id: rma.id } })
  } catch (e) {
    $q.notify({ type: 'negative', message: getApiErrorMessage(e), multiLine: true })
  } finally {
    submitting.value = false
  }
}

onMounted(load)
</script>
