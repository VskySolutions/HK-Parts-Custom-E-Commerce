/*
 * Canonical date/time formatting for the whole app.
 *
 * The API stores and returns timestamps in UTC. .NET's System.Text.Json often serialises a UTC
 * DateTime WITHOUT a trailing 'Z' (e.g. "2026-07-10T12:34:56"), which `new Date()` would wrongly
 * read as LOCAL time — so parseUtc() appends 'Z' to any naive ISO datetime before parsing.
 *
 * App-wide display standard: MM/DD/YYYY hh:mm AM/PM (12-hour), rendered in the ACTIVE display
 * timezone. The active zone is the tenant's configured display timezone (set from tenant branding),
 * optionally overridden per signed-in customer on the storefront — see setActiveTimeZone(). Callers
 * may also pass an explicit zone as the 2nd argument. Pure calendar dates (no time) render as
 * MM/DD/YYYY with no timezone shift.
 */

const EMPTY = '—'
const NAIVE_DATETIME = /^\d{4}-\d{2}-\d{2}[T ]\d{2}:\d{2}(:\d{2}(\.\d+)?)?$/
const DATE_ONLY = /^\d{4}-\d{2}-\d{2}$/

const pad = (n) => String(n).padStart(2, '0')

function browserTimeZone () {
  try {
    return Intl.DateTimeFormat().resolvedOptions().timeZone || 'UTC'
  } catch (e) {
    return 'UTC'
  }
}

// The active display timezone (IANA id). Defaults to the browser zone until branding sets it.
let activeTimeZone = browserTimeZone()

export function setActiveTimeZone (tz) {
  if (tz && typeof tz === 'string') activeTimeZone = tz.trim()
}
export function getActiveTimeZone () {
  return activeTimeZone
}

// Parse an API value into a Date, treating a timezone-less ISO datetime as UTC (see file header).
export function parseUtc (value) {
  if (!value) return null
  if (value instanceof Date) return Number.isNaN(value.getTime()) ? null : value
  let s = String(value).trim()
  if (!s) return null
  if (NAIVE_DATETIME.test(s)) s = s.replace(' ', 'T') + 'Z'
  const d = new Date(s)
  return Number.isNaN(d.getTime()) ? null : d
}

const isDateOnly = (value) => typeof value === 'string' && DATE_ONLY.test(value.trim())

// Extract formatted parts of a Date in a given IANA zone; falls back to the browser zone on a bad id.
function partsIn (d, tz, options) {
  let fmt
  try {
    fmt = new Intl.DateTimeFormat('en-US', { timeZone: tz, hourCycle: 'h12', ...options })
  } catch (e) {
    fmt = new Intl.DateTimeFormat('en-US', { hourCycle: 'h12', ...options })
  }
  const out = {}
  for (const p of fmt.formatToParts(d)) out[p.type] = p.value
  return out
}

// MM/DD/YYYY — for pure calendar dates (no timezone shift), else a timestamp's date in the active zone.
export function formatDate (value, tz) {
  const d = parseUtc(value)
  if (!d) return EMPTY
  if (isDateOnly(value)) return `${pad(d.getUTCMonth() + 1)}/${pad(d.getUTCDate())}/${d.getUTCFullYear()}`
  const p = partsIn(d, tz || activeTimeZone, { year: 'numeric', month: '2-digit', day: '2-digit' })
  return `${p.month}/${p.day}/${p.year}`
}

// MM/DD/YYYY hh:mm AM/PM — the app-wide standard for timestamps, in the active (or given) timezone.
export function formatDateTime (value, tz) {
  const d = parseUtc(value)
  if (!d) return EMPTY
  const p = partsIn(d, tz || activeTimeZone, {
    year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit'
  })
  return `${p.month}/${p.day}/${p.year} ${p.hour}:${p.minute} ${(p.dayPeriod || '').toUpperCase()}`
}

// IANA timezone options for the settings/profile dropdowns.
export function timeZoneOptions () {
  let zones
  try {
    zones = typeof Intl.supportedValuesOf === 'function' ? Intl.supportedValuesOf('timeZone') : null
  } catch (e) {
    zones = null
  }
  if (!zones || !zones.length) zones = FALLBACK_ZONES
  return zones.map((z) => ({ label: z.replace(/_/g, ' '), value: z }))
}

// Minimal fallback for engines without Intl.supportedValuesOf.
const FALLBACK_ZONES = [
  'UTC',
  'America/New_York', 'America/Chicago', 'America/Denver', 'America/Los_Angeles', 'America/Sao_Paulo',
  'Europe/London', 'Europe/Paris', 'Europe/Berlin', 'Europe/Moscow',
  'Asia/Dubai', 'Asia/Kolkata', 'Asia/Singapore', 'Asia/Shanghai', 'Asia/Tokyo',
  'Australia/Sydney', 'Pacific/Auckland'
]

export default { parseUtc, formatDate, formatDateTime, setActiveTimeZone, getActiveTimeZone, timeZoneOptions }
