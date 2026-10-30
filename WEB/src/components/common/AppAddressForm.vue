<template>
  <div class="app-address-form column">
    <div v-if="showNames" class="row q-col-gutter-sm">
      <div class="col-12 col-sm-6">
        <AppTextField label="First name" :required="required" :disable="disable" :model-value="value.firstName" placeholder="e.g. John" @update:model-value="update('firstName', $event)" />
      </div>
      <div class="col-12 col-sm-6">
        <AppTextField label="Last name" :required="required" :disable="disable" :model-value="value.lastName" placeholder="e.g. Doe" @update:model-value="update('lastName', $event)" />
      </div>
    </div>

    <AppTextField v-if="showCompany" label="Company" :disable="disable" :model-value="value.company" placeholder="Company name (optional)" @update:model-value="update('company', $event)" />

    <AppSelect
      label="Country"
      :required="required"
      :disable="disable"
      :model-value="value.countryCode"
      :options="countryFiltered"
      use-input
      fill-input
      hide-selected
      input-debounce="0"
      lazy-rules
      :rules="requiredRules('Country')"
      placeholder="Select a country"
      @filter="filterCountries"
      @update:model-value="onCountry"
    />

    <div class="row q-col-gutter-x-md">
      <div class="col-12 col-sm-6">
        <AppSelect
          label="State / Region"
          :required="required"
          :model-value="stateModel"
          :options="stateFiltered"
          :disable="disable || !value.countryCode"
          use-input
          fill-input
          hide-selected
          input-debounce="0"
          new-value-mode="add-unique"
          lazy-rules
          :rules="requiredRules('State / Region')"
          placeholder="Select or type"
          @filter="filterStates"
          @update:model-value="onState"
        />
      </div>
      <div class="col-12 col-sm-6">
        <AppSelect
          label="City"
          :model-value="value.city"
          :options="cityFiltered"
          :disable="disable || !value.countryCode"
          use-input
          fill-input
          hide-selected
          input-debounce="0"
          new-value-mode="add-unique"
          placeholder="Select or type"
          @filter="filterCities"
          @update:model-value="update('city', $event)"
        />
      </div>
    </div>

    <AppTextField label="Address line 1" :required="required" :disable="disable" :model-value="value.addressLine1" placeholder="House / flat no., building, street" lazy-rules :rules="requiredRules('Address line 1')" @update:model-value="update('addressLine1', $event)" />
    <AppTextField label="Address line 2" :disable="disable" :model-value="value.addressLine2" placeholder="Area, colony (optional)" @update:model-value="update('addressLine2', $event)" />
    <AppTextField label="Landmark" :disable="disable" :model-value="value.landmark" placeholder="Nearby landmark, e.g. opposite City Mall (optional)" @update:model-value="update('landmark', $event)" />

    <div class="row q-col-gutter-x-md">
      <div class="col-12 col-sm-6">
        <AppTextField
          :label="postal.label"
          :required="required"
          :disable="disable"
          :model-value="value.postalCode"
          :placeholder="postal.example ? `e.g. ${postal.example}` : 'Postal code'"
          lazy-rules
          :rules="postalRules"
          @update:model-value="update('postalCode', $event)"
        />
      </div>
      <div v-if="showPhone" class="col-12 col-sm-6">
        <AppPhoneInput label="Phone" :required="required" :disable="disable" :model-value="value.phoneNumber" :default-country="value.countryCode || 'US'" lazy-rules :rules="requiredRules('Phone number')" @update:model-value="update('phoneNumber', $event)" />
      </div>
    </div>
  </div>
</template>

<script setup>
/*
 * AppAddressForm: the ONE standard address form for the whole app (customer address book, checkout,
 * store, …). Backed by useAddress(): dependent Country -> State -> City pickers (US/India pinned to
 * the top), a per-country postal label/placeholder/validation, a Landmark field, and an integrated
 * phone input whose country follows the selected country. v-model is the canonical address object
 * (see composables/useAddress.emptyAddress).
 */
import { ref, computed, watch } from 'vue'
import { useAddress, emptyAddress } from 'composables/useAddress'
import AppSelect from './AppSelect.vue'
import AppTextField from './AppTextField.vue'
import AppPhoneInput from './AppPhoneInput.vue'

const props = defineProps({
  modelValue: { type: Object, default: () => ({}) },
  required: { type: Boolean, default: false },
  showNames: { type: Boolean, default: true },
  showCompany: { type: Boolean, default: true },
  showPhone: { type: Boolean, default: true },
  disable: { type: Boolean, default: false }
})
const emit = defineEmits(['update:modelValue'])

const { countryOptions, statesFor, citiesFor, postalMeta, postalValid } = useAddress()

const value = computed(() => ({ ...emptyAddress(), ...(props.modelValue || {}) }))

// Known states/cities for the current country (cascade uses the state ISO derived from the name).
const stateList = computed(() => statesFor(value.value.countryCode))
const stateCode = computed(() => {
  const found = stateList.value.find((o) => o.label === value.value.stateProvince)
  return found ? found.value : ''
})
const cityList = computed(() => citiesFor(value.value.countryCode, stateCode.value))
// The state select shows the ISO for a known state, or the raw typed value for a custom one.
const stateModel = computed(() => stateCode.value || value.value.stateProvince || null)

const countryFiltered = ref(countryOptions)
const stateFiltered = ref([])
const cityFiltered = ref([])

function makeFilter (getSource, target) {
  return (needle, doneFn) => {
    doneFn(() => {
      const q = (needle || '').toLowerCase()
      const src = getSource()
      target.value = q ? src.filter((o) => o.label.toLowerCase().includes(q)) : src
    })
  }
}
const filterCountries = makeFilter(() => countryOptions, countryFiltered)
const filterStates = makeFilter(() => stateList.value, stateFiltered)
const filterCities = makeFilter(() => cityList.value, cityFiltered)

// Keep the option lists in sync with the current country/state so a selected value can always be
// resolved to its label (fill-input needs the option present, and the lists start empty otherwise).
watch(stateList, (v) => { stateFiltered.value = v }, { immediate: true })
watch(cityList, (v) => { cityFiltered.value = v }, { immediate: true })

const postal = computed(() => postalMeta(value.value.countryCode))

// Quasar `:rules` so the required address fields (Country, State, Address line 1, Postal) participate in
// the enclosing QForm's validation and in an explicit formRef.validate() — enforced only when `required`.
function requiredRules (label) {
  return props.required ? [(v) => (!!v && String(v).trim().length > 0) || `${label} is required`] : []
}
const postalRules = computed(() => {
  const rules = requiredRules(postal.value.label)
  // Format check (only when a value is present) using the per-country postal validator.
  rules.push((v) => !v || postalValid(value.value.countryCode, v) ||
    `Enter a valid ${postal.value.label}${postal.value.example ? ` (e.g. ${postal.value.example})` : ''}`)
  return rules
})

function patch (changes) {
  emit('update:modelValue', { ...value.value, ...changes })
}
function update (key, val) {
  patch({ [key]: val ?? '' })
}
function onCountry (val) {
  patch({ countryCode: val ?? '', stateProvince: '', city: '' })
}
function onState (val) {
  if (!val) { patch({ stateProvince: '', city: '' }); return }
  const opt = stateList.value.find((o) => o.value === val)
  patch({ stateProvince: opt ? opt.label : String(val), city: '' })
}
</script>
