<template>
  <!-- FFL Shipping Information (docs/ffl-shipping-information-requirements.md §4). Rendered only when the cart
       holds FFL-required items; every dealer field is rendered as text (never v-html), FFL-44. -->
  <q-card v-if="state && state.enabled && state.hasFflItems" ref="rootRef" flat bordered class="q-mb-lg sf-ffl">
    <q-card-section>
      <div class="text-subtitle1 text-weight-bold q-mb-xs row items-center no-wrap">
        <span v-if="step" class="sf-step" :class="{ 'sf-step--done': stepDone }">
          <q-icon v-if="stepDone" name="o_check" size="14px" />
          <template v-else>{{ step }}</template>
        </span>
        FFL Shipping Information
      </div>
      <div class="text-body2 text-grey-8 q-mb-md" style="white-space: pre-line">{{ state.introText }}</div>

      <q-banner v-if="state.notice" dense class="bg-orange-1 text-orange-10 rounded-borders q-mb-md">
        <template #avatar><q-icon name="o_info" color="orange-10" /></template>
        {{ state.notice }}
      </q-banner>

      <div class="row q-col-gutter-sm items-start">
        <div class="col-12 col-sm-5">
          <q-input
            v-model="zip"
            outlined
            dense
            label="Zip Code"
            maxlength="15"
            inputmode="numeric"
            autocomplete="postal-code"
            :disable="disabled"
            @update:model-value="zipTouched = true"
            @keydown.enter.prevent="search"
          />
        </div>
        <div class="col-7 col-sm-4">
          <q-select
            v-model="radius"
            outlined
            dense
            emit-value
            map-options
            label="Search Radius"
            :options="radiusOptions"
            :disable="disabled"
            @update:model-value="search"
          />
        </div>
        <div class="col-5 col-sm-3">
          <q-btn
            unelevated
            no-caps
            color="primary"
            class="full-width"
            style="min-height: 40px"
            label="Search Dealers"
            icon="o_search"
            :loading="searching"
            :disable="disabled"
            @click="search"
          />
        </div>
      </div>

      <!-- Dealer list, with the selected dealer directly beneath it -->
      <div class="row q-col-gutter-md q-mt-xs">
        <!-- Dealer list -->
        <div class="col-12">
          <div class="text-body2 text-weight-medium q-mb-xs" aria-live="polite">
            Select Dealer ({{ dealers.length }} {{ dealers.length === 1 ? 'dealer' : 'dealers' }} found)
          </div>
          <q-banner v-if="searchError" dense class="bg-red-1 text-red-9 rounded-borders q-mb-sm">
            <template #avatar><q-icon name="o_error" color="red-9" /></template>
            {{ searchError }}
          </q-banner>
          <div v-else-if="searchMessage" class="text-body2 text-grey-7 q-mb-sm">{{ searchMessage }}</div>
          <div v-if="truncated" class="text-caption text-grey-7 q-mb-xs">
            Showing the nearest {{ dealers.length }} of {{ totalFound }} dealers. Try a smaller radius to narrow the list.
          </div>

          <div class="sf-ffl__list rounded-borders" role="listbox" aria-label="FFL dealers">
            <q-inner-loading :showing="searching" label="Loading Dealers..." color="primary" />
            <div
              v-for="d in dealers"
              :key="d.key"
              :ref="(el) => setRowRef(d.key, el)"
              class="sf-ffl__row row no-wrap items-start q-pa-sm"
              :class="{ 'sf-ffl__row--active': selectedKey === d.key }"
              role="option"
              tabindex="0"
              :aria-selected="selectedKey === d.key ? 'true' : 'false'"
              @click="choose(d)"
              @keydown.enter.prevent="choose(d)"
              @keydown.space.prevent="choose(d)"
            >
              <div class="sf-ffl__distance text-body2 text-grey-8">{{ milesText(d.distanceMiles) }}</div>
              <div class="col sf-ffl__info">
                <q-icon v-if="selectedKey === d.key" name="o_check_circle" color="positive" size="16px" class="float-right q-ml-xs" aria-hidden="true" />
                <div class="text-weight-bold">{{ d.name }}</div>
                <div class="text-body2 text-grey-8">{{ d.street }}, {{ d.city }} {{ d.state }}</div>
                <div v-if="d.lastTransferFee != null" class="text-caption text-grey-7">Last transfer fee {{ money(d.lastTransferFee) }}</div>
              </div>
              <a :href="d.mapUrl" target="_blank" rel="noopener noreferrer" class="text-primary text-body2 q-ml-sm" @click.stop>View Map</a>
            </div>
            <div v-if="!searching && !dealers.length && !searchError" class="text-body2 text-grey-6 q-pa-md text-center">
              {{ searched ? 'No dealers to show.' : 'Enter a ZIP code and search to find a dealer.' }}
            </div>
          </div>
        </div>

        <!-- Selected dealer -->
        <div class="col-12 relative-position">
          <div class="text-body2 text-weight-medium q-mb-xs">Your Selected Dealer:</div>
          <q-inner-loading :showing="selecting" color="primary" />
          <div v-if="selectError" class="text-body2 text-negative q-mb-sm">{{ selectError }}</div>
          <div v-if="dealer" class="text-body2">
            <div class="text-weight-bold">{{ dealer.name }}</div>
            <div>{{ dealer.street }}</div>
            <div>{{ dealer.city }}, {{ dealer.state }} {{ dealer.zip }}</div>
            <div v-if="dealer.phoneDisplay">{{ dealer.phoneDisplay }}</div>
            <div class="text-caption text-grey-7">FFL {{ dealer.licenseNumber }}<span v-if="dealer.licenseExpires"> · expires {{ dealer.licenseExpires }}</span></div>
            <a :href="dealer.mapUrl" target="_blank" rel="noopener noreferrer" class="text-primary">View Map</a>
            <div v-if="state.remembered" class="text-caption text-grey-7 q-mt-xs">Pre-selected from your last firearm order.</div>
            <div v-if="state.transferWarning" class="text-negative q-mt-sm" style="white-space: pre-line">{{ state.transferWarning }}</div>
          </div>
          <div v-else class="sf-ffl__empty rounded-borders text-body2">
            <q-icon name="o_touch_app" size="18px" class="q-mr-xs" />
            No dealer selected yet. {{ dealers.length ? 'Click a dealer in the list to choose it.' : 'Search by ZIP code, then click a dealer to choose it.' }}
          </div>
          <div v-if="requiredError && !dealer" class="text-body2 text-negative q-mt-sm">{{ requiredError }}</div>
        </div>
      </div>
    </q-card-section>
  </q-card>
</template>

<script setup>
import { ref, computed, watch, onMounted, nextTick } from 'vue'
import { fflApi } from 'modules/storefront/api'
import { getApiErrorMessage } from 'services/api'

const props = defineProps({
  sessionId: { type: String, default: null },
  // The buyer's address ZIP: pre-fills the search (FFL-22).
  billingZip: { type: String, default: '' },
  disabled: { type: Boolean, default: false },
  // Shown under the selected-dealer block when the server refused placement for want of a dealer.
  requiredError: { type: String, default: '' },
  // Bump to re-read the cart's FFL state (e.g. after the cart changed).
  reloadKey: { type: [Number, String], default: 0 },
  // Checkout step number for the header badge (none = no badge) and whether the step is complete.
  step: { type: Number, default: null },
  stepDone: { type: Boolean, default: false }
})
const emit = defineEmits(['state', 'selected'])

const rootRef = ref(null)
const state = ref(null)
const zip = ref('')
const zipTouched = ref(false)
const radius = ref(null)
const dealers = ref([])
const totalFound = ref(0)
const truncated = ref(false)
const searching = ref(false)
const searched = ref(false)
const searchError = ref('')
const searchMessage = ref('')
const selecting = ref(false)
const selectError = ref('')
const rowRefs = new Map()

const dealer = computed(() => state.value?.dealer || null)
const selectedKey = computed(() => dealer.value?.key || null)
const radiusOptions = computed(() => (state.value?.radii || [15, 25, 50, 100, 150]).map((r) => ({ label: `${r} miles`, value: r })))

function setRowRef (key, el) {
  if (el) rowRefs.set(key, el)
  else rowRefs.delete(key)
}

function milesText (m) {
  if (m == null) return ''
  const n = Math.round(m)
  return `${n} ${n === 1 ? 'mile' : 'miles'}`
}

function money (v) {
  return Number(v).toLocaleString(undefined, { style: 'currency', currency: 'USD' })
}

const zip5 = computed(() => {
  const z = (zip.value || '').trim().slice(0, 5)
  return /^[0-9]{5}$/.test(z) ? z : null
})

async function loadState () {
  if (!props.sessionId) return
  try {
    const s = await fflApi.selection(props.sessionId)
    applyState(s)
    if (s?.enabled && s.hasFflItems) {
      if (radius.value == null) radius.value = (s.radii || [15])[0]
      if (!zip.value && props.billingZip) zip.value = props.billingZip
      // FFL-23: search on load.
      if (zip5.value && !searched.value) search()
    }
  } catch (err) {
    // A missing cart is the cart page's problem; leave the panel hidden.
    state.value = null
  }
}

function applyState (s) {
  state.value = s
  emit('state', s)
}

async function search () {
  searchError.value = ''
  searchMessage.value = ''
  if (!zip5.value) {
    // FFL-31/37: an invalid ZIP shows a message and an empty list.
    dealers.value = []
    totalFound.value = 0
    truncated.value = false
    searchError.value = 'Please enter a valid 5-digit ZIP code.'
    return
  }
  searching.value = true
  try {
    const r = await fflApi.searchCheckout(zip5.value, radius.value)
    dealers.value = r.dealers || []
    totalFound.value = r.totalFound || 0
    truncated.value = !!r.truncated
    searchMessage.value = r.message || ''
    searched.value = true
    // FFL-43: the current selection stays even when it is not in the new list; highlight it when it is.
    await nextTick()
    const el = selectedKey.value && rowRefs.get(selectedKey.value)
    if (el && el.scrollIntoView) el.scrollIntoView({ block: 'nearest' })
  } catch (err) {
    // FFL-37: say so, and reset the count rather than leaving stale rows.
    dealers.value = []
    totalFound.value = 0
    truncated.value = false
    searchError.value = err?.response?.status === 429
      ? getApiErrorMessage(err)
      : (err?.response?.status === 400 ? getApiErrorMessage(err) : "We couldn't search dealers right now. Please try again.")
  } finally {
    searching.value = false
  }
}

async function choose (d) {
  if (props.disabled || selecting.value || selectedKey.value === d.key) return
  selectError.value = ''
  selecting.value = true
  try {
    const s = await fflApi.select(props.sessionId, d.key)
    applyState(s)
    emit('selected', s)
  } catch (err) {
    // FFL-45: one inline error in the selected-dealer block.
    selectError.value = getApiErrorMessage(err) || "We couldn't load that dealer. Please search again or choose another dealer."
  } finally {
    selecting.value = false
  }
}

function scrollIntoView () {
  const el = rootRef.value?.$el
  if (el && el.scrollIntoView) el.scrollIntoView({ behavior: 'smooth', block: 'start' })
}

// Pre-fill from the buyer's ZIP until they type their own.
watch(() => props.billingZip, (v) => {
  if (zipTouched.value || !v) return
  const changed = (zip.value || '') !== v
  zip.value = v
  if (changed && state.value?.hasFflItems && zip5.value && !searched.value) search()
})

watch(() => [props.sessionId, props.reloadKey], () => loadState())

onMounted(loadState)

defineExpose({ reload: loadState, scrollIntoView })
</script>

<style scoped lang="scss">
.sf-ffl__list {
  position: relative;
  max-height: 340px;
  min-height: 80px;
  overflow-y: auto;
  overflow-x: hidden;
  border: 1px solid rgba(0, 0, 0, 0.12);
}
/* Long names / streets wrap inside the row instead of widening the list (no horizontal scrollbar). */
.sf-ffl__info {
  min-width: 0;
  overflow-wrap: anywhere;
}
.sf-ffl__empty {
  display: flex;
  align-items: flex-start;
  padding: 8px 10px;
  background: rgba(242, 140, 40, 0.1);
  color: #8a4b00;
}
.sf-step {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  flex: 0 0 22px;
  width: 22px;
  height: 22px;
  margin-right: 8px;
  border-radius: 50%;
  background: var(--q-primary);
  color: #fff;
  font-size: 12px;
  font-weight: 700;
  line-height: 1;
}
.sf-step--done {
  background: var(--q-positive);
}
.sf-ffl__row {
  cursor: pointer;
  border-bottom: 1px solid rgba(0, 0, 0, 0.06);
  gap: 8px;
  &:last-child { border-bottom: none; }
  &:hover, &:focus-visible { background: rgba(0, 0, 0, 0.04); outline: none; }
}
.sf-ffl__row--active,
.sf-ffl__row--active:hover {
  background: rgba(0, 0, 0, 0.1);
}
.sf-ffl__distance {
  width: 64px;
  flex: 0 0 64px;
}
body.body--dark {
  .sf-ffl__list { border-color: rgba(255, 255, 255, 0.2); }
  .sf-ffl__row { border-bottom-color: rgba(255, 255, 255, 0.08); }
  .sf-ffl__row--active { background: rgba(255, 255, 255, 0.12); }
  .sf-ffl__empty { background: rgba(242, 140, 40, 0.16); color: #ffcc80; }
}
@media (max-width: 599px) {
  .sf-ffl__distance { width: 52px; flex-basis: 52px; }
}
</style>
