/*
 * Shared validators (WO-94 Step 5/8 support).
 *
 * Re-exports the @vuelidate/validators primitives and adds project validators
 * built on `validator` (emails/urls) and `libphonenumber-js` (phone numbers).
 * Each custom rule is wrapped with a human-readable message via helpers.
 */
import {
  required,
  requiredIf,
  minLength,
  maxLength,
  minValue,
  maxValue,
  numeric,
  integer,
  sameAs,
  helpers
} from '@vuelidate/validators'
import validator from 'validator'
import { isValidPhoneNumber } from 'libphonenumber-js'

const { withMessage, req } = helpers

// Email using the `validator` library (stricter than a naive regex).
export const email = withMessage(
  'Enter a valid email address',
  (value) => !req(value) || validator.isEmail(String(value))
)

// Absolute URL.
export const url = withMessage(
  'Enter a valid URL',
  (value) =>
    !req(value) ||
    validator.isURL(String(value), { require_protocol: true, require_valid_protocol: true })
)

// Shared account-password policy — single source of truth for the frontend, mirrored on the
// backend in API/src/Application/Common/Validation/PasswordRules.cs: 8–16 chars, at least one
// letter and one number.
export const PASSWORD_MIN = 8
export const PASSWORD_MAX = 16

function passwordPolicyOk (value) {
  const v = String(value)
  return v.length >= PASSWORD_MIN && v.length <= PASSWORD_MAX && /[A-Za-z]/.test(v) && /\d/.test(v)
}

// Vuelidate rule (for pages that validate via `v$`).
export const strongPassword = withMessage(
  `Password must be ${PASSWORD_MIN}–${PASSWORD_MAX} characters and include a letter and a number`,
  (value) => !req(value) || passwordPolicyOk(value)
)

// Quasar `:rules` array (for pages that validate through QForm/q-input rules). Each returns
// true or an error string. Keep messages granular so the user knows exactly what to fix.
export function passwordRules () {
  return [
    (v) => (!!v && String(v).length >= PASSWORD_MIN) || `Use at least ${PASSWORD_MIN} characters`,
    (v) => (!v || String(v).length <= PASSWORD_MAX) || `Use at most ${PASSWORD_MAX} characters`,
    (v) => (/[A-Za-z]/.test(String(v || '')) && /\d/.test(String(v || ''))) || 'Include a letter and a number'
  ]
}

// Quasar confirm-password rule; pass a getter for the password to compare against.
export function matchRule (getOther, message = 'Passwords do not match') {
  return (v) => v === (typeof getOther === 'function' ? getOther() : getOther) || message
}

// 0–4 strength score with a label/colour, for the strength meter in AppPasswordField.
export function passwordStrength (value) {
  const v = String(value || '')
  if (!v) return { score: 0, label: '', color: 'grey-5' }
  let score = 0
  if (v.length >= PASSWORD_MIN) score++
  if (v.length >= 12) score++
  if (/[a-z]/.test(v) && /[A-Z]/.test(v)) score++
  if (/\d/.test(v)) score++
  if (/[^A-Za-z0-9]/.test(v)) score++
  const clamped = Math.min(4, Math.max(1, score))
  const meta = {
    1: { label: 'Weak', color: 'negative' },
    2: { label: 'Fair', color: 'orange' },
    3: { label: 'Good', color: 'light-green-7' },
    4: { label: 'Strong', color: 'positive' }
  }[clamped]
  return { score: clamped, ...meta }
}

// Hex colour (#rgb / #rrggbb).
export const hexColor = withMessage(
  'Enter a valid hex colour',
  (value) => !req(value) || validator.isHexColor(String(value))
)

// Phone number for a given ISO country (defaults to permissive international).
export function phone (defaultCountry) {
  return withMessage('Enter a valid phone number', (value) => {
    if (!req(value)) return true
    try {
      return isValidPhoneNumber(String(value), defaultCountry)
    } catch (e) {
      return false
    }
  })
}

export {
  required,
  requiredIf,
  minLength,
  maxLength,
  minValue,
  maxValue,
  numeric,
  integer,
  sameAs,
  helpers
}

export default {
  required,
  requiredIf,
  minLength,
  maxLength,
  minValue,
  maxValue,
  numeric,
  integer,
  sameAs,
  email,
  url,
  strongPassword,
  passwordRules,
  matchRule,
  passwordStrength,
  PASSWORD_MIN,
  PASSWORD_MAX,
  hexColor,
  phone
}
