/*
 * Client-side checks for the on-site Authorize.Net card / bank-account fields. They only stop obviously
 * wrong input before Accept.js is called; the gateway still decides whether the card or account is good.
 * Each function returns an error message, or '' when the value is valid (Quasar rules use `|| message`).
 */

const digits = (v) => String(v || '').replace(/\D/g, '')

/** Card brand from the leading digits; only used to pick the CVC length and the allowed card length. */
export function cardBrand (number) {
  const n = digits(number)
  if (/^3[47]/.test(n)) return 'amex'
  if (/^3(0[0-5]|[68])/.test(n)) return 'diners'
  if (/^4/.test(n)) return 'visa'
  if (/^(5[1-5]|2(2[2-9]|[3-6]\d|7[01]|720))/.test(n)) return 'mastercard'
  if (/^(6011|65|64[4-9]|622)/.test(n)) return 'discover'
  if (/^35/.test(n)) return 'jcb'
  return 'unknown'
}

/** Luhn (mod 10) checksum used by every card number. */
export function luhnValid (number) {
  const n = digits(number)
  let sum = 0
  for (let i = 0; i < n.length; i++) {
    let d = Number(n[n.length - 1 - i])
    if (i % 2 === 1) {
      d *= 2
      if (d > 9) d -= 9
    }
    sum += d
  }
  return n.length > 0 && sum % 10 === 0
}

export function cardNumberError (value) {
  const n = digits(value)
  if (!n) return 'Enter your card number'
  if (/[^\d\s-]/.test(String(value))) return 'Use digits only'
  const brand = cardBrand(n)
  const okLength = brand === 'amex' ? n.length === 15 : n.length >= 13 && n.length <= 19
  if (!okLength) return 'Card number is incomplete'
  if (!luhnValid(n)) return 'Card number is not valid'
  return ''
}

/** "MM/YY": a real month, not in the past, and not absurdly far ahead. */
export function expiryError (value, now = new Date()) {
  const m = /^(\d{2})\s*\/\s*(\d{2}|\d{4})$/.exec(String(value || '').trim())
  if (!value) return 'Enter the expiry date'
  if (!m) return 'Use MM/YY'
  const month = Number(m[1])
  if (month < 1 || month > 12) return 'Month must be 01 to 12'
  const year = m[2].length === 2 ? 2000 + Number(m[2]) : Number(m[2])
  const thisYear = now.getFullYear()
  const thisMonth = now.getMonth() + 1
  if (year < thisYear || (year === thisYear && month < thisMonth)) return 'This card has expired'
  if (year > thisYear + 20) return 'Expiry year is not valid'
  return ''
}

export function cvcError (value, number) {
  const c = String(value || '').trim()
  if (!c) return 'Enter the security code'
  if (!/^\d+$/.test(c)) return 'Use digits only'
  const len = cardBrand(number) === 'amex' ? 4 : 3
  if (c.length !== len) return `Security code must be ${len} digits`
  return ''
}

export function accountNameError (value) {
  const v = String(value || '').trim()
  if (!v) return 'Enter the name on the account'
  if (v.length > 22) return 'Name must be 22 characters or fewer'
  return ''
}

/** US ABA routing number: 9 digits with the 3-7-1 checksum. */
export function routingNumberError (value) {
  const r = digits(value)
  if (!r) return 'Enter the routing number'
  if (r.length !== 9) return 'Routing number must be 9 digits'
  const w = [3, 7, 1, 3, 7, 1, 3, 7, 1]
  const sum = r.split('').reduce((s, d, i) => s + Number(d) * w[i], 0)
  if (sum % 10 !== 0) return 'Routing number is not valid'
  return ''
}

export function accountNumberError (value) {
  const a = String(value || '').trim()
  if (!a) return 'Enter the account number'
  if (!/^\d+$/.test(a)) return 'Use digits only'
  if (a.length < 4 || a.length > 17) return 'Account number must be 4 to 17 digits'
  return ''
}

/** True when every field of the chosen Authorize.Net instrument passes. */
export function authNetDetailsValid (instrument, card, bank) {
  if (instrument === 'ach') {
    return !accountNameError(bank.nameOnAccount) && !routingNumberError(bank.routingNumber) &&
      !accountNumberError(bank.accountNumber) && !!bank.accountType
  }
  return !cardNumberError(card.number) && !expiryError(card.expiry) && !cvcError(card.cvc, card.number)
}
