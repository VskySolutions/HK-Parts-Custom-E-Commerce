<template>
  <div class="app-field app-phone">
    <AppFieldLabel v-if="label" :label="label" :required="required" />

    <q-input
      dense
      outlined
      :model-value="national"
      :error="hasError"
      :error-message="errorMessage"
      :placeholder="placeholder"
      hide-bottom-space
      inputmode="tel"
      autocomplete="tel"
      v-bind="$attrs"
      @update:model-value="onInput"
      @blur="onBlur"
    >
      <template #prepend>
        <q-select
          :model-value="country"
          :options="filteredCountries"
          dense
          borderless
          emit-value
          map-options
          options-dense
          hide-dropdown-icon
          :display-value="displayValue"
          class="app-phone__cc"
          popup-content-class="app-phone__cc-popup"
          @popup-show="onPopupShow"
          @update:model-value="onCountrySelect"
        >
          <template #before-options>
            <div class="app-phone__search q-pa-xs">
              <q-input
                v-model="search"
                dense
                outlined
                autofocus
                placeholder="Search country"
                @update:model-value="filterCountries"
              >
                <template #prepend><q-icon name="o_search" size="18px" /></template>
              </q-input>
            </div>
          </template>
          <template #option="scope">
            <q-item v-bind="scope.itemProps">
              <q-item-section avatar class="app-phone__flag">{{ scope.opt.flag }}</q-item-section>
              <q-item-section>{{ scope.opt.name }}</q-item-section>
              <q-item-section side class="text-grey-7">+{{ scope.opt.code }}</q-item-section>
            </q-item>
          </template>
          <template #no-option>
            <q-item><q-item-section class="text-grey-6">No country found</q-item-section></q-item>
          </template>
        </q-select>
      </template>
    </q-input>
  </div>
</template>

<script setup>
/*
 * AppPhoneInput (WO-94 Step 10): a country calling-code selector (with flag +
 * searchable country name) + national number field, powered by libphonenumber-js.
 * Emits an E.164 string when the number is parseable, otherwise the raw national
 * digits. Country names/flags come from country-state-city; calling codes come from
 * libphonenumber-js (authoritative for parsing/formatting).
 */
import { ref, computed, watch } from 'vue'
import {
  getCountries,
  getCountryCallingCode,
  parsePhoneNumberFromString,
  getExampleNumber,
  AsYouType,
  isPossiblePhoneNumber
} from 'libphonenumber-js'
import examples from 'libphonenumber-js/examples.mobile.json'
import { Country } from 'country-state-city'
import AppFieldLabel from './AppFieldLabel.vue'

defineOptions({ inheritAttrs: false })

const props = defineProps({
  modelValue: { type: String, default: '' },
  label: { type: String, default: '' },
  required: { type: Boolean, default: false },
  defaultCountry: { type: String, default: 'US' },
  v: { type: Object, default: null }
})

const emit = defineEmits(['update:modelValue'])

// ISO country code -> { name, flag } from country-state-city (used only for display).
const META = (() => {
  const map = {}
  for (const c of Country.getAllCountries()) map[c.isoCode] = { name: c.name, flag: c.flag }
  return map
})()

const flagOf = (iso) => META[iso]?.flag || ''
const nameOf = (iso) => META[iso]?.name || iso
function codeOf (iso) {
  try {
    return getCountryCallingCode(iso)
  } catch (e) {
    return ''
  }
}

// Calling-code picker with the common markets (US, India) pinned to the top, then the rest by name.
const PINNED = ['US', 'IN']
const COUNTRY_OPTIONS = (() => {
  const all = getCountries()
  const opt = (c) => ({ label: `${nameOf(c)} +${codeOf(c)}`, value: c, name: nameOf(c), code: codeOf(c), flag: flagOf(c) })
  const pinned = PINNED.filter((c) => all.includes(c)).map(opt)
  const rest = all.filter((c) => !PINNED.includes(c)).map(opt).sort((a, b) => a.name.localeCompare(b.name))
  return [...pinned, ...rest]
})()

const country = ref(props.defaultCountry || 'US')
const national = ref('')
const search = ref('')
const filteredCountries = ref(COUNTRY_OPTIONS)

// Compact "🇺🇸 +1" shown in the collapsed selector, so the country segment stays narrow and the
// number field keeps most of the width (industry-standard proportion).
const displayValue = computed(() => `${flagOf(country.value)} +${codeOf(country.value)}`.trim())

// Search lives in a field inside the dropdown (#before-options) so the collapsed control stays compact.
function filterCountries (needle) {
  const q = (needle || '').toLowerCase().trim()
  filteredCountries.value = !q
    ? COUNTRY_OPTIONS
    : COUNTRY_OPTIONS.filter(
      (o) => o.name.toLowerCase().includes(q) || `+${o.code}`.includes(q) || o.value.toLowerCase().includes(q)
    )
}

function onPopupShow () {
  search.value = ''
  filteredCountries.value = COUNTRY_OPTIONS
}

function onCountrySelect (iso) {
  if (!iso) return
  country.value = iso
  emitValue()
}

// A real example number for the selected country, shown as the placeholder (formatted nationally).
const placeholder = computed(() => {
  try {
    const ex = getExampleNumber(country.value, examples)
    return ex ? ex.formatNational() : ''
  } catch (e) {
    return ''
  }
})

// Seed from an incoming E.164 value.
function hydrate (value) {
  if (!value) {
    national.value = ''
    return
  }
  const parsed = parsePhoneNumberFromString(String(value))
  if (parsed) {
    if (parsed.country) country.value = parsed.country
    national.value = parsed.formatNational()
  } else {
    national.value = String(value)
  }
}
hydrate(props.modelValue)

watch(
  () => props.modelValue,
  (val) => {
    // Avoid clobbering while the user is mid-edit.
    const current = parsePhoneNumberFromString(national.value || '', country.value)
    if (!current || current.number !== val) hydrate(val)
  }
)

// Follow the address country while the number is still empty (selecting a country sets the code).
watch(
  () => props.defaultCountry,
  (c) => {
    if (c && !national.value) country.value = c
  }
)

const MAX_E164_DIGITS = 15

function onInput (val) {
  let s = String(val || '')
  const digits = s.replace(/\D/g, '')
  // E.164 permits at most 15 digits — rebuild from the capped digits so the field can't accept more.
  if (digits.length > MAX_E164_DIGITS) s = digits.slice(0, MAX_E164_DIGITS)
  const formatter = new AsYouType(country.value)
  national.value = formatter.input(s)
  emitValue()
}

function emitValue () {
  const parsed = parsePhoneNumberFromString(national.value || '', country.value)
  emit('update:modelValue', parsed ? parsed.number : national.value)
}

// Self-validation (shown once the field is blurred): flags a missing required number and, more
// importantly, an invalid/too-long number that libphonenumber can't accept for the chosen country.
const touched = ref(false)
const internalError = computed(() => {
  const raw = national.value || ''
  if (!raw.trim()) return props.required ? 'Phone Number is required' : ''
  // isPossible checks the number's length/shape for the country (catches too-few/too-many digits)
  // without rejecting plausible numbers whose exact prefix isn't in the assigned numbering plan.
  return isPossiblePhoneNumber(raw, country.value) ? '' : 'Enter a valid phone number'
})

const hasError = computed(() => {
  if (props.v && props.v.$error) return true
  return touched.value && !!internalError.value
})
const errorMessage = computed(() => {
  if (props.v && Array.isArray(props.v.$errors) && props.v.$errors.length) {
    return props.v.$errors[0].$message
  }
  return touched.value && internalError.value ? internalError.value : undefined
})

function onBlur () {
  touched.value = true
  if (props.v && typeof props.v.$touch === 'function') props.v.$touch()
}

// Exposed for callers that want to validate outside Vuelidate.
defineExpose({
  isValid: () => isPossiblePhoneNumber(national.value || '', country.value)
})
</script>

<style scoped lang="scss">
/* Country selector = a narrow fixed segment; the number input takes the rest of the width
   (industry-standard proportion). Separated from the number by spacing only — no border. */
.app-phone :deep(.q-field__prepend) {
  height: 22px;
  padding-right: 10px;
  margin-right: 10px;
  align-self: center;
}

.app-phone__cc {
  width: 55px;
  :deep(.q-field__control),
  :deep(.q-field__native),
  :deep(.q-field__marginal) {
    min-height: 22px;
    padding-top: 0;
    padding-bottom: 0;
  }

  /* No border / outline / focus ring on the inner country selector — in any state. */
  :deep(.q-field__control)::before,
  :deep(.q-field__control)::after {
    display: none !important;
  }
  :deep(.q-field__control),
  :deep(.q-field__native),
  :deep(input) {
    box-shadow: none !important;
    outline: none !important;
  }

  :deep(.q-field__native) {
    padding-left: 0;
    min-width: 0;
    flex-wrap: nowrap;
    white-space: nowrap;
    font-size: 14px;
  }
}

/* Sticky search field at the top of the country dropdown. */
.app-phone__search {
  position: sticky;
  top: 0;
  z-index: 1;
  background: var(--sf-surface, #fff);
}

.app-phone__flag {
  min-width: 30px;
  font-size: 18px;
}

body.body--dark .app-phone__search {
  background: #1d1d1d;
}
</style>
