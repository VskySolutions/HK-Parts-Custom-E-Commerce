<template>
  <q-card flat bordered class="q-pa-md">
    <div class="row items-center q-mb-md">
      <q-btn flat round dense icon="o_arrow_back" class="q-mr-sm" :to="{ name: 'shop-account-returns' }" />
      <div>
        <div class="text-h6">Return {{ rma ? rma.rmaNumber : '' }}</div>
        <div v-if="rma" class="text-caption text-grey-7">Order {{ rma.orderNumber }} · requested {{ formatDate(rma.requestedOnUtc) }}</div>
      </div>
      <q-space />
      <q-badge v-if="rma" :color="returnStatusColor(rma.status)" :label="returnStatusLabel(rma.status)" class="text-body2 q-pa-sm" />
    </div>

    <q-inner-loading :showing="loading" />

    <template v-if="rma">
      <!-- What to do next -->
      <q-banner v-if="rma.status === 'Requested'" rounded class="bg-blue-1 q-mb-md">
        We're reviewing your request and will email you within 1–2 business days.
      </q-banner>
      <q-banner v-else-if="rma.status === 'Approved'" rounded class="bg-green-1 q-mb-md">
        <div class="text-weight-medium">Your return is approved.</div>
        <div v-if="rma.returnInstructions" class="q-mt-xs">{{ rma.returnInstructions }}</div>
        <div v-if="rma.labelUrl" class="q-mt-xs"><a :href="rma.labelUrl" target="_blank" rel="noopener">Download your return label</a></div>
        <div v-if="rma.labelExpiresAtUtc" class="q-mt-xs">Please ship by {{ formatDay(rma.labelExpiresAtUtc) }}. Put the return number {{ rma.rmaNumber }} inside the box.</div>
      </q-banner>

      <div class="text-subtitle2 q-mb-xs">Items</div>
      <q-list bordered separator class="rounded-borders q-mb-md">
        <q-item v-for="l in rma.lines" :key="l.id">
          <q-item-section>
            <q-item-label class="text-weight-medium">{{ l.productName }}</q-item-label>
            <q-item-label caption>{{ l.quantity }} × · {{ l.lineReason || '' }}</q-item-label>
            <q-item-label v-if="l.deductionReason" caption class="text-deep-orange">Deduction: {{ l.deductionReason }}</q-item-label>
          </q-item-section>
          <q-item-section v-if="l.quantityReceived" side>
            <q-item-label caption>Received {{ l.quantityReceived }}</q-item-label>
          </q-item-section>
        </q-item>
      </q-list>

      <div class="row q-col-gutter-md">
        <div class="col-12 col-sm-6">
          <div class="text-subtitle2 q-mb-xs">{{ rma.refund.isEstimate ? 'Estimated refund' : 'Refund' }}</div>
          <div class="row justify-between text-body2"><span>Items</span><span>{{ money(rma.refund.subtotal) }}</span></div>
          <div class="row justify-between text-body2"><span>Tax</span><span>{{ money(rma.refund.tax) }}</span></div>
          <div v-if="rma.refund.shipping" class="row justify-between text-body2"><span>Shipping</span><span>{{ money(rma.refund.shipping) }}</span></div>
          <div v-if="rma.refund.restockFee" class="row justify-between text-body2"><span>Restocking fee</span><span>−{{ money(rma.refund.restockFee) }}</span></div>
          <div v-if="rma.refund.labelDeduction" class="row justify-between text-body2"><span>Return label</span><span>−{{ money(rma.refund.labelDeduction) }}</span></div>
          <div v-if="rma.refund.adjustment" class="row justify-between text-body2"><span>Adjustment</span><span>{{ money(rma.refund.adjustment) }}</span></div>
          <div class="row justify-between text-body1 text-weight-medium q-mt-xs"><span>Total</span><span>{{ money(rma.refund.total) }}</span></div>
          <div v-if="rma.storeCreditIssued" class="text-positive q-mt-xs">Store credit issued: {{ money(rma.storeCreditIssued) }}</div>
          <div v-else-if="rma.refundedAmount" class="text-positive q-mt-xs">Refunded: {{ money(rma.refundedAmount) }}</div>
          <div v-if="rma.refund.isEstimate" class="text-caption text-grey-7">Confirmed after we inspect the items.</div>
        </div>
        <div class="col-12 col-sm-6">
          <div class="text-subtitle2 q-mb-xs">Progress</div>
          <q-timeline color="primary" layout="dense" class="q-my-none">
            <q-timeline-entry v-for="h in [...rma.history].reverse()" :key="h.id" :title="returnStatusLabel(h.toStatus)" :subtitle="formatDate(h.createdOnUtc)" />
          </q-timeline>
        </div>
      </div>

      <!-- Self-ship tracking -->
      <q-card v-if="can('setTracking')" flat bordered class="q-pa-md q-mt-md">
        <div class="text-subtitle2">Shipped it? Tell us the tracking number</div>
        <div class="row q-col-gutter-sm q-mt-xs">
          <div class="col-12 col-sm-4"><q-select v-model="tracking.carrier" :options="['USPS', 'UPS', 'FedEx', 'DHL', 'Other']" dense outlined label="Carrier" /></div>
          <div class="col-12 col-sm-5"><q-input v-model="tracking.number" dense outlined label="Tracking number" /></div>
          <div class="col-12 col-sm-3"><q-btn unelevated color="primary" no-caps class="full-width" label="Save" :loading="busy" :disable="!tracking.carrier || !tracking.number" @click="saveTracking" /></div>
        </div>
      </q-card>

      <!-- Messages -->
      <div class="text-subtitle2 q-mt-lg q-mb-xs">Messages</div>
      <div v-if="!rma.messages.length" class="text-grey-6 q-mb-sm">No messages yet.</div>
      <q-chat-message
        v-for="m in rma.messages" :key="m.id"
        :name="m.authorType === 'Customer' ? 'You' : (m.authorName || 'Customer service')"
        :text="[m.body]"
        :stamp="formatDate(m.createdOnUtc)"
        :sent="m.authorType === 'Customer'"
      />
      <template v-if="can('message')">
        <q-input v-model="reply" type="textarea" autogrow dense outlined maxlength="4000" label="Write a message" />
        <div class="row items-center q-mt-xs">
          <q-file v-model="upload" dense borderless accept=".jpg,.jpeg,.png,.webp,.heic,.pdf" label="Attach a photo" style="max-width: 220px" @update:model-value="uploadFile">
            <template #prepend><q-icon name="o_attach_file" /></template>
          </q-file>
          <q-space />
          <q-btn unelevated color="primary" no-caps label="Send" :loading="busy" :disable="!reply.trim()" @click="sendReply" />
        </div>
      </template>

      <div v-if="rma.attachments.length" class="q-mt-md">
        <div class="text-subtitle2 q-mb-xs">Photos & files</div>
        <q-chip v-for="a in rma.attachments" :key="a.id" clickable icon="o_attach_file" @click="returnsApi.openAttachment(rma.id, a.id)">{{ a.originalFileName }}</q-chip>
      </div>

      <div v-if="can('cancel')" class="row justify-end q-mt-lg">
        <q-btn flat color="negative" no-caps icon="o_close" label="Cancel this return" :loading="busy" @click="cancelReturn" />
      </div>
    </template>
  </q-card>
</template>

<script setup>
/* Customer view of one return: status, next steps, refund, tracking entry, messages (RT-22 / RT-26 / RT-45). */
import { ref, reactive, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { useQuasar } from 'quasar'
import { returnsApi } from 'modules/storefront/account-api'
import { formatPrice } from 'modules/storefront/api'
import { formatDateTime as formatDate } from 'src/utils/datetime'
import { getApiErrorMessage } from 'services/api'
import { returnStatusColor, returnStatusLabel } from 'modules/storefront/composables/useReturnStatus'

const route = useRoute()
const $q = useQuasar()

const rma = ref(null)
const loading = ref(true)
const busy = ref(false)
const reply = ref('')
const upload = ref(null)
const tracking = reactive({ carrier: 'USPS', number: '' })

const money = (v) => formatPrice(v || 0, rma.value?.currencyCode || 'USD')
const formatDay = (v) => (v ? new Date(v).toLocaleDateString() : '')
const can = (a) => !!rma.value?.availableActions?.includes(a)

async function load () {
  loading.value = true
  try {
    rma.value = await returnsApi.get(route.params.id)
  } catch (e) {
    $q.notify({ type: 'negative', message: getApiErrorMessage(e) })
  } finally {
    loading.value = false
  }
}

async function run (fn, success) {
  busy.value = true
  try {
    rma.value = await fn()
    if (success) $q.notify({ type: 'positive', message: success })
    return true
  } catch (e) {
    $q.notify({ type: 'negative', message: getApiErrorMessage(e) })
    return false
  } finally {
    busy.value = false
  }
}

async function sendReply () {
  if (await run(() => returnsApi.message(rma.value.id, reply.value.trim()), 'Message sent')) reply.value = ''
}

function saveTracking () {
  run(() => returnsApi.setTracking(rma.value.id, tracking.carrier, tracking.number.trim()), 'Thanks — we\'ll watch for your package')
}

async function uploadFile (file) {
  if (!file) return
  busy.value = true
  try {
    await returnsApi.upload(rma.value.id, file)
    await load()
  } catch (e) {
    $q.notify({ type: 'negative', message: getApiErrorMessage(e) })
  } finally {
    upload.value = null
    busy.value = false
  }
}

function cancelReturn () {
  $q.dialog({ title: 'Cancel return', message: 'Cancel this return request?', cancel: true, persistent: true })
    .onOk(() => run(() => returnsApi.cancel(rma.value.id), 'Return cancelled'))
}

onMounted(load)
</script>
