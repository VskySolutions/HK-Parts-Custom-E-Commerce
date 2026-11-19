<template>
  <!-- Select New Shipping Dealer (FFL-92/93): search the directory, pick a dealer, and re-point the order's
       FFL shipment at it. The server reads the dealer from the directory and refuses once the order shipped. -->
  <q-dialog :model-value="modelValue" @update:model-value="$emit('update:modelValue', $event)">
    <q-card style="width: 640px; max-width: 95vw">
      <q-card-section class="text-h6">New Shipping Dealer for order {{ orderNumber }}</q-card-section>
      <q-separator />
      <q-card-section class="column q-gutter-md">
        <div v-if="current" class="text-body2">
          <div class="text-caption text-grey-7">Current ship-to</div>
          <div class="text-weight-medium">{{ current.fflDealerName || '—' }}</div>
          <div v-if="current.fflDealerStreet">{{ current.fflDealerStreet }}, {{ current.fflDealerCity }}, {{ current.fflDealerState }} {{ current.fflDealerZip }}</div>
          <div v-if="current.fflNumber" class="text-caption text-grey-7">FFL {{ current.fflNumber }}</div>
        </div>

        <div class="row q-col-gutter-sm items-start">
          <div class="col-12 col-sm-5">
            <AppTextField v-model="zip" label="Zip Code" maxlength="15" @keydown.enter.prevent="search" />
          </div>
          <div class="col-7 col-sm-4">
            <AppSelect v-model="radius" label="Search Radius" :options="radiusOptions" @update:model-value="search" />
          </div>
          <div class="col-5 col-sm-3">
            <q-btn unelevated no-caps color="primary" class="full-width" style="min-height: 40px" label="Search Dealers" :loading="searching" @click="search" />
          </div>
        </div>

        <div v-if="searchError" class="text-body2 text-negative">{{ searchError }}</div>
        <div v-else-if="message" class="text-body2 text-grey-7">{{ message }}</div>

        <AppSelect
          v-model="selectedKey"
          :label="`Select Dealer (${dealers.length} found)`"
          :options="dealerOptions"
          :disable="!dealers.length"
        />

        <div v-if="selected" class="text-body2">
          <div class="text-weight-bold">{{ selected.name }}</div>
          <div>{{ selected.street }}</div>
          <div>{{ selected.city }}, {{ selected.state }} {{ selected.zip }}</div>
          <div v-if="selected.phoneDisplay">{{ selected.phoneDisplay }}</div>
          <div class="text-caption text-grey-7">FFL {{ selected.licenseNumber }}<span v-if="selected.licenseExpires"> · expires {{ selected.licenseExpires }}</span></div>
          <a :href="selected.mapUrl" target="_blank" rel="noopener noreferrer" class="text-primary">View Map</a>
        </div>
      </q-card-section>
      <q-separator />
      <q-card-actions align="right">
        <q-btn flat no-caps label="Cancel" @click="$emit('update:modelValue', false)" />
        <q-btn unelevated no-caps color="primary" label="Save dealer" :loading="saving" :disable="!selectedKey" @click="save" />
      </q-card-actions>
    </q-card>
  </q-dialog>
</template>

<script setup>
import { ref, computed, watch } from 'vue'
import { fflAdminApi } from 'modules/shipping/ffl-api'
import { getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'

const props = defineProps({
  modelValue: { type: Boolean, default: false },
  orderId: { type: String, default: null },
  orderNumber: { type: String, default: '' },
  // Pre-fills the ZIP (the order's address ZIP, FFL-92).
  defaultZip: { type: String, default: '' },
  // The order's current FFL shipping row (OrderShippingDto).
  current: { type: Object, default: null }
})
const emit = defineEmits(['update:modelValue', 'saved'])
const notify = useNotify()

const radiusOptions = [15, 25, 50, 100, 150].map((r) => ({ label: `${r} miles`, value: r }))
const zip = ref('')
const radius = ref(15)
const dealers = ref([])
const selectedKey = ref(null)
const searching = ref(false)
const saving = ref(false)
const searchError = ref('')
const message = ref('')

const dealerOptions = computed(() => dealers.value.map((d) => ({
  label: `${d.name} (${Math.round(d.distanceMiles ?? 0)} mi, ${d.city} ${d.state})`,
  value: d.key
})))
const selected = computed(() => dealers.value.find((d) => d.key === selectedKey.value) || null)

watch(() => props.modelValue, (open) => {
  if (!open) return
  zip.value = (props.defaultZip || '').trim().slice(0, 5)
  radius.value = 15
  dealers.value = []
  selectedKey.value = null
  searchError.value = ''
  message.value = ''
  if (/^[0-9]{5}$/.test(zip.value)) search()
})

async function search () {
  searchError.value = ''
  message.value = ''
  const z = (zip.value || '').trim().slice(0, 5)
  if (!/^[0-9]{5}$/.test(z)) {
    searchError.value = 'Please enter a valid 5-digit ZIP code.'
    return
  }
  searching.value = true
  try {
    const r = await fflAdminApi.search(z, radius.value)
    dealers.value = r.dealers || []
    message.value = r.message || ''
    if (!dealers.value.some((d) => d.key === selectedKey.value)) selectedKey.value = null
  } catch (err) {
    dealers.value = []
    searchError.value = getApiErrorMessage(err)
  } finally {
    searching.value = false
  }
}

async function save () {
  if (!selectedKey.value || !props.orderId) return
  saving.value = true
  try {
    const r = await fflAdminApi.changeOrderDealer(props.orderId, selectedKey.value)
    notify.success(r?.message || 'Shipping address updated. Please verify FFL details.')
    emit('saved', r)
    emit('update:modelValue', false)
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    saving.value = false
  }
}
</script>
