<template>
  <div class="app-address-fields column q-gutter-sm">
    <AppSelect
      :model-value="value.country"
      label="Country"
      :required="required"
      :options="countryOptions"
      use-input
      clearable
      input-debounce="0"
      @filter="filterCountries"
      @update:model-value="onCountry"
    />

    <div class="row q-col-gutter-sm">
      <div class="col-12 col-sm-6">
        <AppSelect
          :model-value="value.state"
          label="State / Region"
          :options="stateOptions"
          :disable="!value.country"
          use-input
          clearable
          input-debounce="0"
          @filter="filterStates"
          @update:model-value="onState"
        />
      </div>
      <div class="col-12 col-sm-6">
        <AppSelect
          :model-value="value.city"
          label="City"
          :options="cityOptions"
          :disable="!value.state"
          use-input
          clearable
          input-debounce="0"
          @filter="filterCities"
          @update:model-value="update('city', $event)"
        />
      </div>
    </div>

    <AppTextField
      :model-value="value.line1"
      label="Address line 1"
      :required="required"
      @update:model-value="update('line1', $event)"
    />
    <AppTextField
      :model-value="value.line2"
      label="Address line 2"
      @update:model-value="update('line2', $event)"
    />
    <div class="row q-col-gutter-sm">
      <div class="col-12 col-sm-6">
        <AppTextField
          :model-value="value.postalCode"
          label="Postal code"
          @update:model-value="update('postalCode', $event)"
        />
      </div>
    </div>
  </div>
</template>

<script setup>
/*
 * AppAddressFields (WO-94 Step 10): country -> state -> city cascading selects
 * (country-state-city) plus street lines and postal code. v-model is a single
 * address object.
 */
import { ref, computed } from 'vue'
import { Country, State, City } from 'country-state-city'
import AppSelect from './AppSelect.vue'
import AppTextField from './AppTextField.vue'

const props = defineProps({
  modelValue: { type: Object, default: () => ({}) },
  required: { type: Boolean, default: false }
})

const emit = defineEmits(['update:modelValue'])

const value = computed(() => ({
  country: '',
  state: '',
  city: '',
  line1: '',
  line2: '',
  postalCode: '',
  ...(props.modelValue || {})
}))

function toOptions (list, valueKey, labelKey) {
  return list.map((item) => ({ label: item[labelKey], value: item[valueKey] }))
}

const allCountries = toOptions(Country.getAllCountries(), 'isoCode', 'name')
const countryOptions = ref(allCountries)

const allStates = computed(() =>
  value.value.country
    ? toOptions(State.getStatesOfCountry(value.value.country), 'isoCode', 'name')
    : []
)
const stateOptions = ref([])

const allCities = computed(() =>
  value.value.country && value.value.state
    ? toOptions(City.getCitiesOfState(value.value.country, value.value.state), 'name', 'name')
    : []
)
const cityOptions = ref([])

// q-select @filter handlers (client-side filtering over the full lists).
function makeFilter (source, target) {
  return (needle, doneFn) => {
    doneFn(() => {
      const q = (needle || '').toLowerCase()
      target.value = q
        ? source.value.filter((o) => o.label.toLowerCase().includes(q))
        : source.value
    })
  }
}
const filterCountries = makeFilter(computed(() => allCountries), countryOptions)
const filterStates = makeFilter(allStates, stateOptions)
const filterCities = makeFilter(allCities, cityOptions)

function update (key, val) {
  emit('update:modelValue', { ...value.value, [key]: val ?? '' })
}

function onCountry (val) {
  emit('update:modelValue', { ...value.value, country: val ?? '', state: '', city: '' })
}

function onState (val) {
  emit('update:modelValue', { ...value.value, state: val ?? '', city: '' })
}
</script>
