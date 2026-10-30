/*
 * Frontend error reporting boot file (WO-71).
 *
 * Ships uncaught client errors to POST /api/logging/frontend-error so runtime
 * failures in the browser become observable server-side. It captures three
 * sources:
 *   - Vue's app.config.errorHandler (uncaught render / lifecycle / watcher errors)
 *   - window 'error'                (uncaught exceptions)
 *   - window 'unhandledrejection'   (promise rejections without a catch)
 *
 * Reporting is strictly best-effort — it must never itself throw or spam:
 *   - throttled to MAX_PER_WINDOW reports per rolling WINDOW_MS
 *   - identical messages de-duped within DEDUPE_MS
 *   - every path wrapped in try/catch; ALL failures (incl. 429 rate-limit) swallowed
 *
 * Runs AFTER boot/error.js (which keeps the console logging) and chains its Vue
 * handler rather than replacing it, so this only ADDS server ingest.
 */
import { anonApi } from 'services/api'

const ENDPOINT = '/api/logging/frontend-error'
const WINDOW_MS = 60000 // rolling one-minute window
const MAX_PER_WINDOW = 5 // at most ~5 reports/min
const DEDUPE_MS = 10000 // drop an identical message seen within 10s
const MAX_DEDUPE_KEYS = 50 // bound the de-dupe map so it can't grow unbounded

const recentTimestamps = [] // report times still inside the current window
const lastSeen = new Map() // message -> last-sent epoch ms

// Returns true when this message should be dropped (rate-limited or a recent duplicate).
function throttled (message) {
  const now = Date.now()

  // De-dupe identical messages within the dedupe window.
  const previous = lastSeen.get(message)
  if (previous && now - previous < DEDUPE_MS) return true

  // Rolling-window rate limit: prune expired entries, then cap.
  while (recentTimestamps.length && now - recentTimestamps[0] > WINDOW_MS) recentTimestamps.shift()
  if (recentTimestamps.length >= MAX_PER_WINDOW) return true

  recentTimestamps.push(now)
  lastSeen.set(message, now)

  // Keep the de-dupe map bounded by evicting stale keys.
  if (lastSeen.size > MAX_DEDUPE_KEYS) {
    const cutoff = now - DEDUPE_MS
    for (const [key, ts] of lastSeen) {
      if (ts < cutoff) lastSeen.delete(key)
    }
  }
  return false
}

function toMessage (err) {
  if (!err) return 'Unknown error'
  if (typeof err === 'string') return err
  if (err.message) return String(err.message)
  try {
    return String(err)
  } catch (e) {
    return 'Unserialisable error'
  }
}

function report (err) {
  try {
    const message = toMessage(err)
    if (!message || throttled(message)) return
    const stack = err && err.stack ? String(err.stack) : null
    const url = typeof location !== 'undefined' ? location.href : null
    // Fire-and-forget; swallow every failure (network, 429 rate-limit, etc.).
    anonApi.post(ENDPOINT, { message, stack, url }).catch(() => {})
  } catch (e) {
    // Error reporting must never throw.
  }
}

export default ({ app }) => {
  // Chain the existing Vue error handler (boot/error.js console logging) so both run.
  const previousHandler = app.config.errorHandler
  app.config.errorHandler = (err, instance, info) => {
    report(err)
    if (typeof previousHandler === 'function') {
      try {
        previousHandler(err, instance, info)
      } catch (e) {
        // Never let a downstream handler failure escape.
      }
    }
  }

  if (typeof window !== 'undefined') {
    // Uncaught runtime exceptions. Prefer the real Error (carries a stack).
    window.addEventListener('error', (event) => {
      report(event && (event.error || event.message))
    })
    // Promise rejections with no catch.
    window.addEventListener('unhandledrejection', (event) => {
      report(event && event.reason)
    })
  }
}
