<template>
  <q-dialog
    :model-value="modelValue"
    maximized
    transition-show="fade"
    transition-hide="fade"
    class="sf-lightbox"
    @update:model-value="emit('update:modelValue', $event)"
  >
    <div class="sf-lightbox__backdrop" @click.self="close">
      <!-- Close -->
      <q-btn round flat class="sf-lightbox__close" icon="o_close" @click="close">
        <q-tooltip>Close (Esc)</q-tooltip>
      </q-btn>

      <!-- Counter -->
      <div v-if="slides.length > 1" class="sf-lightbox__counter">{{ index + 1 }} / {{ slides.length }}</div>

      <!-- Prev -->
      <q-btn
        v-if="slides.length > 1"
        round
        flat
        size="lg"
        class="sf-lightbox__nav sf-lightbox__nav--prev"
        icon="o_chevron_left"
        @click.stop="prev"
      />

      <!-- Stage -->
      <div
        v-touch-swipe.mouse.horizontal="onSwipe"
        class="sf-lightbox__stage"
        @click.self="close"
      >
        <template v-if="current">
          <!-- Video slide: YouTube/Vimeo embed, else a native <video> for a direct file. -->
          <div v-if="isVideo(current)" class="sf-lightbox__frame" @click.stop>
            <iframe
              v-if="embed"
              :src="embed"
              class="sf-lightbox__iframe"
              frameborder="0"
              allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share"
              allowfullscreen
            />
            <video
              v-else
              :src="mediaSrc(current.url)"
              class="sf-lightbox__media"
              controls
              autoplay
              playsinline
            />
          </div>
          <!-- Image slide -->
          <img
            v-else
            :src="mediaSrc(current.url)"
            :alt="current.altText || productName"
            class="sf-lightbox__media"
            draggable="false"
            @click.stop
          >
        </template>
      </div>

      <!-- Next -->
      <q-btn
        v-if="slides.length > 1"
        round
        flat
        size="lg"
        class="sf-lightbox__nav sf-lightbox__nav--next"
        icon="o_chevron_right"
        @click.stop="next"
      />

      <!-- Thumbnails -->
      <div v-if="slides.length > 1" class="sf-lightbox__thumbs" @click.stop>
        <img
          v-for="(img, i) in slides"
          :key="img.id ?? i"
          :src="mediaSrc(img.thumbnailUrl || img.url)"
          :alt="img.altText || productName"
          class="sf-lightbox__thumb"
          :class="{ 'sf-lightbox__thumb--active': i === index }"
          draggable="false"
          @click="index = i"
        >
      </div>
    </div>
  </q-dialog>
</template>

<script setup>
/*
 * Fullscreen media lightbox (WO-74). A maximized QDialog portal showing the
 * current gallery slide large, with prev/next (arrow buttons + keyboard
 * Left/Right + touch swipe), close on Esc / outside-click / the close button,
 * a slide counter and a thumbnail strip. Video slides render a YouTube/Vimeo
 * iframe embed when the url is recognised, otherwise a native <video>.
 */
import { ref, computed, watch, onBeforeUnmount } from 'vue'
import { mediaUrl } from 'services/api'

const props = defineProps({
  modelValue: { type: Boolean, default: false },
  // Same slide shape as ProductGalleryView: { id, url, thumbnailUrl, altText, mediaType }.
  images: { type: Array, default: () => [] },
  startIndex: { type: Number, default: 0 },
  productName: { type: String, default: '' }
})
const emit = defineEmits(['update:modelValue'])

const index = ref(0)

const slides = computed(() => props.images || [])
const current = computed(() => slides.value[index.value] || null)
const embed = computed(() => (current.value ? embedUrl(current.value.url) : null))

function mediaSrc (url) {
  return mediaUrl(url)
}

function isVideo (img) {
  return !!img && (img.mediaType === 'Video' || img.mediaType === 1)
}

// Recognise a YouTube/Vimeo link and return its iframe-embed URL, else null (direct file).
function embedUrl (url) {
  if (!url || typeof url !== 'string') return null
  const yt = url.match(/(?:youtube\.com\/(?:watch\?v=|embed\/|shorts\/|v\/)|youtu\.be\/)([\w-]{11})/i)
  if (yt) return `https://www.youtube.com/embed/${yt[1]}`
  const vm = url.match(/(?:player\.)?vimeo\.com\/(?:video\/)?(\d+)/i)
  if (vm) return `https://player.vimeo.com/video/${vm[1]}`
  return null
}

function clamp (i) {
  const n = slides.value.length
  if (!n) return 0
  return Math.max(0, Math.min(n - 1, i || 0))
}

function next () {
  const n = slides.value.length
  if (n > 1) index.value = (index.value + 1) % n
}
function prev () {
  const n = slides.value.length
  if (n > 1) index.value = (index.value - 1 + n) % n
}

// v-touch-swipe fires with { direction }: swiping left advances, right goes back.
function onSwipe ({ direction }) {
  if (direction === 'left') next()
  else if (direction === 'right') prev()
}

function close () {
  emit('update:modelValue', false)
}

function onKeydown (e) {
  // Esc is handled by QDialog itself; we add Left/Right navigation.
  if (e.key === 'ArrowRight') {
    e.preventDefault()
    next()
  } else if (e.key === 'ArrowLeft') {
    e.preventDefault()
    prev()
  }
}

// Sync the starting slide + arrow-key handling with the open state.
watch(
  () => props.modelValue,
  (open) => {
    if (open) {
      index.value = clamp(props.startIndex)
      window.addEventListener('keydown', onKeydown)
    } else {
      window.removeEventListener('keydown', onKeydown)
    }
  },
  { immediate: true }
)
watch(() => props.startIndex, (i) => { if (props.modelValue) index.value = clamp(i) })

onBeforeUnmount(() => window.removeEventListener('keydown', onKeydown))
</script>

<style scoped lang="scss">
// QDialog teleports to <body>; the scope attribute still rides along, so these
// scoped rules apply to the portalled content.
.sf-lightbox__backdrop {
  position: relative;
  width: 100%;
  height: 100%;
  background: rgba(15, 15, 18, 0.94);
  display: flex;
  align-items: center;
  justify-content: center;
  overflow: hidden;
}

.sf-lightbox__stage {
  flex: 1;
  height: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 64px 76px 96px;
}

.sf-lightbox__media {
  max-width: min(1200px, 92vw);
  max-height: 82vh;
  object-fit: contain;
  border-radius: 4px;
  user-select: none;
  box-shadow: 0 10px 40px rgba(0, 0, 0, 0.5);
}

.sf-lightbox__frame {
  width: min(1200px, 92vw);
  aspect-ratio: 16 / 9;
  max-height: 82vh;
  background: #000;
}
.sf-lightbox__iframe {
  width: 100%;
  height: 100%;
  border: 0;
}

.sf-lightbox__close {
  position: absolute;
  top: 14px;
  right: 16px;
  z-index: 4;
  color: #fff;
  background: rgba(255, 255, 255, 0.1);
}

.sf-lightbox__counter {
  position: absolute;
  top: 22px;
  left: 22px;
  z-index: 4;
  color: #fff;
  font-size: 13px;
  letter-spacing: 0.5px;
}

.sf-lightbox__nav {
  position: absolute;
  top: 50%;
  transform: translateY(-50%);
  z-index: 3;
  color: #fff;
  background: rgba(255, 255, 255, 0.08);
  transition: background 0.2s ease;

  &:hover { background: rgba(255, 255, 255, 0.2); }
  &--prev { left: 18px; }
  &--next { right: 18px; }
}

.sf-lightbox__thumbs {
  position: absolute;
  bottom: 14px;
  left: 50%;
  transform: translateX(-50%);
  z-index: 4;
  display: flex;
  gap: 8px;
  max-width: 92vw;
  overflow-x: auto;
  padding: 6px 4px;
}
.sf-lightbox__thumb {
  width: 54px;
  height: 54px;
  flex: 0 0 auto;
  object-fit: cover;
  border-radius: 3px;
  border: 2px solid transparent;
  opacity: 0.55;
  cursor: pointer;
  transition: opacity 0.2s ease, border-color 0.2s ease;

  &--active,
  &:hover { opacity: 1; }
  &--active { border-color: #fff; }
}

@media (max-width: 599px) {
  .sf-lightbox__stage { padding: 56px 12px 90px; }
  .sf-lightbox__nav--prev { left: 4px; }
  .sf-lightbox__nav--next { right: 4px; }
}
</style>
