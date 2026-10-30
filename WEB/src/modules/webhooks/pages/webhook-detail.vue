<template>
  <q-page class="app-page">
    <AppDetailHeader
      :title="isCreate ? 'New webhook' : (entity?.url || 'Webhook')"
      :breadcrumbs="[
        { label: 'Home', icon: 'o_home', to: '/dashboard' },
        { label: 'Webhooks', to: { name: 'admin-webhooks' } },
        { label: isCreate ? 'New webhook' : (entity?.url || 'Webhook') }
      ]"
      :status="!isCreate && entity ? (form.isActive ? 'Active' : 'Paused') : ''"
      :status-color="form.isActive ? 'positive' : 'grey'"
      show-back
      @back="router.push({ name: 'admin-webhooks' })"
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
      Webhook not found.
    </q-banner>

    <!-- Signing secret: shown exactly once, right after creation -->
    <q-banner v-if="justSecret" class="bg-orange-1 text-orange-9 q-mb-md" rounded>
      <template #avatar><q-icon name="o_vpn_key" color="orange" /></template>
      <div class="text-weight-medium">Signing secret — copy it now, it is shown only once.</div>
      <q-input :model-value="justSecret" readonly dense outlined class="q-mt-sm bg-white">
        <template #append><q-btn flat round dense icon="o_content_copy" @click="copySecret"><q-tooltip>Copy</q-tooltip></q-btn></template>
      </q-input>
      <template #action><q-btn flat color="orange-9" no-caps label="Dismiss" @click="justSecret = ''" /></template>
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
          <q-tab name="general" icon="o_webhook" label="General" />
          <q-tab name="deliveries" icon="o_history" label="Deliveries" :disable="isCreate" />
        </q-tabs>

        <q-separator />

        <q-tab-panels v-model="tab" animated keep-alive>
          <!-- ============ GENERAL ============ -->
          <q-tab-panel name="general" class="q-gutter-y-sm">
            <AppTextField v-model="form.url" label="Payload URL" required :v="v$.url" placeholder="https://example.com/webhooks/hkparts" hint="The https endpoint that will receive signed POST requests" :disable="!canWrite" />

            <AppFieldLabel label="Events" />
            <q-select
              v-model="form.eventTypes"
              multiple use-chips dense outlined emit-value map-options
              :options="eventOptions"
              placeholder="Select one or more events"
              :error="v$.eventTypes.$error"
              :disable="!canWrite"
              @blur="v$.eventTypes.$touch()"
            />

            <AppTextField v-model="form.description" label="Description" placeholder="e.g. Notify fulfilment service of new orders" hint="Optional note to identify this endpoint" :disable="!canWrite" />
            <q-toggle v-if="!isCreate" v-model="form.isActive" label="Active" color="primary" :disable="!canWrite" />
          </q-tab-panel>

          <!-- ============ DELIVERIES ============ -->
          <q-tab-panel name="deliveries" class="q-gutter-y-sm">
            <div class="row items-center q-mb-sm">
              <div class="text-body2 text-grey-7 col">Recent delivery attempts to this endpoint.</div>
              <q-btn flat round dense icon="o_refresh" :loading="deliveriesLoading" @click="loadDeliveries"><q-tooltip>Refresh</q-tooltip></q-btn>
            </div>
            <div v-if="!deliveryRows.length && !deliveriesLoading" class="text-grey-6 text-caption q-pa-md text-center">No deliveries recorded yet.</div>
            <q-markup-table v-else flat dense bordered>
              <thead>
                <tr>
                  <th class="text-left">Event</th>
                  <th class="text-left">Status</th>
                  <th class="text-center">Attempts</th>
                  <th class="text-center">Response</th>
                  <th class="text-left">Occurred</th>
                  <th class="text-left">Last attempt</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="d in deliveryRows" :key="d.id">
                  <td class="text-left">{{ d.eventType }}</td>
                  <td class="text-left"><q-badge :color="statusColor(d.status)" :label="d.status" /></td>
                  <td class="text-center">{{ d.attemptCount }}</td>
                  <td class="text-center">{{ d.lastResponseStatus ?? '—' }}</td>
                  <td class="text-left">{{ formatDate(d.occurredAtUtc, true) }}</td>
                  <td class="text-left">{{ d.lastAttemptOnUtc ? formatDate(d.lastAttemptOnUtc, true) : '—' }}</td>
                </tr>
              </tbody>
            </q-markup-table>
          </q-tab-panel>
        </q-tab-panels>

        <template v-if="isCreate">
          <q-separator />
          <q-card-actions class="q-pa-md">
            <div class="text-caption text-grey-7">Create the webhook to reveal its signing secret and unlock delivery history.</div>
            <q-space />
            <q-btn v-if="canWrite" unelevated color="primary" no-caps icon="o_check" label="Create webhook" :loading="creating" @click="create" />
          </q-card-actions>
        </template>
      </q-card>
    </template>

    <AppRecordMeta entity-type="webhook" :record-id="entity?.id" />
  </q-page>
</template>

<script setup>
/*
 * Webhook create + manage page (full-page auto-save via useDetailForm). The subscription list has no
 * get-by-id endpoint, so the api adapter resolves one from the list; create captures the one-time
 * signing secret into a dismissible banner. Deliveries tab shows this endpoint's recent attempts.
 */
import { ref, computed } from 'vue'
import { useRouter } from 'vue-router'
import { copyToClipboard } from 'quasar'
import { getApiErrorMessage } from 'services/api'
import { usePermissions } from 'composables/usePermissions'
import { useNotify } from 'composables/useNotify'
import { useDetailForm } from 'composables/useDetailForm'
import { required, maxLength, url as urlRule } from 'validators'
import { webhookApi, webhookDeliveryStatusColor as statusColor } from 'modules/webhooks/api'
import { formatDate } from 'modules/orders/api'
import AppDetailHeader from 'components/common/AppDetailHeader.vue'
import AppTextField from 'components/common/AppTextField.vue'
import AppFieldLabel from 'components/common/AppFieldLabel.vue'

const router = useRouter()
const notify = useNotify()
const { has } = usePermissions()
const canWrite = computed(() => has('Webhooks.Write'))

const tab = ref('general')
const justSecret = ref('')

const eventOptions = ref([])
async function loadEventTypes () {
  try {
    const types = await webhookApi.eventTypes()
    eventOptions.value = (Array.isArray(types) ? types : []).map((t) => ({ label: t, value: t }))
  } catch (e) { eventOptions.value = [] }
}
loadEventTypes()

function buildPayload (f) {
  return {
    url: (f.url || '').trim(),
    eventTypes: f.eventTypes,
    description: f.description?.trim() || null,
    isActive: f.isActive
  }
}

// No get-by-id endpoint: resolve one subscription from the full list. Create captures the secret.
const detailApi = {
  get: async (id) => {
    const all = await webhookApi.list()
    return (Array.isArray(all) ? all : all?.items || []).find((w) => w.id === id) || null
  },
  create: async (payload) => {
    const created = await webhookApi.create(payload)
    if (created?.secret) justSecret.value = created.secret
    return created
  },
  update: (id, payload) => webhookApi.update(id, payload)
}

const {
  form, v$, entity, loading, creating, isCreate, id, saveStatus, create
} = useDetailForm({
  createRouteName: 'webhook-new',
  detailRouteName: 'webhook-detail',
  entityLabel: 'webhook',
  api: detailApi,
  buildPayload,
  empty: { url: '', eventTypes: [], description: '', isActive: true },
  rules: {
    url: { required, maxLength: maxLength(2048), url: urlRule },
    eventTypes: { required }
  },
  hydrateForm: (f, e) => { f.eventTypes = [...(e.eventTypes || [])] },
  afterLoad: () => loadDeliveries()
})

// ---- Deliveries ----------------------------------------------------------------
const deliveryRows = ref([])
const deliveriesLoading = ref(false)
async function loadDeliveries () {
  if (!id.value) return
  deliveriesLoading.value = true
  try {
    const r = await webhookApi.deliveries({ subscriptionId: id.value, page: 1, pageSize: 100 })
    deliveryRows.value = Array.isArray(r?.items) ? r.items : []
  } catch (e) { deliveryRows.value = [] } finally { deliveriesLoading.value = false }
}

async function copySecret () {
  try { await copyToClipboard(justSecret.value); notify.success('Secret copied') } catch (e) { /* clipboard unavailable */ }
}
</script>

<style scoped lang="scss">
.app-detail-tabs {
  :deep(.q-tab) { min-height: 44px; }
}
</style>
