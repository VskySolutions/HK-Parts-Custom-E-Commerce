/*
 * useNotify() (WO-94 Step 8).
 *
 * Wraps Quasar's Notify.create with standard defaults (multiLine, html support,
 * bottom position). Uses the static Notify API so it also works outside of a
 * component setup (interceptors, stores, ...).
 *
 * Exports notifyPositive/Negative/Warning/Info and the success/error/warning/
 * info aliases, plus a useNotify() composable returning them all.
 */
import { Notify } from 'quasar'

const BASE = {
  multiLine: true,
  html: false,
  position: 'bottom',
  timeout: 4000,
  progress: true
}

const TYPE_CONFIG = {
  positive: { type: 'positive', color: 'positive', icon: 'mdi-check-circle' },
  negative: { type: 'negative', color: 'negative', icon: 'mdi-alert-circle' },
  warning: { type: 'warning', color: 'warning', textColor: 'dark', icon: 'mdi-alert' },
  info: { type: 'info', color: 'info', icon: 'mdi-information' }
}

function emit (kind, message, opts = {}) {
  return Notify.create({
    ...BASE,
    ...TYPE_CONFIG[kind],
    message: typeof message === 'string' ? message : String(message ?? ''),
    actions: [{ icon: 'mdi-close', color: 'white', round: true, dense: true }],
    ...opts
  })
}

export function notifyPositive (message, opts) {
  return emit('positive', message, opts)
}
export function notifyNegative (message, opts) {
  return emit('negative', message, opts)
}
export function notifyWarning (message, opts) {
  return emit('warning', message, opts)
}
export function notifyInfo (message, opts) {
  return emit('info', message, opts)
}

// Aliases
export const success = notifyPositive
export const error = notifyNegative
export const warning = notifyWarning
export const info = notifyInfo

export function useNotify () {
  return {
    notifyPositive,
    notifyNegative,
    notifyWarning,
    notifyInfo,
    success: notifyPositive,
    error: notifyNegative,
    warning: notifyWarning,
    info: notifyInfo,
    // legacy short names
    positive: notifyPositive,
    negative: notifyNegative
  }
}

export default useNotify
