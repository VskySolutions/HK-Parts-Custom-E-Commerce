<template>
  <div v-if="cards.length" class="sf-section">
    <div class="sf-container">
      <div class="sf-section__head"><h2 class="sf-section__title">{{ heading }}</h2></div>
      <div class="row q-col-gutter-md">
        <div v-for="cat in cards" :key="cat.id" class="col-6 col-md-3">
          <router-link :to="categoryTo(cat)" class="sf-cat-card" :class="{ 'sf-cat-card--image': cat.imageUrl }">
            <img
              v-if="cat.imageUrl"
              :src="$media(cat.imageUrl)"
              :alt="cat.name"
              class="sf-cat-card__img"
              loading="lazy"
            >
            <div class="sf-cat-card__overlay">
              <div class="sf-cat-card__name">{{ cat.name }}</div>
              <div v-if="cat.productCount" class="sf-cat-card__count">{{ cat.productCount }} products</div>
              <span class="sf-cat-card__link">Shop Now <q-icon name="o_arrow_forward" size="14px" /></span>
            </div>
          </router-link>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
/*
 * Porto featured-categories grid (WO-110 / WO-100). Renders either an injected
 * FeaturedCategories home-section payload ([{ id, name, slug, imageUrl }] — cards
 * show the image when present) or, as a fallback, the public category tree over
 * branded surfaces.
 */
import { computed, onMounted } from 'vue'
import { useCategories } from 'modules/storefront/composables/useCategories'

const props = defineProps({
  limit: { type: Number, default: 8 },
  // Injected FeaturedCategories section payload (WO-100). When absent/empty, the
  // grid falls back to the public category tree so it is never blank.
  categories: { type: Array, default: null },
  heading: { type: String, default: 'Shop by Category' }
})

const { categories: categoryTree, loadCategories } = useCategories()

const injected = computed(() => (Array.isArray(props.categories) && props.categories.length ? props.categories : null))
const cards = computed(() => (injected.value || categoryTree.value || []).slice(0, props.limit))

function categoryTo (cat) { return { name: 'shop-category', params: { idOrSlug: cat.slug || cat.id } } }

// Only load the public tree when no categories were injected.
onMounted(() => { if (!injected.value) loadCategories() })
</script>

<style scoped lang="scss">
.sf-cat-card {
  display: block;
  position: relative;
  aspect-ratio: 4 / 3;
  border-radius: var(--sf-radius);
  overflow: hidden;
  text-decoration: none;
  background: var(--sf-surface-alt);
  border: 1px solid var(--sf-border);
  transition: transform var(--sf-transition), box-shadow var(--sf-transition), border-color var(--sf-transition);
}
.sf-cat-card:hover { transform: translateY(-4px); box-shadow: var(--sf-shadow-hover); border-color: var(--sf-accent); }
.sf-cat-card__overlay {
  position: absolute;
  inset: 0;
  display: flex;
  flex-direction: column;
  justify-content: center;
  align-items: center;
  text-align: center;
  padding: 18px;
  color: var(--sf-heading);
}
.sf-cat-card__name { font-size: 18px; font-weight: 700; }
.sf-cat-card__count { font-size: 12px; color: var(--sf-muted); margin-top: 2px; }
.sf-cat-card__link {
  margin-top: 10px;
  font-size: 12px;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  color: var(--sf-accent);
  display: inline-flex;
  align-items: center;
  gap: 4px;
}

// ---- Image-backed cards (injected FeaturedCategories with imageUrl) ----
.sf-cat-card__img {
  position: absolute;
  inset: 0;
  width: 100%;
  height: 100%;
  object-fit: cover;
  transition: transform var(--sf-transition);
}
.sf-cat-card:hover .sf-cat-card__img { transform: scale(1.05); }
.sf-cat-card--image .sf-cat-card__overlay {
  justify-content: flex-end;
  align-items: flex-start;
  text-align: left;
  color: #fff;
  background: linear-gradient(to top, rgba(0, 0, 0, 0.6), rgba(0, 0, 0, 0.15) 55%, rgba(0, 0, 0, 0));
}
.sf-cat-card--image .sf-cat-card__name { color: #fff; }
.sf-cat-card--image .sf-cat-card__count { color: rgba(255, 255, 255, 0.85); }
.sf-cat-card--image .sf-cat-card__link { color: #fff; }
</style>
