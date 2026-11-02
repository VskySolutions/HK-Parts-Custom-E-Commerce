/*
 * Resolve a scanned / typed code and open the product screen. Shared by the dashboard (manual entry,
 * handheld scanner) and the scan page (camera, photo, manual, handheld).
 *
 * open(code, source) resolves to:
 *   'found'      — navigated to the product
 *   'not-found'  — 404 (caller shows its own message and keeps scanning)
 *   'cancelled'  — several products matched and the user closed the picker
 * and throws (with a readable .message) for anything else (400, 403, network).
 */
import { useQuasar } from 'quasar'
import { useRouter } from 'vue-router'
import { inventoryScannerApi, productRoute, errorText } from '../api'

export function useScannerLookup () {
  const $q = useQuasar()
  const router = useRouter()

  // 409: several products share the code — let the user say which one they scanned.
  function pickCandidate (code, candidates) {
    return new Promise((resolve) => {
      $q.dialog({
        title: 'Several products match',
        message: `"${code}" belongs to more than one product. Choose the one you scanned.`,
        options: {
          type: 'radio',
          model: null,
          items: candidates.map((c, i) => ({
            value: i,
            label: c.name + (c.variantLabel ? ' (' + c.variantLabel + ')' : '') +
              ' · SKU ' + (c.sku || '—') + ' · Barcode ' + (c.barcode || '—') +
              (c.isPublished ? '' : ' · Inactive')
          }))
        },
        ok: { label: 'Open', color: 'positive', unelevated: true, noCaps: true },
        cancel: { label: 'Cancel', flat: true, noCaps: true },
        persistent: true
      })
        .onOk((index) => resolve(index === null || index === undefined ? null : candidates[index]))
        .onCancel(() => resolve(null))
    })
  }

  async function open (rawCode, source) {
    const code = String(rawCode || '').trim()
    let product
    try {
      product = await inventoryScannerApi.lookup(code)
    } catch (err) {
      const status = err?.response?.status
      if (status === 404) return 'not-found'
      if (status === 409 && Array.isArray(err.response.data?.candidates)) {
        product = await pickCandidate(code, err.response.data.candidates)
        if (!product) return 'cancelled'
      } else {
        throw new Error(status === 403 ? "You don't have permission to use the inventory scanner." : errorText(err))
      }
    }
    await router.push(productRoute(product, { code, source, matchedBy: product.matchedBy }))
    return 'found'
  }

  return { open }
}
