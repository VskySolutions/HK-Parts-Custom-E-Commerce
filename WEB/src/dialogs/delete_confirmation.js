/*
 * Delete confirmation dialog wrapper (WO-94 Step 11).
 *
 * Pre-styled destructive confirmation. Resolves true when the user confirms.
 *
 *   if (await deleteConfirmation('the "Blue" widget')) { await api.remove(id) }
 */
import { Dialog } from 'quasar'

export function deleteConfirmation (itemLabel = 'this item', options = {}) {
  const {
    title = 'Delete',
    okLabel = 'Delete',
    cancelLabel = 'Cancel',
    message = `Are you sure you want to delete ${itemLabel}? This action cannot be undone.`,
    persistent = true
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
      persistent,
      ok: { label: okLabel, color: 'negative', unelevated: true, icon: 'mdi-delete' },
      cancel: { label: cancelLabel, color: 'grey-8', flat: true }
    })
      .onOk(() => done(true))
      .onCancel(() => done(false))
      .onDismiss(() => done(false))
  })
}

export default deleteConfirmation
