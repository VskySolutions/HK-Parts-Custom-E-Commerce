/*
 * Confirmation dialog wrapper (WO-94 Step 11).
 *
 * Standalone (non-composable) Dialog.create helper usable from anywhere.
 * Resolves true on OK, false on Cancel/dismiss.
 */
import { Dialog } from 'quasar'

export function confirmation (options = {}) {
  const {
    title = 'Please confirm',
    message = 'Are you sure you want to proceed?',
    okLabel = 'Confirm',
    cancelLabel = 'Cancel',
    color = 'primary',
    persistent = true,
    html = false
  } = options

  return new Promise((resolve) => {
    let settled = false
    const done = (v) => {
      if (settled) return
      settled = true
      resolve(v)
    }

    Dialog.create({
      title,
      message,
      html,
      persistent,
      ok: { label: okLabel, color, unelevated: true },
      cancel: { label: cancelLabel, color: 'grey-8', flat: true }
    })
      .onOk(() => done(true))
      .onCancel(() => done(false))
      .onDismiss(() => done(false))
  })
}

export default confirmation
