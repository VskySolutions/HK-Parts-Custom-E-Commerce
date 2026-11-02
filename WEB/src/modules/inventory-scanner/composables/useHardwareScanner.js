/*
 * Keyboard-wedge (USB / Bluetooth handheld) barcode scanner support. Those devices "type" the code very
 * fast and finish with Enter. A burst of characters arriving < 50 ms apart, at least 4 long, ending in
 * Enter is treated as a scan; normal human typing is far slower and is left alone.
 *
 * Keystrokes aimed at a text field are ignored (the field gets them, and its own Enter handler runs), so
 * the manual-entry box keeps working — a wedge scanner typing into it simply fills it and submits.
 */
import { onMounted, onBeforeUnmount } from 'vue'

const MAX_GAP_MS = 50
const MIN_LENGTH = 4

export function useHardwareScanner (onScan, { enabled = () => true } = {}) {
  let buffer = ''
  let last = 0

  function isTypingTarget (el) {
    if (!el) return false
    const tag = (el.tagName || '').toLowerCase()
    return tag === 'input' || tag === 'textarea' || tag === 'select' || el.isContentEditable
  }

  function onKeydown (e) {
    if (!enabled() || e.ctrlKey || e.altKey || e.metaKey || isTypingTarget(e.target)) return
    const now = performance.now()
    if (now - last > MAX_GAP_MS) buffer = ''
    last = now

    if (e.key === 'Enter') {
      const code = buffer
      buffer = ''
      if (code.length >= MIN_LENGTH) {
        e.preventDefault()
        onScan(code)
      }
      return
    }
    if (e.key.length === 1) buffer += e.key
  }

  onMounted(() => window.addEventListener('keydown', onKeydown, true))
  onBeforeUnmount(() => window.removeEventListener('keydown', onKeydown, true))
}
