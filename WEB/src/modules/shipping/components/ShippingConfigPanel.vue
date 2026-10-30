<template>
  <div class="relative-position">
    <q-inner-loading :showing="loading" />

    <div class="row q-col-gutter-md">
      <!-- ============ LEFT: what we offer ============ -->
      <div class="col-12 col-md-8">
        <AppSection title="Rate sources">
          <div class="text-caption text-grey-7 q-mb-md">
            Checkout offers the combined rates of every source enabled here, and the customer picks one.
            Unlike tax, this is not an either/or choice — you can run manual methods alongside live carriers.
          </div>

          <q-list separator bordered class="rounded-borders">
            <q-item v-for="carrier in config.carriers" :key="carrier.carrier">
              <q-item-section avatar>
                <q-icon :name="metaFor(carrier.carrier).icon" size="24px" :color="carrier.isEnabled ? 'primary' : 'grey-5'" />
              </q-item-section>

              <q-item-section>
                <q-item-label class="text-weight-medium">{{ carrier.displayName }}</q-item-label>
                <q-item-label caption>{{ metaFor(carrier.carrier).hint }}</q-item-label>
              </q-item-section>

              <q-item-section side>
                <div class="row items-center q-gutter-sm no-wrap">
                  <template v-if="needsCredentials(carrier)">
                    <q-badge outline color="orange" class="q-py-xs">
                      <q-icon name="o_warning" size="14px" class="q-mr-xs" />
                      No credentials
                    </q-badge>
                    <q-btn
                      flat round dense size="sm" color="primary" icon="o_add_link"
                      :aria-label="`Add ${carrier.displayName} credentials`"
                      @click="openCredentials(metaFor(carrier.carrier).section)"
                    >
                      <q-tooltip>Add {{ carrier.displayName }} credentials</q-tooltip>
                    </q-btn>
                  </template>
                  <q-badge v-else-if="carrier.requiresCredentials" outline color="positive" class="q-py-xs">
                    <q-icon name="o_check_circle" size="14px" class="q-mr-xs" />
                    Credentials active
                  </q-badge>

                  <div>
                    <q-toggle v-model="carrier.isEnabled" color="primary" :disable="toggleDisabled(carrier)" />
                    <q-tooltip v-if="toggleReason(carrier)">{{ toggleReason(carrier) }}</q-tooltip>
                  </div>
                </div>
              </q-item-section>
            </q-item>
          </q-list>
        </AppSection>
      </div>

      <!-- ============ RIGHT: how the offer behaves ============ -->
      <div class="col-12 col-md-4">
        <AppSection title="Shipping configuration">
          <q-toggle v-model="config.isEnabled" label="Shipping calculation enabled" color="primary" :disable="!canWrite" />
          <div class="text-caption text-grey-7 q-mt-sm">
            When off, no shipping rates are quoted from any source.
          </div>
        </AppSection>

        <AppSection title="Selection" class="q-mt-md">
          <AppFieldLabel label="How the customer's option is chosen" />
          <q-option-group
            v-model="config.selectionMode"
            :options="selectionModeOptions"
            color="primary"
            :disable="!canWrite || !config.isEnabled"
          />

          <template v-if="isAutomatic">
            <q-separator class="q-my-md" />

            <AppFieldLabel label="Balance" />
            <q-slider
              v-model="config.costVsSpeedWeight"
              :min="0" :max="100" :step="5"
              label :label-value="balanceLabel"
              color="primary"
              :disable="!canWrite || !config.isEnabled"
            />
            <div class="row justify-between text-caption text-grey-7 q-mb-md">
              <span>Prefer faster</span>
              <span>Prefer cheaper</span>
            </div>

            <AppTextField
              v-model="config.assumedTransitDays"
              label="Assume delivery time (days) when unknown"
              type="number"
              placeholder="e.g. 7"
              :disable="!canWrite || !config.isEnabled"
            />
            <div class="text-caption text-grey-7">
              Carriers don't always return a delivery estimate, and a manual method may have none set.
              Without this, an option with no estimate would be scored as if it arrived instantly.
            </div>
          </template>
        </AppSection>

        <AppSection title="Store pickup" class="q-mt-md">
          <q-toggle v-model="config.pickupEnabled" label="Offer pickup in store" color="primary" :disable="!canWrite" />
          <div class="text-caption text-grey-7 q-mt-sm">
            Lets shoppers collect their order instead of having it delivered. Independent of the shipping
            switch above — collection quotes no carrier, so it still works with delivery turned off.
          </div>

          <!-- The platform switch alone changes nothing until a store opts in, so say where that stands. -->
          <div class="text-caption q-mt-sm" :class="pickupStoreCount ? 'text-grey-7' : 'text-orange-9'">
            <q-icon :name="pickupStoreCount ? 'o_store' : 'o_warning'" size="14px" class="q-mr-xs" />
            <template v-if="pickupStoreCount">
              {{ pickupStoreCount }} {{ pickupStoreCount === 1 ? 'store offers' : 'stores offer' }} collection.
            </template>
            <template v-else>
              No store offers collection yet — turn on <strong>Pickup in store</strong> on a store first.
            </template>
          </div>
          <q-btn
            flat dense no-caps size="sm" color="primary" icon="o_store"
            label="Manage stores" class="q-mt-xs" @click="openStores"
          />
        </AppSection>
      </div>
    </div>
  </div>
</template>

<script setup>
/* Shipping configuration, grouped by category: what we offer on the left (the rate sources), and the
 * switches governing how that offer behaves on the right.
 *
 * Everything here is platform-wide. The fulfilling store narrows it further — Pickup has its own per-store
 * toggle, and checkout requires both layers to agree.
 *
 * AUTO-SAVES every change (debounced) — no explicit Save button, matching the admin detail-page standard.
 * useDetailForm is route-bound (create-vs-manage off a route pair, api.get(id)); this is a singleton on a
 * tab panel, so it follows the same pattern as the Branding page instead. */
import { ref, reactive, computed, watch, nextTick, onMounted, onBeforeUnmount } from 'vue'
import { useRouter } from 'vue-router'
import { debounce } from 'quasar'
import { getApiErrorMessage } from 'services/api'
import { usePermissions } from 'composables/usePermissions'
import { useNotify } from 'composables/useNotify'
import {
  shippingConfigApi, shippingCarrierMeta, shippingSelectionModeOptions, shippingMethodsBlockedReason
} from 'modules/shipping/api'
import AppSection from 'components/common/AppSection.vue'
import AppFieldLabel from 'components/common/AppFieldLabel.vue'
import AppTextField from 'components/common/AppTextField.vue'

const router = useRouter()
const notify = useNotify()
const { has } = usePermissions()
const canWrite = computed(() => has('Stores.Write'))

const loading = ref(false)
const saving = ref(0)
const saveError = ref(false)
const savedOnce = ref(false)
let hydrating = false
let lastSnap = ''

const config = reactive({
  isEnabled: true,
  selectionMode: 'Manual',
  costVsSpeedWeight: 50,
  assumedTransitDays: 7,
  pickupEnabled: true,
  carriers: []
})

// Server-computed, never posted back — how many stores actually opted in to collection.
const pickupStoreCount = ref(0)

const saveStatus = computed(() => {
  if (!canWrite.value) return null
  if (saving.value > 0) return { label: 'Saving…', icon: 'o_sync', chip: 'blue-1', text: 'primary', spin: true }
  if (saveError.value) return { label: 'Couldn’t save — retry', icon: 'o_cloud_off', chip: 'red-1', text: 'negative' }
  if (savedOnce.value) return { label: 'All changes saved', icon: 'o_cloud_done', chip: 'green-1', text: 'positive' }
  return { label: 'Auto-save on', icon: 'o_cloud_queue', chip: 'grey-3', text: 'grey-8' }
})

const selectionModeOptions = shippingSelectionModeOptions
const isAutomatic = computed(() => config.selectionMode === 'Automatic')

/* The slider is a single cost-vs-speed weight; name the extremes so it reads as a balance, not a number. */
const balanceLabel = computed(() => {
  const w = config.costVsSpeedWeight
  if (w >= 90) return 'Cheapest'
  if (w >= 65) return 'Mostly cost'
  if (w > 35) return 'Balanced'
  if (w > 10) return 'Mostly speed'
  return 'Fastest'
})

// The parent tab page hides its toolbar here, but still binds `search` to whichever panel is active;
// declaring it keeps it off the root element. Nothing to search, add or filter on this tab.
defineProps({ search: { type: String, default: '' } })
// The status chip belongs beside Back in the page header, which the parent owns — so publish it upward
// rather than rendering a second one inside the panel.
const emit = defineEmits(['save-status', 'methods-blocked'])
watch(saveStatus, (status) => emit('save-status', status), { immediate: true })

/* The master switch and the Manual source between them decide whether custom methods can quote at all, and
 * the tab page greys out its Methods tab while they can't — it owns the tabs, so publish the reason upward
 * as it changes rather than reaching across for it. */
const methodsBlocked = computed(() => shippingMethodsBlockedReason(config))
watch(methodsBlocked, (reason) => emit('methods-blocked', reason), { immediate: true })

function noop () {}
defineExpose({ onAdd: noop, openFilters: noop })

function metaFor (carrier) {
  return shippingCarrierMeta[carrier] || { icon: 'o_local_shipping', section: null, hint: '' }
}

/* True when this source needs credentials and has no active credential row. */
function needsCredentials (carrier) {
  return carrier.requiresCredentials && !carrier.hasCredentials
}

/* A carrier with no credentials cannot be switched on — it would quote nothing and look identical to a
 * disabled one. It can always be switched off though: credentials can be removed or deactivated after the
 * fact, and a flat disable would strand the carrier on with no way back. */
function toggleDisabled (carrier) {
  if (!canWrite.value || !config.isEnabled) return true
  return needsCredentials(carrier) && !carrier.isEnabled
}

function toggleReason (carrier) {
  if (!canWrite.value) return 'You do not have permission to change this'
  if (!config.isEnabled) return 'Shipping calculation is off'
  if (needsCredentials(carrier)) {
    return carrier.isEnabled
      ? `${carrier.displayName} has no active credentials and is quoting nothing — add credentials or switch it off`
      : `Add active ${carrier.displayName} credentials before enabling it`
  }
  return ''
}

async function openCredentials (section) {
  if (!section) return
  await flushSave()
  router.push({ name: 'integrations', query: { section } })
}

async function openStores () {
  await flushSave()
  router.push({ name: 'stores' })
}

function buildPayload () {
  return {
    isEnabled: config.isEnabled,
    selectionMode: config.selectionMode,
    costVsSpeedWeight: Number(config.costVsSpeedWeight) || 0,
    // Guard the server's 1..365 rule: a blank field would otherwise post 0 and be rejected on every
    // keystroke while the admin is mid-edit.
    assumedTransitDays: Number(config.assumedTransitDays) || 7,
    pickupEnabled: config.pickupEnabled,
    carriers: config.carriers.map((c) => ({ carrier: c.carrier, isEnabled: c.isEnabled, displayOrder: c.displayOrder }))
  }
}

function snapshot () { return JSON.stringify(buildPayload()) }

/* Hydrate from the server without tripping the auto-save watch, and baseline the snapshot so the initial
 * load never counts as a change. */
function apply (c) {
  hydrating = true
  Object.assign(config, {
    isEnabled: c.isEnabled,
    selectionMode: c.selectionMode,
    costVsSpeedWeight: c.costVsSpeedWeight,
    assumedTransitDays: c.assumedTransitDays,
    pickupEnabled: c.pickupEnabled !== false,
    carriers: c.carriers || []
  })
  pickupStoreCount.value = c.pickupStoreCount || 0
  lastSnap = snapshot()
  nextTick(() => { hydrating = false })
}

async function load () {
  loading.value = true
  try {
    const c = await shippingConfigApi.get()
    if (c) apply(c)
  } catch (err) { notify.error(getApiErrorMessage(err)) } finally { loading.value = false }
}

// Debounced auto-save: skip if unchanged, then PUT. The response is deliberately not applied back over
// the form — a toggle flipped while the request is in flight would be silently reverted by it, and
// nothing this save changes (credential state, carrier list) can differ from what we already hold.
async function saveNow () {
  if (!canWrite.value) return
  const snap = snapshot()
  if (snap === lastSnap) return
  saving.value++
  saveError.value = false
  try {
    await shippingConfigApi.update(buildPayload())
    lastSnap = snap
    savedOnce.value = true
  } catch (err) {
    saveError.value = true
    notify.error(getApiErrorMessage(err))
  } finally {
    saving.value--
  }
}

const saveCore = debounce(saveNow, 800)

/* Runs a pending save immediately. Navigating away inside the debounce window would otherwise drop the
 * change with no Save button for the admin to have noticed they missed. */
async function flushSave () {
  saveCore.cancel()
  await saveNow()
}

// Auto-save on any change (suppressed while hydrating server data).
watch(config, () => { if (!hydrating) saveCore() }, { deep: true })

onBeforeUnmount(flushSave)

onMounted(load)
</script>
