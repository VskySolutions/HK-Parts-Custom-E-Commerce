<template>
  <!-- Full-screen camera view, laid over the app shell (like the PHP scan page). -->
  <div class="scan-screen">
    <div class="scan-topbar">
      <q-btn flat round dense icon="o_arrow_back" color="white" :to="{ name: 'inventory-scanner' }" aria-label="Back" />
      <div class="scan-topbar__title">Scan QR / Barcode</div>
    </div>

    <div :id="readerId" class="scan-reader" />

    <template v-if="cameraState === 'running' || cameraState === 'starting'">
      <div class="scan-frame"><div class="scan-line" /></div>
      <div class="scan-hint">Align the QR / barcode within the frame</div>
    </template>

    <!-- Camera unavailable: explain why and offer the other two ways in -->
    <div v-else-if="cameraState === 'unavailable'" class="scan-fallback">
      <q-icon name="o_no_photography" size="48px" color="grey-5" />
      <div class="text-subtitle1 q-mt-sm">Camera not available</div>
      <div class="text-body2 text-grey-4 q-mt-xs">{{ cameraError }}</div>
      <div class="q-mt-md column q-gutter-sm items-center">
        <q-btn unelevated color="positive" no-caps icon="o_refresh" label="Retry" class="scan-fallback__btn" @click="retryCamera" />
        <q-btn outline color="white" no-caps icon="o_keyboard" label="Enter SKU manually" class="scan-fallback__btn" @click="openManual" />
        <q-btn flat color="white" no-caps icon="o_photo_library" label="Scan from a photo" class="scan-fallback__btn" @click="pickFile" />
      </div>
    </div>

    <transition name="fade">
      <div v-if="toast" class="scan-toast" :class="toast.ok ? 'scan-toast--ok' : ''">{{ toast.text }}</div>
    </transition>

    <div class="scan-controls">
      <button type="button" class="scan-ctrl" title="Scan from a photo" aria-label="Scan from a photo" @click="pickFile">
        <q-icon name="o_photo_library" size="26px" />
      </button>
      <button
        type="button"
        class="scan-ctrl"
        :class="{ 'scan-ctrl--on': torchOn, 'scan-ctrl--off': cameraState === 'running' && !torchSupported }"
        title="Toggle flash"
        aria-label="Toggle flash"
        :disabled="cameraState !== 'running'"
        @click="toggleTorch"
      >
        <q-icon :name="torchOn ? 'o_flashlight_on' : 'o_flashlight_off'" size="26px" />
      </button>
      <button type="button" class="scan-ctrl" title="Enter SKU manually" aria-label="Enter SKU manually" @click="openManual">
        <q-icon name="o_keyboard" size="26px" />
      </button>
    </div>
    <input ref="fileInput" type="file" accept="image/*" class="hidden" @change="onFile">
  </div>
</template>

<script setup>
/*
 * Barcode inventory scanner — camera scan (PHP scan.blade.php). Uses html5-qrcode 2.3.8 (same library and
 * formats as the PHP page): back camera, live decode, flash toggle, "scan from a photo", manual SKU entry,
 * and USB / Bluetooth handheld (keyboard-wedge) scanners.
 *
 * Unlike the PHP page, 12–13 digit codes (UPC / EAN) are NOT ignored — that filter broke retail-barcode
 * scanning. False reads are suppressed instead by requiring the SAME value to decode twice in a row
 * within 1.5 s before it's looked up.
 *
 * A confirmed code is looked up: found → stop the camera and open the product; not found → red toast and
 * keep scanning; several matches → picker dialog (useScannerLookup).
 *
 * Camera access needs a secure context (HTTPS, or http://localhost). On a phone hitting the dev server
 * over plain http://<LAN-IP> the browser blocks the camera — the page then offers retry / manual / photo.
 */
import { ref, onMounted, onBeforeUnmount } from 'vue'
import { useQuasar } from 'quasar'
import { Html5Qrcode, Html5QrcodeSupportedFormats } from 'html5-qrcode'
import { ScanSource } from '../api'
import { useScannerLookup } from '../composables/useScannerLookup'
import { useHardwareScanner } from '../composables/useHardwareScanner'

const $q = useQuasar()
const { open } = useScannerLookup()

const CONFIRM_WINDOW_MS = 1500

const readerId = 'inv-scan-reader-' + Math.random().toString(36).slice(2, 8)
const fileInput = ref(null)
const cameraState = ref('starting') // starting | running | unavailable
const cameraError = ref('')
const torchOn = ref(false)
const torchSupported = ref(false)
const toast = ref(null)
let scanner = null
let processing = false
let lastRead = { code: '', at: 0 }
let toastTimer = null
let audioCtx = null
let leaving = false

const FORMATS = [
  Html5QrcodeSupportedFormats.QR_CODE,
  Html5QrcodeSupportedFormats.EAN_13,
  Html5QrcodeSupportedFormats.EAN_8,
  Html5QrcodeSupportedFormats.UPC_A,
  Html5QrcodeSupportedFormats.UPC_E,
  Html5QrcodeSupportedFormats.CODE_128,
  Html5QrcodeSupportedFormats.CODE_39,
  Html5QrcodeSupportedFormats.CODE_93,
  Html5QrcodeSupportedFormats.ITF,
  Html5QrcodeSupportedFormats.CODABAR
]

function showToast (text, ok = false) {
  toast.value = { text, ok }
  clearTimeout(toastTimer)
  toastTimer = setTimeout(() => { toast.value = null }, 2200)
}

// Short haptic + beep on an accepted read. Both are best-effort (iOS has no vibrate; audio may be blocked).
function feedback () {
  try { navigator.vibrate?.(100) } catch (e) { /* unsupported */ }
  try {
    const Ctx = window.AudioContext || window.webkitAudioContext
    if (!Ctx) return
    audioCtx = audioCtx || new Ctx()
    const osc = audioCtx.createOscillator()
    const gain = audioCtx.createGain()
    osc.frequency.value = 1200
    gain.gain.value = 0.08
    osc.connect(gain).connect(audioCtx.destination)
    osc.start()
    osc.stop(audioCtx.currentTime + 0.09)
  } catch (e) { /* audio blocked */ }
}

// Live camera decodes: accept a value only on its second consecutive read within the window.
function onDecoded (raw) {
  const code = String(raw || '').trim()
  if (!code || processing) return
  const now = Date.now()
  const confirmed = lastRead.code === code && now - lastRead.at <= CONFIRM_WINDOW_MS
  lastRead = { code, at: now }
  if (confirmed) lookup(code, ScanSource.Camera)
}

// Resolves true when the product screen was opened.
async function lookup (code, source) {
  if (!code || processing) return false
  processing = true
  feedback()
  try {
    const result = await open(code, source)
    if (result === 'found') {
      leaving = true
      return true
    }
    if (result === 'not-found') showToast(`No product found for "${code}". Try again.`)
  } catch (err) {
    showToast(err.message)
  }
  // Keep scanning; resetting the confirmation stops one unknown label re-firing many times a second.
  lastRead = { code: '', at: 0 }
  setTimeout(() => { processing = false }, 800)
  return false
}

async function startCamera () {
  torchOn.value = false
  torchSupported.value = false
  if (!window.isSecureContext || !navigator.mediaDevices?.getUserMedia) {
    cameraState.value = 'unavailable'
    cameraError.value = 'The browser only allows the camera on HTTPS (or localhost). Type the SKU or use a photo of the barcode instead.'
    return
  }
  cameraState.value = 'starting'
  try {
    if (!scanner) scanner = new Html5Qrcode(readerId, { formatsToSupport: FORMATS, verbose: false })
    await scanner.start(
      { facingMode: 'environment' },
      { fps: 10, qrbox: (w, h) => ({ width: Math.min(300, Math.floor(w * 0.85)), height: Math.min(180, Math.floor(h * 0.4)) }) },
      onDecoded,
      () => {} // fires continuously while nothing is in frame
    )
    // The user may have left while the camera was still starting.
    if (leaving) { await stopCamera(); return }
    cameraState.value = 'running'
    try {
      torchSupported.value = !!scanner.getRunningTrackCameraCapabilities?.()?.torchFeature?.()?.isSupported()
    } catch (e) { torchSupported.value = false }
  } catch (err) {
    cameraState.value = 'unavailable'
    const msg = String(err?.message || err || '')
    cameraError.value = /NotAllowed|Permission/i.test(msg)
      ? 'Camera permission was denied. Allow camera access for this site in the browser settings, then retry — or enter the SKU manually.'
      : /NotFound|Requested device not found|Overconstrained/i.test(msg)
        ? 'No camera was found on this device.'
        : 'Could not start the camera: ' + msg
  }
}

function retryCamera () {
  startCamera()
}

async function stopCamera () {
  if (scanner && scanner.isScanning) {
    try { await scanner.stop() } catch (e) { /* already stopped */ }
  }
  torchOn.value = false
}

function pickFile () {
  fileInput.value?.click()
}

async function onFile (e) {
  const file = e.target.files && e.target.files[0]
  e.target.value = ''
  if (!file || processing) return
  let wasRunning = false
  let decoded = null
  try {
    // scanFile needs a scanner instance but not a running camera (and can't run while it streams).
    if (!scanner) scanner = new Html5Qrcode(readerId, { formatsToSupport: FORMATS, verbose: false })
    wasRunning = scanner.isScanning
    if (wasRunning) await scanner.stop()
    decoded = await scanner.scanFile(file, false)
  } catch (err) {
    showToast('Could not read a barcode from that image.')
  }
  const opened = decoded ? await lookup(String(decoded).trim(), ScanSource.Gallery) : false
  if (!opened && wasRunning && !leaving) startCamera()
}

function openManual () {
  $q.dialog({
    title: 'Enter SKU',
    prompt: {
      model: '',
      type: 'text',
      placeholder: 'e.g. HK-12345',
      isValid: (v) => !!String(v || '').trim(),
      attrs: { autocomplete: 'off', maxlength: 100 }
    },
    ok: { label: 'Go', color: 'positive', unelevated: true, noCaps: true },
    cancel: { label: 'Cancel', flat: true, noCaps: true }
  }).onOk(async (value) => {
    const code = String(value || '').trim()
    if (!code) { showToast('Please enter a SKU number.'); return }
    const opened = await lookup(code, ScanSource.Manual)
    if (opened) await stopCamera()
  })
}

function toggleTorch () {
  try {
    const torch = scanner?.getRunningTrackCameraCapabilities?.()?.torchFeature?.()
    if (!torch || !torch.isSupported()) {
      showToast('Flash is not supported on this device.')
      return
    }
    torchOn.value = !torchOn.value
    torch.apply(torchOn.value)
  } catch (e) {
    showToast('Flash is not supported on this device.')
  }
}

useHardwareScanner(async (code) => {
  const opened = await lookup(code.trim(), ScanSource.Hardware)
  if (opened) await stopCamera()
})

onMounted(startCamera)
// Always release the camera when leaving, or the phone's camera light stays on.
onBeforeUnmount(async () => {
  leaving = true
  clearTimeout(toastTimer)
  await stopCamera()
  try { scanner?.clear() } catch (e) { /* nothing rendered */ }
  try { audioCtx?.close() } catch (e) { /* already closed */ }
})
</script>

<style scoped lang="scss">
.scan-screen {
  position: fixed;
  inset: 0;
  z-index: 3000; // above the app header / drawer
  background: #000;
  color: #fff;
  overflow: hidden;
}
.scan-topbar {
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  z-index: 5;
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 12px 12px;
  background: linear-gradient(rgba(0, 0, 0, 0.55), transparent);
  &__title { font-size: 16px; font-weight: 600; }
}
.scan-reader {
  position: absolute;
  inset: 0;
  :deep(video) {
    width: 100% !important;
    height: 100% !important;
    object-fit: cover;
  }
  // html5-qrcode injects its own shaded region / borders — the custom frame below replaces them.
  :deep(#qr-shaded-region), :deep(img) { display: none !important; }
}
.scan-frame {
  position: absolute;
  top: 50%;
  left: 50%;
  transform: translate(-50%, -50%);
  width: min(85vw, 320px);
  height: 200px;
  border: 3px solid #7ac143;
  border-radius: 14px;
  box-shadow: 0 0 0 2000px rgba(0, 0, 0, 0.35);
  z-index: 3;
  pointer-events: none;
}
.scan-line {
  position: absolute;
  left: 4%;
  width: 92%;
  height: 2px;
  background: #7ac143;
  box-shadow: 0 0 8px #7ac143;
  animation: scan-move 2.2s ease-in-out infinite;
}
@keyframes scan-move {
  0% { top: 6%; }
  50% { top: 92%; }
  100% { top: 6%; }
}
.scan-hint {
  position: absolute;
  top: calc(50% + 120px);
  left: 0;
  right: 0;
  z-index: 4;
  text-align: center;
  font-size: 14px;
  padding: 0 20px;
  text-shadow: 0 1px 3px rgba(0, 0, 0, 0.6);
}
.scan-fallback {
  position: absolute;
  inset: 0;
  z-index: 4;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  text-align: center;
  padding: 24px;
  &__btn { min-width: 220px; min-height: 44px; }
}
.scan-toast {
  position: absolute;
  top: 72px;
  left: 5%;
  right: 5%;
  z-index: 10;
  background: rgba(192, 57, 43, 0.95);
  padding: 12px 16px;
  border-radius: 8px;
  text-align: center;
  font-size: 14px;
  &--ok { background: rgba(122, 193, 67, 0.95); }
}
.scan-controls {
  position: absolute;
  bottom: 32px;
  left: 0;
  right: 0;
  z-index: 5;
  display: flex;
  justify-content: center;
  gap: 40px;
}
.scan-ctrl {
  width: 58px;
  height: 58px;
  border-radius: 50%;
  background: rgba(255, 255, 255, 0.15);
  border: 1px solid rgba(255, 255, 255, 0.4);
  color: #fff;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  &--on { background: #7ac143; border-color: #7ac143; }
  &--off { opacity: 0.45; }
  &:disabled { opacity: 0.35; cursor: default; }
}
.fade-enter-active, .fade-leave-active { transition: opacity 0.2s; }
.fade-enter-from, .fade-leave-to { opacity: 0; }
</style>
