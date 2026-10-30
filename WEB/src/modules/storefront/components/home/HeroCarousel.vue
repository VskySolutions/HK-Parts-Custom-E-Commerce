<template>
  <q-carousel
    v-model="slide"
    animated
    infinite
    :autoplay="4000"
    transition-prev="fade"
    transition-next="fade"
    swipeable
    arrows
    navigation
    control-color="primary"
    height="clamp(280px, 42vw, 520px)"
    class="sf-hero"
  >
    <q-carousel-slide
      v-for="(s, i) in slides"
      :key="i"
      :name="i"
      class="sf-hero__slide"
      :style="{ background: s.background }"
    >
      <div class="sf-container full-height column justify-center">
        <div class="sf-hero__content" :class="`text-${s.align || 'left'}`">
          <div v-if="s.label" class="sf-hero__label">{{ s.label }}</div>
          <div class="sf-hero__title">{{ s.title }}</div>
          <div v-if="s.subtitle" class="sf-hero__subtitle">{{ s.subtitle }}</div>
          <div class="sf-hero__cta q-gutter-sm q-mt-md">
            <q-btn v-if="s.primaryTo" unelevated color="primary" no-caps :label="s.primaryLabel" :to="s.primaryTo" />
            <q-btn v-if="s.secondaryTo" outline color="primary" no-caps :label="s.secondaryLabel" :to="s.secondaryTo" />
          </div>
        </div>
      </div>
    </q-carousel-slide>
  </q-carousel>
</template>

<script setup>
/*
 * Porto hero carousel (WO-110). A Banner/hero-slide API does not exist yet
 * (flagged on WO-110), so slides are derived from the top categories (real,
 * clickable content) over branded gradients, with a branded welcome slide as the
 * lead. When a Banner system lands, feed its slides in via the `slides` prop.
 */
import { ref, computed, onMounted } from 'vue'
import { useCategories } from 'modules/storefront/composables/useCategories'
import { useStorefront } from 'modules/storefront/composables/useStorefront'

const props = defineProps({
  slides: { type: Array, default: null }
})

const slide = ref(0)
const { categories, loadCategories } = useCategories()
const { branding, loadBranding } = useStorefront()

// Soft, settled light gradients — the brand colour comes through the CTAs, not the backdrop.
const gradients = [
  'linear-gradient(120deg, #f5f6fb 0%, #e8ebf6 100%)',
  'linear-gradient(120deg, #f8f3f7 0%, #eee2ee 100%)',
  'linear-gradient(120deg, #eff5f4 0%, #e0efec 100%)'
]

const slides = computed(() => {
  if (props.slides && props.slides.length) return props.slides

  const out = [{
    label: 'Welcome',
    title: `Discover ${branding.value?.brandName || 'our store'}`,
    subtitle: branding.value?.tagline || 'Quality products, delivered.',
    align: 'left',
    background: gradients[0],
    primaryLabel: 'Shop all',
    primaryTo: { name: 'shop-search' }
  }]

  categories.value.slice(0, 3).forEach((cat, i) => {
    out.push({
      label: 'Featured category',
      title: cat.name,
      subtitle: cat.productCount ? `${cat.productCount} products to explore` : 'Explore the collection',
      align: i % 2 === 0 ? 'left' : 'right',
      background: gradients[(i + 1) % gradients.length],
      primaryLabel: 'Shop now',
      primaryTo: { name: 'shop-category', params: { idOrSlug: cat.slug || cat.id } }
    })
  })

  return out
})

onMounted(() => {
  loadBranding()
  loadCategories()
})
</script>

<style scoped lang="scss">
.sf-hero { border-radius: 0; }
.sf-hero__slide { padding: 0; color: var(--sf-heading); }
.sf-hero__content { max-width: 560px; }
.text-right .sf-hero__content,
.sf-hero__content.text-right { margin-left: auto; }
.sf-hero__label {
  text-transform: uppercase;
  letter-spacing: 2px;
  font-size: 13px;
  font-weight: 600;
  opacity: 0.85;
  margin-bottom: 10px;
}
.sf-hero__title { font-size: clamp(28px, 4.5vw, 52px); font-weight: 800; line-height: 1.05; }
.sf-hero__subtitle { font-size: clamp(15px, 2vw, 20px); margin-top: 12px; opacity: 0.9; }
</style>
