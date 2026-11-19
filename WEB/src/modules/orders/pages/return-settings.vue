<template>
  <q-page class="app-page">
    <AppListHeader
      title="Return Settings"
      subtitle="The return policy customers see and the rules staff work to."
      :breadcrumbs="[{ label: 'Home', icon: 'o_home', to: '/dashboard' }, { label: 'Returns', to: '/returns' }, { label: 'Settings' }]"
    >
      <template #actions>
        <q-btn unelevated color="primary" no-caps icon="o_save" label="Save settings" :loading="saving" @click="save" />
      </template>
    </AppListHeader>

    <q-inner-loading :showing="loading" color="primary" />

    <template v-if="!loading && form">
      <AppSection title="Policy">
        <q-toggle v-model="form.enabled" label="Customers can request returns online" />
        <div class="text-body2 text-muted q-mb-sm">When off, staff can still create returns from an order.</div>
        <div class="row q-col-gutter-md">
          <div class="col-12 col-md-3">
            <AppTextField label="Return window (days)" type="number" hint="Counted from the ship date" :model-value="form.windowDays" @update:model-value="form.windowDays = toInt($event)" />
          </div>
          <div class="col-12 col-md-5">
            <q-select
              v-model="form.eligibleStatuses" multiple use-chips emit-value map-options dense outlined
              label="Order statuses that can be returned" :options="orderStatusOptions"
            />
          </div>
          <div class="col-12 col-md-4">
            <AppTextField label="Return policy page" placeholder="/return-policy" :model-value="form.policyUrl" @update:model-value="form.policyUrl = $event" />
          </div>
        </div>
        <div class="row q-gutter-md q-mt-xs">
          <q-toggle v-model="form.allowStoreCredit" label="Offer store credit" />
          <q-toggle v-model="form.allowExchange" label="Offer exchanges" />
          <q-toggle v-model="form.allowDropOff" label="Offer drop-off at a store" />
          <q-toggle v-model="form.requirePhotos" label="Require photos for damaged / defective" />
        </div>
      </AppSection>

      <AppSection title="Money" class="q-mt-md">
        <div class="row q-col-gutter-md">
          <div class="col-12 col-md-3">
            <AppTextField label="Restocking fee (%)" type="number" hint="Only for reasons marked “fee applies”" :model-value="form.restockFeePercent" @update:model-value="form.restockFeePercent = toNum($event)" />
          </div>
          <div class="col-12 col-md-3">
            <AppTextField label="Store-credit bonus (%)" type="number" hint="Extra credit when the customer picks store credit" :model-value="form.storeCreditBonusPercent" @update:model-value="form.storeCreditBonusPercent = toNum($event)" />
          </div>
          <div class="col-12 col-md-3">
            <q-toggle v-model="form.autoApprove" label="Auto-approve small returns" />
          </div>
          <div class="col-12 col-md-3">
            <AppTextField label="Auto-approve up to" type="number" :disable="!form.autoApprove" hint="Estimated refund; reasons must allow it" :model-value="form.autoApproveMax" @update:model-value="form.autoApproveMax = toNum($event)" />
          </div>
        </div>
      </AppSection>

      <AppSection title="Return shipping" class="q-mt-md">
        <div class="row q-col-gutter-md">
          <div class="col-12 col-md-5">
            <AppSelect v-model="form.labelMode" label="Return label" :options="returnLabelModeOptions" />
          </div>
          <div class="col-12 col-md-3">
            <AppSelect v-model="form.labelCarrier" label="Default carrier" :options="carrierOptions" />
          </div>
          <div class="col-12 col-md-4">
            <AppTextField label="Approved return expires after (days)" type="number" :model-value="form.labelExpiryDays" @update:model-value="form.labelExpiryDays = toInt($event)" />
          </div>
        </div>
        <div class="text-subtitle2 q-mt-md">Ship returns to</div>
        <div class="row q-col-gutter-md">
          <div class="col-12 col-md-4"><AppTextField v-model="form.returnAddress.name" label="Name" /></div>
          <div class="col-12 col-md-4"><AppTextField v-model="form.returnAddress.company" label="Company" /></div>
          <div class="col-12 col-md-4"><AppTextField v-model="form.returnAddress.phone" label="Phone" /></div>
          <div class="col-12 col-md-6"><AppTextField v-model="form.returnAddress.line1" label="Address line 1" /></div>
          <div class="col-12 col-md-6"><AppTextField v-model="form.returnAddress.line2" label="Address line 2" /></div>
          <div class="col-12 col-md-4"><AppTextField v-model="form.returnAddress.city" label="City" /></div>
          <div class="col-6 col-md-3"><AppTextField v-model="form.returnAddress.state" label="State" /></div>
          <div class="col-6 col-md-3"><AppTextField v-model="form.returnAddress.postalCode" label="ZIP" /></div>
          <div class="col-12 col-md-2"><AppTextField v-model="form.returnAddress.country" label="Country" /></div>
        </div>
      </AppSection>

      <AppSection title="Notifications and stock" class="q-mt-md">
        <div class="row q-col-gutter-md">
          <div class="col-12 col-md-6">
            <AppTextField label="Staff returns email" hint="Alerted on every new request, customer reply and failed refund. Comma separated." :model-value="form.notifyEmail" @update:model-value="form.notifyEmail = $event" />
          </div>
          <div class="col-12 col-md-6">
            <AppSelect v-model="form.defaultRestockStoreId" label="Restock store when the order has none" clearable :options="storeOptions" />
          </div>
        </div>
      </AppSection>

      <AppSection title="Return reasons" class="q-mt-md">
        <template #actions>
          <q-btn flat color="primary" no-caps icon="o_add" label="Add reason" @click="editReason(null)" />
        </template>
        <q-markup-table flat dense>
          <thead>
            <tr>
              <th class="text-left">Reason</th><th>Customer can pick</th><th>Fee applies</th><th>Our fault</th>
              <th>Auto-approve</th><th>Comment</th><th>Photo</th><th>Active</th><th></th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="r in reasons" :key="r.id" :class="{ 'text-grey-6': !r.isActive }">
              <td>{{ r.name }}</td>
              <td class="text-center"><q-icon v-if="r.customerVisible" name="o_check" /></td>
              <td class="text-center"><q-icon v-if="r.feeApplies" name="o_check" /></td>
              <td class="text-center"><q-icon v-if="r.merchantFault" name="o_check" /></td>
              <td class="text-center"><q-icon v-if="r.autoApprove" name="o_check" /></td>
              <td class="text-center"><q-icon v-if="r.requiresComment" name="o_check" /></td>
              <td class="text-center"><q-icon v-if="r.requiresPhoto" name="o_check" /></td>
              <td class="text-center"><q-icon v-if="r.isActive" name="o_check" /></td>
              <td class="text-right">
                <q-btn flat round dense icon="o_edit" @click="editReason(r)" />
                <q-btn flat round dense icon="o_delete" color="negative" @click="removeReason(r)" />
              </td>
            </tr>
          </tbody>
        </q-markup-table>
        <div class="text-caption text-grey-7 q-mt-xs">
          “Our fault” reasons never pay a restocking fee or label cost, and the whole order coming back refunds shipping too.
          A reason already used on a return is deactivated instead of deleted.
        </div>
      </AppSection>
    </template>

    <AppFormDrawer v-model="reasonDrawer" :title="reasonForm.id ? 'Edit reason' : 'New reason'" :saving="saving" @submit="saveReason" @cancel="reasonDrawer = false">
      <div class="q-gutter-y-sm">
        <AppTextField v-model="reasonForm.name" label="Name" required />
        <AppTextField v-model="reasonForm.sortOrder" label="Sort order" type="number" />
        <q-toggle v-model="reasonForm.isActive" label="Active" />
        <q-toggle v-model="reasonForm.customerVisible" label="Customers can pick it" />
        <q-toggle v-model="reasonForm.feeApplies" label="Restocking fee applies" />
        <q-toggle v-model="reasonForm.merchantFault" label="Our fault (waive fee and label cost)" />
        <q-toggle v-model="reasonForm.autoApprove" label="Can be auto-approved" />
        <q-toggle v-model="reasonForm.requiresComment" label="Customer must describe the problem" />
        <q-toggle v-model="reasonForm.requiresPhoto" label="Photo required (when photos are required)" />
      </div>
    </AppFormDrawer>
  </q-page>
</template>

<script setup>
/* Return policy settings + reasons list (docs/order-return-requirements.bak.md section 15). */
import { ref, reactive, onMounted } from 'vue'
import { api, unwrap, getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'
import { confirmation } from 'dialogs/confirmation'
import { rmaApi, returnLabelModeOptions, shipmentCarrierOptions } from 'modules/orders/api'

const notify = useNotify()
const loading = ref(true)
const saving = ref(false)
const form = ref(null)
const reasons = ref([])
const storeOptions = ref([])
const reasonDrawer = ref(false)
const reasonForm = reactive({})

const carrierOptions = shipmentCarrierOptions.filter((c) => c.value !== 'Freightview')
const orderStatusOptions = ['Shipped', 'Delivered', 'ReadyForPickup', 'Processing', 'Accepted']
  .map((s) => ({ label: s.replace(/([a-z])([A-Z])/g, '$1 $2'), value: s }))

const toInt = (v) => (v === '' || v === null ? 0 : parseInt(v, 10) || 0)
const toNum = (v) => (v === '' || v === null ? 0 : Number(v) || 0)

async function load () {
  loading.value = true
  try {
    const [settings, reasonList] = await Promise.all([rmaApi.getSettings(), rmaApi.reasons()])
    settings.returnAddress = settings.returnAddress || {}
    form.value = settings
    reasons.value = reasonList
    try {
      const stores = await api.get('/api/admin/stores', { params: { pageSize: 200 } }).then(unwrap)
      storeOptions.value = (stores?.items || stores || []).map((s) => ({ label: s.name, value: s.id }))
    } catch { /* optional */ }
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    loading.value = false
  }
}

async function save () {
  saving.value = true
  try {
    form.value = await rmaApi.saveSettings(form.value)
    form.value.returnAddress = form.value.returnAddress || {}
    notify.success('Return settings saved')
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    saving.value = false
  }
}

function editReason (r) {
  Object.keys(reasonForm).forEach((k) => delete reasonForm[k])
  Object.assign(reasonForm, r ? { ...r } : {
    id: null, name: '', isActive: true, customerVisible: true, feeApplies: false, merchantFault: false,
    autoApprove: false, requiresComment: false, requiresPhoto: false, sortOrder: reasons.value.length
  })
  reasonDrawer.value = true
}

async function saveReason () {
  if (!reasonForm.name?.trim()) return notify.error('Enter a name')
  saving.value = true
  try {
    await rmaApi.saveReason({ ...reasonForm, sortOrder: toInt(reasonForm.sortOrder) })
    reasons.value = await rmaApi.reasons()
    reasonDrawer.value = false
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    saving.value = false
  }
}

async function removeReason (r) {
  const ok = await confirmation({ title: 'Delete reason', message: `Delete “${r.name}”? If a return uses it, it is deactivated instead.`, color: 'negative', okLabel: 'Delete' })
  if (!ok) return
  try {
    await rmaApi.deleteReason(r.id)
    reasons.value = await rmaApi.reasons()
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  }
}

onMounted(load)
</script>
