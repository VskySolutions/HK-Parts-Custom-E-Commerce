<template>
  <div class="relative-position">
    <q-inner-loading :showing="loading" />

    <div class="row q-col-gutter-md">
      <!-- ============ LEFT: add-ons the buyer can choose ============ -->
      <div class="col-12 col-md-7">
        <AppSection title="Signature on delivery">
          <q-toggle v-model="rules.signatureEnabled" label="Offer “Require signature” at checkout" color="primary" :disable="!canWrite" />
          <AppTextField
            v-model="rules.signatureFee"
            class="q-mt-sm"
            label="Signature fee"
            type="number"
            placeholder="e.g. 5.00"
            :disable="!canWrite || !rules.signatureEnabled"
          />
        </AppSection>

        <AppSection title="Shipping insurance" class="q-mt-md">
          <q-toggle v-model="rules.insuranceEnabled" label="Offer shipping insurance at checkout" color="primary" :disable="!canWrite" />
          <div class="text-caption text-grey-7 q-mt-sm q-mb-sm">
            Priced on the declared value (the shipped items’ total). The checkout shows and charges this one table.
          </div>

          <q-markup-table flat dense bordered>
            <thead>
              <tr>
                <th class="text-left">Declared value up to</th>
                <th class="text-left">Fee</th>
                <th />
              </tr>
            </thead>
            <tbody>
              <tr v-for="(band, i) in rules.insuranceBands" :key="i">
                <td><q-input v-model.number="band.upTo" type="number" dense borderless :disable="!canWrite || !rules.insuranceEnabled" /></td>
                <td><q-input v-model.number="band.fee" type="number" dense borderless :disable="!canWrite || !rules.insuranceEnabled" /></td>
                <td class="text-right">
                  <q-btn
                    flat round dense size="sm" icon="o_delete" color="negative"
                    :disable="!canWrite || !rules.insuranceEnabled || rules.insuranceBands.length <= 1"
                    aria-label="Remove band"
                    @click="rules.insuranceBands.splice(i, 1)"
                  />
                </td>
              </tr>
            </tbody>
          </q-markup-table>
          <q-btn
            flat dense no-caps size="sm" color="primary" icon="o_add" label="Add band" class="q-mt-xs"
            :disable="!canWrite || !rules.insuranceEnabled"
            @click="addBand"
          />
          <AppTextField
            v-model="rules.insuranceExcessFeePer100"
            class="q-mt-sm"
            :label="`Above ${formatAmount(lastBandLimit)}: extra fee per $100 or part of $100`"
            type="number"
            placeholder="e.g. 1.90"
            :disable="!canWrite || !rules.insuranceEnabled"
          />
        </AppSection>
      </div>

      <!-- ============ RIGHT: where we ship ============ -->
      <div class="col-12 col-md-5">
        <AppSection title="Destination restrictions">
          <div class="text-caption text-grey-7 q-mb-sm">
            Checked when shipping is calculated and again just before payment, so a changed address can’t slip through.
          </div>

          <q-toggle v-model="rules.stateRestrictionEnabled" label="Restrict state-restricted products" color="primary" :disable="!canWrite" />
          <div class="text-caption text-grey-7 q-mb-sm">
            Products with “Restrict Non High Capacity Mags States” = Yes can’t ship to these states, unless the
            signed-in customer is marked exempt on their customer page.
          </div>
          <q-select
            v-model="rules.restrictedStates"
            label="Restricted states"
            outlined dense multiple use-chips use-input new-value-mode="add-unique" hide-dropdown-icon
            input-debounce="0"
            :disable="!canWrite || !rules.stateRestrictionEnabled"
            hint="Type a two-letter code and press Enter"
            @new-value="(v, done) => done(String(v).trim().toUpperCase(), 'add-unique')"
          />

          <q-separator class="q-my-md" />

          <q-toggle v-model="rules.internationalRestrictionEnabled" label="Keep domestic-only products in the US" color="primary" :disable="!canWrite" />
          <div class="text-caption text-grey-7">
            A non-US address is refused when a cart product has “Allow International Shipping” = No.
          </div>

          <q-select
            v-model="rules.allowedCountryCodes"
            class="q-mt-md"
            label="Ship only to these countries"
            outlined dense multiple use-chips use-input new-value-mode="add-unique" hide-dropdown-icon
            input-debounce="0"
            :disable="!canWrite"
            hint="Two-letter codes, e.g. US. Leave empty to allow every country."
            @new-value="(v, done) => done(String(v).trim().toUpperCase(), 'add-unique')"
          />

          <q-separator class="q-my-md" />

          <q-toggle v-model="rules.refusePoBoxes" label="Refuse P.O. box addresses" color="primary" :disable="!canWrite" />
          <AppTextField
            v-model="rules.poBoxMessage"
            class="q-mt-sm"
            label="P.O. box message"
            placeholder="We cannot ship to a P.O. box. Please enter a street address."
            :disable="!canWrite || !rules.refusePoBoxes"
          />

          <AppTextField
            v-model="rules.invalidDestinationMessage"
            class="q-mt-sm"
            label="Refused destination message (optional)"
            placeholder="Leave blank to explain which rule refused the address"
            :disable="!canWrite"
          />
        </AppSection>

        <div class="text-caption text-grey-6 q-mt-sm">
          <q-icon name="o_history" size="14px" class="q-mr-xs" />
          Rules version {{ version }}. Each saved change starts a new version; placed orders keep the fees they were charged.
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
/* Shipping rules (AD-20/21): checkout add-on fees (signature, insurance bands) and destination restrictions.
 * AUTO-SAVES each change (debounced), like the Configuration panel next to it. The server bumps the rules
 * version on every save and stamps it on each new order's shipping row. */
import { ref, reactive, computed, watch, nextTick, onMounted, onBeforeUnmount } from 'vue'
import { debounce } from 'quasar'
import { getApiErrorMessage } from 'services/api'
import { usePermissions } from 'composables/usePermissions'
import { useNotify } from 'composables/useNotify'
import { shippingRulesApi } from 'modules/shipping/api'
import AppSection from 'components/common/AppSection.vue'
import AppTextField from 'components/common/AppTextField.vue'

const notify = useNotify()
const { has } = usePermissions()
const canWrite = computed(() => has('Stores.Write'))

defineProps({ search: { type: String, default: '' } })
const emit = defineEmits(['save-status'])
function noop () {}
defineExpose({ onAdd: noop, openFilters: noop })

const loading = ref(false)
const saving = ref(0)
const saveError = ref(false)
const savedOnce = ref(false)
const version = ref(1)
let hydrating = false
let lastSnap = ''

const rules = reactive({
  signatureEnabled: true,
  signatureFee: 5,
  insuranceEnabled: true,
  insuranceBands: [],
  insuranceExcessFeePer100: 1.9,
  stateRestrictionEnabled: true,
  restrictedStates: [],
  internationalRestrictionEnabled: true,
  allowedCountryCodes: [],
  refusePoBoxes: false,
  poBoxMessage: '',
  invalidDestinationMessage: ''
})

const saveStatus = computed(() => {
  if (!canWrite.value) return null
  if (saving.value > 0) return { label: 'Saving…', icon: 'o_sync', chip: 'blue-1', text: 'primary', spin: true }
  if (saveError.value) return { label: 'Couldn’t save — retry', icon: 'o_cloud_off', chip: 'red-1', text: 'negative' }
  if (savedOnce.value) return { label: 'All changes saved', icon: 'o_cloud_done', chip: 'green-1', text: 'positive' }
  return { label: 'Auto-save on', icon: 'o_cloud_queue', chip: 'grey-3', text: 'grey-8' }
})
watch(saveStatus, (status) => emit('save-status', status), { immediate: true })

const lastBandLimit = computed(() => {
  const limits = rules.insuranceBands.map((b) => Number(b.upTo) || 0)
  return limits.length ? Math.max(...limits) : 0
})

function formatAmount (v) { return `$${Number(v || 0).toFixed(2)}` }

function addBand () {
  const last = lastBandLimit.value
  const lastFee = rules.insuranceBands.length ? Number(rules.insuranceBands[rules.insuranceBands.length - 1].fee) || 0 : 0
  rules.insuranceBands.push({ upTo: last + 100, fee: lastFee })
}

function buildPayload () {
  return {
    signatureEnabled: rules.signatureEnabled,
    signatureFee: Number(rules.signatureFee) || 0,
    insuranceEnabled: rules.insuranceEnabled,
    // Drop half-typed rows (blank or zero limit) rather than posting a band the server rejects on every keystroke.
    insuranceBands: rules.insuranceBands
      .map((b) => ({ upTo: Number(b.upTo) || 0, fee: Number(b.fee) || 0 }))
      .filter((b) => b.upTo > 0),
    insuranceExcessFeePer100: Number(rules.insuranceExcessFeePer100) || 0,
    stateRestrictionEnabled: rules.stateRestrictionEnabled,
    restrictedStates: rules.restrictedStates,
    internationalRestrictionEnabled: rules.internationalRestrictionEnabled,
    allowedCountryCodes: rules.allowedCountryCodes,
    refusePoBoxes: rules.refusePoBoxes,
    poBoxMessage: rules.poBoxMessage || null,
    invalidDestinationMessage: rules.invalidDestinationMessage || null
  }
}

function snapshot () { return JSON.stringify(buildPayload()) }

function apply (r) {
  hydrating = true
  Object.assign(rules, {
    signatureEnabled: r.signatureEnabled,
    signatureFee: r.signatureFee,
    insuranceEnabled: r.insuranceEnabled,
    insuranceBands: (r.insuranceBands || []).map((b) => ({ upTo: b.upTo, fee: b.fee })),
    insuranceExcessFeePer100: r.insuranceExcessFeePer100,
    stateRestrictionEnabled: r.stateRestrictionEnabled,
    restrictedStates: r.restrictedStates || [],
    internationalRestrictionEnabled: r.internationalRestrictionEnabled,
    allowedCountryCodes: r.allowedCountryCodes || [],
    refusePoBoxes: r.refusePoBoxes,
    poBoxMessage: r.poBoxMessage || '',
    invalidDestinationMessage: r.invalidDestinationMessage || ''
  })
  version.value = r.version || 1
  lastSnap = snapshot()
  nextTick(() => { hydrating = false })
}

async function load () {
  loading.value = true
  try {
    const r = await shippingRulesApi.get()
    if (r) apply(r)
  } catch (err) { notify.error(getApiErrorMessage(err)) } finally { loading.value = false }
}

async function saveNow () {
  if (!canWrite.value) return
  const snap = snapshot()
  if (snap === lastSnap) return
  saving.value++
  saveError.value = false
  try {
    const saved = await shippingRulesApi.update(buildPayload())
    lastSnap = snap
    savedOnce.value = true
    if (saved?.version) version.value = saved.version
  } catch (err) {
    saveError.value = true
    notify.error(getApiErrorMessage(err))
  } finally {
    saving.value--
  }
}

const saveCore = debounce(saveNow, 800)
async function flushSave () {
  saveCore.cancel()
  await saveNow()
}

watch(rules, () => { if (!hydrating) saveCore() }, { deep: true })
onBeforeUnmount(flushSave)
onMounted(load)
</script>
