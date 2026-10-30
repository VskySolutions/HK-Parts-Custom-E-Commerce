/*
 * useAddress — the single source of truth for address form behaviour across the app.
 *
 * - Country / State / City option loaders backed by `country-state-city`, with the most common
 *   markets (US, India) pinned to the top of the country list, then alphabetical.
 * - Per-country postal-code metadata (localised label, example placeholder, validation regex).
 * - Phone default-country derivation (the address country drives the phone country).
 * - A canonical address shape used by AppAddressForm and every consumer.
 *
 * Heavy data libs (country-state-city) load only where this is imported — do NOT globally register
 * consumers, to keep them out of the initial bundle.
 */
import { Country, State, City } from 'country-state-city'

// Markets to surface first in the country picker.
const PINNED_COUNTRIES = ['US', 'IN']

function toOptions (list, valueKey, labelKey) {
  return list.map((item) => ({ label: item[labelKey], value: item[valueKey] }))
}

// Country list ordered: pinned markets first (in PINNED order), then the rest alphabetically.
const ALL_COUNTRIES = toOptions(Country.getAllCountries(), 'isoCode', 'name')
const COUNTRY_OPTIONS = (() => {
  const pinned = PINNED_COUNTRIES
    .map((iso) => ALL_COUNTRIES.find((c) => c.value === iso))
    .filter(Boolean)
  const rest = ALL_COUNTRIES
    .filter((c) => !PINNED_COUNTRIES.includes(c.value))
    .sort((a, b) => a.label.localeCompare(b.label))
  return [...pinned, ...rest]
})()

// ---- Postal-code metadata (curated for common markets; generic fallback otherwise) ----
const POSTAL = {
  US: { label: 'ZIP code', example: '90210', regex: /^\d{5}(-\d{4})?$/ },
  IN: { label: 'PIN code', example: '560001', regex: /^\d{6}$/ },
  GB: { label: 'Postcode', example: 'SW1A 1AA', regex: /^[A-Za-z]{1,2}\d[A-Za-z\d]? ?\d[A-Za-z]{2}$/ },
  CA: { label: 'Postal code', example: 'K1A 0B1', regex: /^[A-Za-z]\d[A-Za-z] ?\d[A-Za-z]\d$/ },
  AU: { label: 'Postcode', example: '2000', regex: /^\d{4}$/ },
  DE: { label: 'PLZ', example: '10115', regex: /^\d{5}$/ },
  FR: { label: 'Code postal', example: '75008', regex: /^\d{5}$/ },
  IT: { label: 'CAP', example: '00184', regex: /^\d{5}$/ },
  ES: { label: 'Código postal', example: '28001', regex: /^\d{5}$/ },
  NL: { label: 'Postcode', example: '1011 AB', regex: /^\d{4} ?[A-Za-z]{2}$/ },
  JP: { label: 'Postal code', example: '100-0001', regex: /^\d{3}-?\d{4}$/ },
  BR: { label: 'CEP', example: '01310-100', regex: /^\d{5}-?\d{3}$/ },
  SG: { label: 'Postal code', example: '238859', regex: /^\d{6}$/ },
  CN: { label: 'Postal code', example: '100000', regex: /^\d{6}$/ },
  AE: { label: 'Postal code', example: '', regex: null }
}
const DEFAULT_POSTAL = { label: 'Postal code', example: '', regex: null }

export function emptyAddress () {
  return {
    firstName: '',
    lastName: '',
    company: '',
    countryCode: '',
    stateProvince: '',
    city: '',
    addressLine1: '',
    addressLine2: '',
    landmark: '',
    postalCode: '',
    phoneNumber: ''
  }
}

export function useAddress () {
  const countryOptions = COUNTRY_OPTIONS

  function statesFor (countryCode) {
    if (!countryCode) return []
    return toOptions(State.getStatesOfCountry(countryCode), 'isoCode', 'name')
  }

  function citiesFor (countryCode, stateCode) {
    if (!countryCode || !stateCode) return []
    return toOptions(City.getCitiesOfState(countryCode, stateCode), 'name', 'name')
  }

  function countryName (iso) {
    return Country.getCountryByCode(iso)?.name || iso || ''
  }

  function stateName (countryCode, stateCode) {
    if (!countryCode || !stateCode) return stateCode || ''
    return State.getStateByCodeAndCountry(stateCode, countryCode)?.name || stateCode
  }

  // Postal metadata for a country (localised label, example, regex).
  function postalMeta (countryCode) {
    return POSTAL[countryCode] || DEFAULT_POSTAL
  }

  // True when the postal code matches the country's format (empty passes — required is handled separately).
  function postalValid (countryCode, value) {
    if (!value) return true
    const meta = postalMeta(countryCode)
    return meta.regex ? meta.regex.test(String(value).trim()) : true
  }

  return {
    countryOptions,
    statesFor,
    citiesFor,
    countryName,
    stateName,
    postalMeta,
    postalValid,
    emptyAddress
  }
}

export default useAddress
