<template>
  <div class="product-gallery">
    <q-carousel
      v-if="displayImages.length"
      v-model="slide"
      class="product-gallery__main rounded-borders bg-grey-1"
      swipeable
      animated
      :arrows="displayImages.length > 1"
      :navigation="false"
      control-color="primary"
      height="420px"
    >
      <q-carousel-slide
        v-for="img in displayImages"
        :key="img.id"
        :name="img.id"
        class="column no-wrap flex-center q-pa-none"
      >
        <!--
          Stage wrapper hosts the storefront-only WO-74 behaviours (hover zoom +
          click-to-open lightbox), both gated behind the `zoom` / `lightbox` props
          so admin callers (which omit them) are visually and behaviourally
          unchanged — they just get a transparent full-size wrapper.
        -->
        <div
          class="product-gallery__stage"
          :class="{
            'product-gallery__stage--clickable': lightbox,
            'product-gallery__stage--zoom': zoom && !isVideo(img)
          }"
          @mouseenter="onZoomEnter(img)"
          @mousemove="onZoomMove"
          @mouseleave="onZoomLeave"
          @click="openLightbox(img)"
        >
          <!-- Video embed entry -->
          <q-video
            v-if="isVideo(img)"
            :ratio="16 / 9"
            :src="$media(img.url)"
            class="product-gallery__video"
          />
          <!-- Image entry: QImg lazy-loads via IntersectionObserver -->
          <q-img
            v-else
            :src="$media(img.url)"
            :alt="img.altText || productName"
            fit="contain"
            loading="lazy"
            class="product-gallery__image fit"
            no-spinner
          />

          <!-- Hover magnifier: an inner-zoom overlay following the cursor (desktop only). -->
          <div
            v-if="zoomActive && zoomImg && zoomImg.id === img.id"
            class="product-gallery__zoom"
            :style="zoomStyle"
          />

          <!-- Fullscreen affordance — always reachable, incl. on video slides. -->
          <q-btn
            v-if="lightbox"
            round
            dense
            unelevated
            class="product-gallery__expand"
            icon="o_fullscreen"
            color="white"
            text-color="dark"
            size="sm"
            @click.stop="openLightbox(img)"
          >
            <q-tooltip>View fullscreen</q-tooltip>
          </q-btn>
        </div>
      </q-carousel-slide>
    </q-carousel>

    <div
      v-else
      class="product-gallery__main product-gallery__empty rounded-borders bg-grey-1 column flex-center text-grey-6"
    >
      <q-icon name="o_image" size="64px" />
      <div class="q-mt-sm">No images</div>
    </div>

    <!-- Thumbnail strip -->
    <div v-if="displayImages.length > 1" class="product-gallery__thumbs row no-wrap q-gutter-sm q-mt-sm">
      <div
        v-for="img in displayImages"
        :key="`thumb-${img.id}`"
        class="product-gallery__thumb cursor-pointer rounded-borders"
        :class="{ 'product-gallery__thumb--active': img.id === slide }"
        @click="select(img.id)"
      >
        <q-img
          :src="$media(img.thumbnailUrl || img.url)"
          :alt="img.altText || productName"
          fit="cover"
          loading="lazy"
          width="64px"
          height="64px"
          class="rounded-borders"
        >
          <q-icon
            v-if="img.mediaType === 'Video' || img.mediaType === 1"
            name="o_play_circle"
            size="24px"
            class="absolute-center text-white"
          />
        </q-img>
      </div>
    </div>

    <!-- Fullscreen lightbox (storefront only; never mounted for admin callers). -->
    <GalleryLightbox
      v-if="lightbox"
      v-model="lightboxOpen"
      :images="displayImages"
      :start-index="lightboxIndex"
      :product-name="productName"
    />
  </div>
</template>

<script setup>
import { ref, computed, watch } from 'vue'
import { mediaUrl } from 'services/api'
import GalleryLightbox from 'modules/storefront/components/GalleryLightbox.vue'

const props = defineProps({
  // Full media set for the product. Each: { id, url, thumbnailUrl, altText, mediaType, displayOrder, productVariantId }
  images: { type: Array, default: () => [] },
  // Currently selected variant id; when set, the gallery shows that variant's media (AC-CAT-PZG-001.3).
  variantId: { type: [String, Number, null], default: null },
  productName: { type: String, default: '' },
  // WO-74 (storefront opt-in). Admin callers omit both → behaviour unchanged.
  // `zoom`: desktop hover magnifier on the main image. `lightbox`: click to open a fullscreen viewer.
  zoom: { type: Boolean, default: false },
  lightbox: { type: Boolean, default: false }
})

const slide = ref(null)

// Images shown: variant-specific media when a variant is selected and has its own images,
// otherwise fall back to product-level media (productVariantId == null). Sorted by displayOrder.
const displayImages = computed(() => {
  const all = [...(props.images || [])].sort(
    (a, b) => (a.displayOrder ?? 0) - (b.displayOrder ?? 0)
  )
  if (props.variantId != null) {
    const variantImages = all.filter((i) => i.productVariantId === props.variantId)
    if (variantImages.length) return variantImages
  }
  const productLevel = all.filter((i) => i.productVariantId == null)
  return productLevel.length ? productLevel : all
})

// ---- WO-74 zoom + lightbox --------------------------------------------------
const ZOOM_SCALE = 2.5

const lightboxOpen = ref(false)
const lightboxIndex = ref(0)

const zoomActive = ref(false)
const zoomImg = ref(null)
const zoomStyle = ref({})

function isVideo (img) {
  return !!img && (img.mediaType === 'Video' || img.mediaType === 1)
}

function isTouch () {
  return typeof window !== 'undefined' &&
    (('ontouchstart' in window) || (navigator.maxTouchPoints || 0) > 0)
}

function onZoomEnter (img) {
  // Desktop, images only — never on touch devices or video slides.
  if (!props.zoom || isVideo(img) || isTouch()) return
  zoomImg.value = img
  zoomActive.value = true
}

function onZoomMove (e) {
  if (!zoomActive.value || !zoomImg.value) return
  const rect = e.currentTarget.getBoundingClientRect()
  if (!rect.width || !rect.height) return
  const x = Math.max(0, Math.min(100, ((e.clientX - rect.left) / rect.width) * 100))
  const y = Math.max(0, Math.min(100, ((e.clientY - rect.top) / rect.height) * 100))
  zoomStyle.value = {
    backgroundImage: `url("${mediaUrl(zoomImg.value.url)}")`,
    backgroundSize: `${ZOOM_SCALE * 100}%`,
    backgroundPosition: `${x}% ${y}%`
  }
}

function onZoomLeave () {
  if (!zoomActive.value) return
  zoomActive.value = false
  zoomImg.value = null
}

function openLightbox (img) {
  if (!props.lightbox) return
  const idx = displayImages.value.findIndex((i) => i.id === img.id)
  lightboxIndex.value = idx >= 0 ? idx : 0
  lightboxOpen.value = true
}

function select (id) {
  slide.value = id
}

// Reset to the first image whenever the visible set changes (variant switch or data load).
watch(
  displayImages,
  (imgs) => {
    if (!imgs.length) {
      slide.value = null
      return
    }
    if (!imgs.some((i) => i.id === slide.value)) {
      slide.value = imgs[0].id
    }
  },
  { immediate: true }
)
</script>

<style scoped lang="scss">
.product-gallery__main {
  width: 100%;
}

// Slide stage: fills the carousel slide and centres the media (matches the
// previous flex-center layout, so admin — which passes no zoom/lightbox — is
// visually identical).
.product-gallery__stage {
  position: relative;
  width: 100%;
  height: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
}
.product-gallery__stage--clickable { cursor: pointer; }
.product-gallery__stage--zoom { cursor: zoom-in; }

// Inner-zoom overlay: a magnified copy of the image, positioned to follow the
// cursor. pointer-events:none keeps mousemove/click flowing to the stage.
.product-gallery__zoom {
  position: absolute;
  inset: 0;
  background-repeat: no-repeat;
  background-color: #fff;
  pointer-events: none;
  z-index: 2;
}

.product-gallery__expand {
  position: absolute;
  top: 10px;
  right: 10px;
  z-index: 3;
  opacity: 0.85;
  transition: opacity 0.2s ease;
  &:hover { opacity: 1; }
}

.product-gallery__empty {
  height: 420px;
}

.product-gallery__thumbs {
  overflow-x: auto;
  padding-bottom: 4px;
}

.product-gallery__thumb {
  border: 2px solid transparent;
  transition: border-color 0.2s ease;
  flex: 0 0 auto;

  &--active {
    border-color: var(--q-primary);
  }
}
</style>
