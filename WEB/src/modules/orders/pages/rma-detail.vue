<template>
  <q-page class="app-page">
    <AppDetailHeader
      :breadcrumbs="[
        { label: 'Home', icon: 'o_home', to: '/dashboard' },
        { label: 'Returns', to: '/returns' },
        { label: rma ? rma.rmaNumber : 'RMA' }
      ]"
      :status="rma ? rma.status : ''"
      :status-color="rma ? rmaStatusColor(rma.status) : 'grey'"
      @back="$router.push({ name: 'admin-rmas' })"
    />

    <q-inner-loading :showing="loading" />

    <div v-if="rma" class="row q-col-gutter-md">
      <div class="col-12 col-md-8">
        <AppSection title="Return details">
          <div class="row q-col-gutter-md text-body2">
            <div class="col-6 col-sm-4"><div class="text-caption text-grey-7">Requested</div>{{ formatDate(rma.requestedOnUtc, true) }}</div>
            <div class="col-6 col-sm-4"><div class="text-caption text-grey-7">Resolution</div>{{ rma.resolution }}</div>
            <div v-if="rma.refundedAmount != null" class="col-6 col-sm-4"><div class="text-caption text-grey-7">Refunded</div>{{ formatMoney(rma.refundedAmount) }}</div>
            <div v-if="rma.storeCreditIssued != null" class="col-6 col-sm-4"><div class="text-caption text-grey-7">Store credit issued</div>{{ formatMoney(rma.storeCreditIssued) }}</div>
            <div v-if="rma.replacementOrderId" class="col-6 col-sm-4"><div class="text-caption text-grey-7">Replacement order</div><a class="text-primary cursor-pointer" @click="openReplacement">View order</a></div>
            <div class="col-12"><div class="text-caption text-grey-7">Reason</div>{{ rma.reason || '—' }}</div>
            <div v-if="rma.resolutionNotes" class="col-12"><div class="text-caption text-grey-7">Resolution notes</div>{{ rma.resolutionNotes }}</div>
          </div>
        </AppSection>

        <AppSection title="Returned items" class="q-mt-md">
          <q-markup-table flat dense>
            <thead>
              <tr><th class="text-left">Product</th><th class="text-left">SKU</th><th class="text-right">Qty</th><th class="text-left">Reason</th></tr>
            </thead>
            <tbody>
              <tr v-for="l in rma.lines" :key="l.id">
                <td>{{ l.productName }}</td>
                <td>{{ l.sku || '—' }}</td>
                <td class="text-right">{{ l.quantity }}</td>
                <td>{{ l.lineReason || '—' }}</td>
              </tr>
            </tbody>
          </q-markup-table>
        </AppSection>
      </div>

      <div class="col-12 col-md-4">
        <AppSection title="Review">
          <template v-if="rma.status === 'Requested'">
            <AppSelect v-model="form.resolution" label="Resolution" :options="rmaResolutionOptions" />
            <div v-if="form.resolution === 'Replacement'" class="text-caption text-grey-7 q-mt-sm">
              Approving creates a no-charge replacement order for the returned items.
            </div>
            <q-input
              v-if="form.resolution === 'Refund' || form.resolution === 'StoreCredit'"
              v-model.number="form.refundAmount"
              type="number" dense outlined step="0.01" class="q-mt-sm"
              :label="`${form.resolution === 'Refund' ? 'Refund' : 'Store credit'} amount (optional)`"
              hint="Blank = sum of returned line values"
            />
            <q-input v-model="form.notes" type="textarea" autogrow dense outlined label="Notes" class="q-mt-sm" />
            <div class="column q-gutter-sm q-mt-md">
              <q-btn color="positive" unelevated no-caps icon="o_check" label="Approve" :loading="busy" @click="resolve(true)" />
              <q-btn color="negative" outline no-caps icon="o_close" label="Reject" :loading="busy" @click="resolve(false)" />
            </div>
          </template>
          <div v-else class="text-grey-6">This return is <strong>{{ rma.status }}</strong> and can no longer be actioned.</div>
        </AppSection>
      </div>
    </div>

    <AppRecordMeta entity-type="rma" :record-id="rma?.id" />
  </q-page>
</template>

<script setup>
/* Admin RMA detail + approve/reject/resolve (WO-114, WO-48). */
import { ref, reactive, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'
import { rmaApi, rmaStatusColor, rmaResolutionOptions, formatMoney, formatDate } from 'modules/orders/api'

const route = useRoute()
const router = useRouter()
const notify = useNotify()

function openReplacement () {
  if (rma.value?.replacementOrderId) router.push({ name: 'admin-order-detail', params: { id: rma.value.replacementOrderId } })
}

const rma = ref(null)
const loading = ref(false)
const busy = ref(false)
const form = reactive({ resolution: 'Refund', refundAmount: null, notes: '' })

async function load () {
  loading.value = true
  try {
    rma.value = await rmaApi.get(route.params.id)
  } catch (err) {
    notify.error(getApiErrorMessage(err))
    rma.value = null
  } finally {
    loading.value = false
  }
}

async function resolve (approve) {
  busy.value = true
  try {
    await rmaApi.resolve(route.params.id, {
      approve,
      resolution: approve ? form.resolution : 'None',
      refundAmount: approve && (form.resolution === 'Refund' || form.resolution === 'StoreCredit') && form.refundAmount ? Number(form.refundAmount) : null,
      notes: form.notes || null
    })
    notify.success(approve ? 'Return approved' : 'Return rejected')
    await load()
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    busy.value = false
  }
}

onMounted(load)
</script>
