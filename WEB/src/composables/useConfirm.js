/*
 * useConfirm() (WO-94 Step 8).
 *
 * Promise-wrapped Quasar Dialog.create with OK/Cancel. Resolves true on OK and
 * false on Cancel/dismiss, so callers can simply:
 *
 *   const confirm = useConfirm()
 *   if (await confirm({ title: 'Delete?', message: '...' })) { ... }
 */
import { Dialog } from 'quasar'

export function useConfirm () {
  return function confirm (options = {}) {
    const {
      title = 'Please confirm',
      message = 'Are you sure you want to proceed?',
      okLabel = 'OK',
      cancelLabel = 'Cancel',
      color = 'primary',
      persistent = true,
      html = false
    } = options

    return new Promise((resolve) => {
      let settled = false
      const done = (value) => {
        if (settled) return
        settled = true
        resolve(value)
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
}

export default useConfirm
