<template>
  <div class="relative-position">
    <q-inner-loading :showing="loading" />

    <div class="row q-col-gutter-md">
      <!-- ============ LEFT: settings ============ -->
      <div class="col-12 col-md-7">
        <AppSection title="ShipStation Custom Store">
          <q-toggle v-model="form.enabled" label="Enabled" color="primary" :disable="!canWrite" />
          <div class="text-caption text-grey-7 q-mb-md">
            While off, ShipStation's calls are refused and no orders are sent.
          </div>

          <div class="row q-col-gutter-md">
            <div class="col-12 col-sm-6">
              <AppTextField
                v-model="form.username"
                label="Username"
                :required="form.enabled"
                autocomplete="off"
                :disable="!canWrite"
              />
            </div>
            <div class="col-12 col-sm-6">
              <AppPasswordField
                v-model="form.password"
                label="Password"
                :required="form.enabled && !passwordSet"
                :placeholder="passwordSet ? '••••• (set) — leave blank to keep' : 'At least 8 characters'"
                :disable="!canWrite"
              />
            </div>
          </div>
          <div class="text-caption text-grey-7 q-mt-xs">
            Enter the same username and password in ShipStation's Custom Store setup. The password is stored
            encrypted and is never shown again.
          </div>

          <q-separator class="q-my-md" />

          <q-toggle v-model="form.includeEbay" label="Send eBay orders" color="primary" :disable="!canWrite" />
          <q-toggle v-model="form.splitFfl" label="Split mixed FFL orders" color="primary" :disable="!canWrite" />
          <div class="text-caption text-grey-7 q-mb-sm">
            A cart with FFL and non-FFL items is sent as two ShipStation orders: the order number for the customer's
            items, and the order number + “-FFL” for the firearms going to the dealer.
          </div>
          <q-toggle v-model="form.notifyCustomer" label="Email the customer when ShipStation reports a shipment" color="primary" :disable="!canWrite" />
          <div class="text-caption text-grey-7 q-mb-sm">
            Turn this off if ShipStation sends its own shipment emails, so customers don't get two.
          </div>

          <AppSelect
            v-model="form.exportStatuses"
            class="q-mt-sm"
            label="Order statuses exported"
            :options="shipStationStatusOptions"
            multiple use-chips emit-value map-options
            :disable="!canWrite"
          >
            <template #option="scope">
              <q-item v-bind="scope.itemProps">
                <q-item-section side>
                  <q-checkbox :model-value="scope.selected" dense @update:model-value="scope.toggleOption(scope.opt)" />
                </q-item-section>
                <q-item-section>
                  <q-item-label>{{ scope.opt.label }}</q-item-label>
                  <q-item-label caption>ShipStation status: {{ scope.opt.caption }}</q-item-label>
                </q-item-section>
              </q-item>
            </template>
          </AppSelect>
          <div class="text-caption text-grey-7 q-mt-xs">
            Only paid orders are sent. Cancelled orders are re-sent so ShipStation cancels them too. Map these
            statuses the same way in the ShipStation store setup.
          </div>

          <AppSelect
            v-model="form.timeZone"
            class="q-mt-md"
            label="Store time zone"
            :options="timeZoneOptions"
            use-input fill-input hide-selected input-debounce="0"
            new-value-mode="add-unique"
            :disable="!canWrite"
            @filter="filterTimeZones"
          />
          <div class="text-caption text-grey-7 q-mt-xs">ShipStation's date window and the exported dates use this zone.</div>
        </AppSection>

        <AppSection title="Security and logging" class="q-mt-md">
          <q-toggle v-model="form.allowQueryCredentials" label="Also accept the legacy SS-UserName / SS-Password query parameters" color="primary" :disable="!canWrite" />
          <div class="text-caption text-grey-7 q-mb-sm">
            ShipStation's current Custom Store setup uses Basic auth. Keep this on only until the ShipStation store is
            switched over, then turn it off.
          </div>
          <q-toggle v-model="form.debugLogging" label="Debug logging (full requests and responses)" color="primary" :disable="!canWrite" />
          <div class="text-caption text-grey-7">
            <template v-if="debugUntil">On until {{ formatDateTime(debugUntil) }}; it switches itself off after 24 hours.</template>
            <template v-else>Logs customer names and addresses, so it switches itself off after 24 hours.</template>
          </div>
        </AppSection>

        <div v-if="canWrite" class="row justify-end q-mt-md">
          <q-btn unelevated no-caps color="primary" icon="o_save" label="Save" :loading="saving" @click="save" />
        </div>
      </div>

      <!-- ============ RIGHT: connection + activity ============ -->
      <div class="col-12 col-md-5">
        <AppSection title="Connection">
          <div class="text-caption text-grey-7">Feed URL — paste this into ShipStation as the Custom Store URL:</div>
          <div class="row items-center no-wrap q-mt-xs">
            <code class="col ellipsis">{{ feedUrl || '—' }}</code>
            <q-btn flat round dense size="sm" icon="o_content_copy" aria-label="Copy feed URL" :disable="!feedUrl" @click="copyFeedUrl" />
          </div>

          <q-list dense class="q-mt-md">
            <q-item>
              <q-item-section>
                <q-item-label caption>Last export</q-item-label>
                <q-item-label>{{ describeCall(lastExport) }}</q-item-label>
              </q-item-section>
            </q-item>
            <q-item>
              <q-item-section>
                <q-item-label caption>Last ship notification</q-item-label>
                <q-item-label>{{ describeCall(lastShipNotify) }}</q-item-label>
              </q-item-section>
            </q-item>
          </q-list>
        </AppSection>

        <AppSection title="Recent activity" class="q-mt-md">
          <q-table
            flat dense
            :rows="activity"
            :columns="activityColumns"
            row-key="id"
            :loading="activityLoading"
            :pagination="{ rowsPerPage: 0 }"
            hide-bottom
            no-data-label="ShipStation has not called yet."
          >
            <template #body-cell-httpStatus="props">
              <q-td :props="props">
                <q-badge :color="props.value < 300 ? 'positive' : (props.value === 404 || props.value === 409 ? 'warning' : 'negative')">
                  {{ props.value }}
                </q-badge>
              </q-td>
            </template>
          </q-table>
          <div class="row justify-end">
            <q-btn flat dense no-caps size="sm" color="primary" icon="o_refresh" label="Refresh" @click="loadActivity" />
          </div>
        </AppSection>
      </div>
    </div>
  </div>
</template>

<script setup>
/* ShipStation (docs/shipstation-verification-requirements.md §5.1): Custom Store credentials, export options,
 * feed URL and the call log. Saved with the Save button: the password is write-only, so the form posts it only
 * when typed, and the server refuses "enabled" without a username and password. */
import { ref, reactive, computed, onMounted } from 'vue'
import { copyToClipboard } from 'quasar'
import { getApiErrorMessage } from 'services/api'
import { usePermissions } from 'composables/usePermissions'
import { useNotify } from 'composables/useNotify'
import { shipStationApi, shipStationStatusOptions } from 'modules/shipping/api'
import AppSection from 'components/common/AppSection.vue'
import AppTextField from 'components/common/AppTextField.vue'
import AppSelect from 'components/common/AppSelect.vue'
import AppPasswordField from 'components/common/AppPasswordField.vue'

const notify = useNotify()
const { has } = usePermissions()
const canWrite = computed(() => has('Stores.Write'))

defineProps({ search: { type: String, default: '' } })
const emit = defineEmits(['save-status'])
function noop () {}
defineExpose({ onAdd: noop, openFilters: noop })

const loading = ref(false)
const saving = ref(false)
const passwordSet = ref(false)
const debugUntil = ref(null)
const feedUrl = ref('')
const lastExport = ref(null)
const lastShipNotify = ref(null)
const activity = ref([])
const activityLoading = ref(false)

const form = reactive({
  enabled: false,
  username: '',
  password: '',
  includeEbay: false,
  splitFfl: true,
  notifyCustomer: true,
  exportStatuses: ['Pending', 'Processing', 'Shipped', 'Cancelled'],
  timeZone: 'America/New_York',
  allowQueryCredentials: true,
  debugLogging: false
})

// Common US zones first; any IANA id the browser knows can be typed or picked.
const allTimeZones = (() => {
  const common = ['America/New_York', 'America/Chicago', 'America/Denver', 'America/Phoenix', 'America/Los_Angeles', 'America/Anchorage', 'Pacific/Honolulu', 'UTC']
  let all = []
  try { all = Intl.supportedValuesOf ? Intl.supportedValuesOf('timeZone') : [] } catch { all = [] }
  return [...new Set([...common, ...all])]
})()
const timeZoneOptions = ref(allTimeZones)
function filterTimeZones (val, update) {
  update(() => {
    const needle = (val || '').toLowerCase()
    timeZoneOptions.value = needle ? allTimeZones.filter((z) => z.toLowerCase().includes(needle)) : allTimeZones
  })
}

const activityColumns = [
  { name: 'createdOnUtc', label: 'When', field: 'createdOnUtc', align: 'left', format: (v) => formatDateTime(v) },
  { name: 'action', label: 'Action', field: 'action', align: 'left' },
  { name: 'detail', label: 'Detail', field: (r) => describeDetail(r), align: 'left', classes: 'ellipsis', style: 'max-width: 180px' },
  { name: 'httpStatus', label: 'Result', field: 'httpStatus', align: 'center' }
]

function formatDateTime (v) {
  if (!v) return ''
  return new Date(v).toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' })
}

function describeDetail (r) {
  if (r.action === 'export') return `${r.orderCount ?? 0} order(s), page ${r.page ?? 1}`
  if (r.action === 'shipnotify') return [r.orderNumber, r.error].filter(Boolean).join(' — ')
  return r.error || ''
}

function describeCall (r) {
  if (!r) return 'Never'
  const ok = r.httpStatus < 300 ? 'OK' : `HTTP ${r.httpStatus}`
  return `${formatDateTime(r.createdOnUtc)} · ${ok} · ${describeDetail(r)}`
}

function apply (d) {
  Object.assign(form, {
    enabled: d.enabled,
    username: d.username || '',
    password: '',
    includeEbay: d.includeEbay,
    splitFfl: d.splitFfl,
    notifyCustomer: d.notifyCustomer,
    exportStatuses: d.exportStatuses || [],
    timeZone: d.timeZone || 'America/New_York',
    allowQueryCredentials: d.allowQueryCredentials,
    debugLogging: d.debugLogging
  })
  passwordSet.value = !!d.passwordSet
  debugUntil.value = d.debugUntilUtc
  feedUrl.value = d.feedUrl || ''
  lastExport.value = d.lastExport
  lastShipNotify.value = d.lastShipNotify
}

async function load () {
  loading.value = true
  try {
    apply(await shipStationApi.get())
  } catch (err) { notify.error(getApiErrorMessage(err)) } finally { loading.value = false }
}

async function loadActivity () {
  activityLoading.value = true
  try {
    const page = await shipStationApi.activity({ page: 1, pageSize: 15 })
    activity.value = page?.items || []
  } catch (err) { notify.error(getApiErrorMessage(err)) } finally { activityLoading.value = false }
}

async function save () {
  saving.value = true
  try {
    const saved = await shipStationApi.update({ ...form, password: form.password || null })
    apply(saved)
    notify.success('ShipStation settings updated successfully')
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    saving.value = false
  }
}

async function copyFeedUrl () {
  try {
    await copyToClipboard(feedUrl.value)
    notify.success('Feed URL copied')
  } catch { notify.error('Could not copy the URL') }
}

onMounted(() => {
  emit('save-status', null)
  load()
  loadActivity()
})
</script>
