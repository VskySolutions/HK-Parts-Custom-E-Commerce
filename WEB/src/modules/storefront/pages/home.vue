<template>
  <q-page class="storefront-root">
    <!-- Loading skeleton -->
    <template v-if="loading">
      <div class="sf-section">
        <div class="sf-container">
          <q-skeleton height="clamp(280px, 42vw, 520px)" class="sf-skeleton-card" />
        </div>
      </div>
      <div class="sf-section">
        <div class="sf-container">
          <div class="row q-col-gutter-md">
            <div v-for="n in 4" :key="n" class="col-6 col-md-3">
              <q-skeleton height="260px" class="sf-skeleton-card" />
            </div>
          </div>
        </div>
      </div>
    </template>

    <!-- Dynamic Home Page Sections (CMSHomePageSection), rendered in order by type -->
    <template v-else-if="visibleSections.length">
      <template v-for="(section, index) in visibleSections" :key="section.id ?? index">
        <!-- Hero Banner -->
        <HeroCarousel
          v-if="section.type === 'HeroBanner'"
          :slides="toHeroSlides(section.banners)"
        />

        <!-- Featured Categories -->
        <CategoryGrid
          v-else-if="section.type === 'FeaturedCategories'"
          :categories="section.categories"
          :heading="section.displayName || 'Shop by Category'"
          :limit="(section.categories && section.categories.length) || 8"
        />

        <!-- Product Row (collection or auto-generated) -->
        <div v-else-if="section.type === 'ProductRow'" class="sf-section">
          <div class="sf-container">
            <div class="sf-section__head">
              <h2 class="sf-section__title">{{ section.displayName || 'Products' }}</h2>
            </div>
            <ProductCarousel :products="section.products" />
          </div>
        </div>

        <!-- Blog Posts Row -->
        <BlogPostsRow
          v-else-if="section.type === 'BlogPostsRow'"
          :heading="section.displayName || 'From the Blog'"
          :posts="section.posts"
        />

        <!-- Custom HTML Block -->
        <CustomHtmlBlock
          v-else-if="section.type === 'CustomHtmlBlock'"
          :html="section.html"
        />
      </template>
    </template>

    <!-- Empty state / fetch failure — never blank (WO-100 AC-CNT-008.6).
         Falls back to an empty hero + featured categories over the static trust
         bar and newsletter, each degrading gracefully on its own. -->
    <template v-else>
      <HeroCarousel />
      <FeatureBar />
      <CategoryGrid :limit="8" />
      <NewsletterStrip />
    </template>

    <!-- Recently viewed (client-side history) — a personalisation strip appended
         below the CMS content, independent of the configured sections. -->
    <div v-if="!loading && showRecent" class="sf-section">
      <div class="sf-container">
        <RecentlyViewed />
      </div>
    </div>
  </q-page>
</template>

<script setup>
/*
 * Dynamic storefront home page (WO-100).
 *
 * Fetches the enabled Home Page Sections (GET /api/storefront/home) on mount and
 * renders each one IN ORDER by `type`:
 *   HeroBanner        -> HeroCarousel (banners mapped to its slide shape)
 *   FeaturedCategories-> CategoryGrid (injected categories, imageUrl when present)
 *   ProductRow        -> ProductCarousel under a .sf-section head (displayName)
 *   BlogPostsRow      -> BlogPostsRow rail
 *   CustomHtmlBlock   -> CustomHtmlBlock (sanitised HTML)
 *
 * Sections whose payload resolves to nothing (empty product/blog/html) self-skip.
 * When NO section renders — zero configured sections OR a failed fetch — a
 * graceful hero + featured-categories fallback keeps the page from ever being
 * blank (AC-CNT-008.6). Each underlying section component keeps its own derived
 * fallback for when its data source is empty.
 */
import { ref, computed, onMounted } from 'vue'
import { mediaUrl } from 'services/api'
import { homeApi } from 'modules/storefront/home-api'
import { useRecentlyViewed } from 'modules/storefront/composables/useStorefrontStorage'
import HeroCarousel from 'modules/storefront/components/home/HeroCarousel.vue'
import FeatureBar from 'modules/storefront/components/home/FeatureBar.vue'
import CategoryGrid from 'modules/storefront/components/home/CategoryGrid.vue'
import BlogPostsRow from 'modules/storefront/components/home/BlogPostsRow.vue'
import CustomHtmlBlock from 'modules/storefront/components/home/CustomHtmlBlock.vue'
import NewsletterStrip from 'modules/storefront/components/home/NewsletterStrip.vue'
import ProductCarousel from 'modules/storefront/components/ProductCarousel.vue'
import RecentlyViewed from 'modules/storefront/components/RecentlyViewed.vue'

const sections = ref([])
const loading = ref(true)

const { recentlyViewedIds } = useRecentlyViewed()
const showRecent = computed(() => (recentlyViewedIds.value || []).length > 0)

// Branded gradient backdrops for hero banners without an image.
const HERO_GRADIENTS = [
  'linear-gradient(120deg, #f5f6fb 0%, #e8ebf6 100%)',
  'linear-gradient(120deg, #f8f3f7 0%, #eee2ee 100%)',
  'linear-gradient(120deg, #eff5f4 0%, #e0efec 100%)'
]

// A section is renderable when it carries content. HeroBanner/FeaturedCategories
// always render (they have derived fallbacks), the content rows only when non-empty.
// Unknown types are skipped. Used to decide the empty-state fallback.
function sectionHasContent (s) {
  if (!s || typeof s !== 'object') return false
  switch (s.type) {
    case 'HeroBanner':
    case 'FeaturedCategories':
      return true
    case 'ProductRow':
      return Array.isArray(s.products) && s.products.length > 0
    case 'BlogPostsRow':
      return Array.isArray(s.posts) && s.posts.length > 0
    case 'CustomHtmlBlock':
      return typeof s.html === 'string' && s.html.trim().length > 0
    default:
      return false
  }
}

const visibleSections = computed(() => (sections.value || []).filter(sectionHasContent))

// Map HeroBanner banners ({ title, subtitle, imageUrl, linkUrl, ctaLabel }) onto the
// HeroCarousel slide shape. The image becomes the slide backdrop under a light scrim
// (the hero text is dark), falling back to a branded gradient when imageless. Returns
// null for an empty list so HeroCarousel uses its own category-derived fallback.
function toHeroSlides (banners) {
  if (!Array.isArray(banners) || !banners.length) return null
  return banners.map((b, i) => ({
    title: b.title,
    subtitle: b.subtitle,
    align: 'left',
    background: b.imageUrl
      ? `linear-gradient(rgba(255,255,255,0.55), rgba(255,255,255,0.35)), url("${mediaUrl(b.imageUrl)}") center / cover no-repeat`
      : HERO_GRADIENTS[i % HERO_GRADIENTS.length],
    primaryLabel: b.ctaLabel || (b.linkUrl ? 'Shop now' : null),
    // linkUrl is treated as an internal storefront path (e.g. /shop/category/x);
    // HeroCarousel binds it via router-link :to.
    primaryTo: b.linkUrl || null
  }))
}

async function load () {
  loading.value = true
  try {
    const payload = await homeApi.get()
    sections.value = Array.isArray(payload?.sections) ? payload.sections : []
  } catch (e) {
    // Endpoint unavailable / error: leave sections empty so the graceful fallback
    // renders rather than an error (AC-CNT-008.6).
    sections.value = []
  } finally {
    loading.value = false
  }
}

onMounted(load)
</script>
