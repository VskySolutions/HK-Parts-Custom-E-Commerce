<template>
  <q-page class="storefront-root">
    <div class="sf-container q-py-md">
      <q-banner v-if="!loading && notFound" class="bg-grey-2 rounded-borders q-my-md">
        This category was not found.
        <template #action>
          <q-btn flat no-caps color="primary" label="Back to shop" :to="{ name: 'shop-home' }" />
        </template>
      </q-banner>

      <template v-if="!notFound">
        <!-- Configurable category banner (WO-101) — above the breadcrumb/description block -->
        <div v-if="bannerImageUrl" class="sf-cat-banner q-mb-md">
          <img :src="$media(bannerImageUrl)" :alt="(category.name || 'Category') + ' banner'" class="sf-cat-banner__img">
        </div>

        <!-- Breadcrumb doubles as the page heading — no separate repeated title (REQ-CNT-012.3) -->
        <nav class="sf-crumbs" aria-label="Breadcrumb">
          <router-link class="sf-crumbs__link" :to="{ name: 'shop-home' }">
            <q-icon name="o_home" size="15px" /> Home
          </router-link>
          <q-icon name="o_chevron_right" size="16px" class="sf-crumbs__sep" />
          <h1 class="sf-crumbs__current">{{ category.name || 'Category' }}</h1>
        </nav>
        <div v-if="category.description" class="text-body2 text-grey-7 q-mb-md">{{ category.description }}</div>

        <div class="row q-col-gutter-lg">
          <!-- ===== Sidebar ===== -->
          <div class="col-12 col-md-3">
            <!-- Mobile: toggle -->
            <q-btn
              class="lt-md q-mb-md full-width"
              outline
              color="dark"
              no-caps
              icon="o_tune"
              label="Filters"
              @click="filtersOpen = !filtersOpen"
            />

            <div v-show="filtersOpen || $q.screen.gt.sm">
              <!-- Category tree -->
              <q-card flat bordered class="q-mb-md">
                <q-card-section class="text-subtitle2 text-weight-bold">Categories</q-card-section>
                <q-separator />
                <q-list dense>
                  <template v-for="top in categoryTree" :key="top.id">
                    <q-item
                      clickable
                      :active="isActive(top)"
                      active-class="text-primary text-weight-medium"
                      :to="categoryTo(top)"
                    >
                      <q-item-section>{{ top.name }}</q-item-section>
                      <q-item-section side><span class="text-caption text-grey-6">{{ top.productCount }}</span></q-item-section>
                    </q-item>
                    <template v-if="isBranchActive(top) && top.children && top.children.length">
                      <q-item
                        v-for="child in top.children"
                        :key="child.id"
                        clickable
                        class="q-pl-lg"
                        :active="isActive(child)"
                        active-class="text-primary text-weight-medium"
                        :to="categoryTo(child)"
                      >
                        <q-item-section>{{ child.name }}</q-item-section>
                        <q-item-section side><span class="text-caption text-grey-6">{{ child.productCount }}</span></q-item-section>
                      </q-item>
                    </template>
                  </template>
                </q-list>
              </q-card>

              <!-- Active filter chips -->
              <div v-if="activeChips.length" class="q-mb-md">
                <div class="row items-center q-mb-xs">
                  <span class="text-caption text-grey-7 col">Active filters</span>
                  <q-btn flat dense no-caps size="sm" color="primary" label="Clear all" @click="clearFilters" />
                </div>
                <q-chip
                  v-for="chip in activeChips"
                  :key="chip.optionId"
                  removable
                  dense
                  color="grey-3"
                  @remove="removeOption(chip.optionId)"
                >
                  {{ chip.value }}
                </q-chip>
              </div>

              <!-- Price + spec facets -->
              <q-card flat bordered class="q-pa-md">
                <FilterPanel
                  :facets="facetGroups"
                  :model-value="selectedOptionIds"
                  show-price
                  :min-price="minPrice"
                  :max-price="maxPrice"
                  @update:model-value="onFiltersChange"
                  @update:min-price="onPriceChange('min', $event)"
                  @update:max-price="onPriceChange('max', $event)"
                  @clear="clearFilters"
                />
              </q-card>
            </div>
          </div>

          <!-- ===== Main ===== -->
          <div class="col-12 col-md-9">
            <!-- Configurable promotional description (WO-101) — above the product grid -->
            <!-- eslint-disable-next-line vue/no-v-html -->
            <div v-if="promoHtml" class="storefront-rich-text sf-cat-promo q-mb-md" v-html="promoHtml" />

            <!-- Top bar: count + sort + view toggle -->
            <div class="row items-center q-mb-md q-gutter-sm">
              <div class="text-body2 text-grey-7 col">
                <span class="text-weight-medium text-dark">{{ totalCount }}</span>
                {{ totalCount === 1 ? 'product' : 'products' }}
              </div>
              <q-btn-toggle
                v-model="view"
                dense
                unelevated
                toggle-color="primary"
                :options="[
                  { value: 'grid4', icon: 'o_grid_view' },
                  { value: 'grid3', icon: 'o_grid_on' },
                  { value: 'list', icon: 'o_view_list' }
                ]"
              />
              <q-select
                v-model="sort"
                dense
                outlined
                emit-value
                map-options
                :options="sortOptions"
                label="Sort by"
                style="min-width: 190px"
                @update:model-value="onSortChange"
              />
            </div>

            <q-inner-loading :showing="loading" color="primary" />

            <div v-if="!loading && !displayItems.length" class="text-grey-6 q-py-xl text-center">
              No products match your selection.
            </div>

            <!-- Grid view — pinned products (WO-101) lead, then the regular page -->
            <div v-if="view !== 'list'" class="row q-col-gutter-md">
              <div
                v-for="p in displayItems"
                :key="p.id"
                :class="view === 'grid3' ? 'col-6 col-sm-4' : 'col-6 col-sm-4 col-md-3'"
              >
                <StorefrontProductCard :product="p" />
              </div>
            </div>

            <!-- List view -->
            <div v-else class="q-gutter-md">
              <q-card v-for="p in displayItems" :key="p.id" flat bordered class="row no-wrap sf-list-row">
                <router-link :to="productTo(p)" class="sf-list-row__media">
                  <img v-if="productImage(p)" :src="$media(productImage(p))" :alt="p.name">
                  <div v-else class="full-height row items-center justify-center bg-grey-2 text-grey-5"><q-icon name="o_image" size="36px" /></div>
                </router-link>
                <q-card-section class="col">
                  <router-link :to="productTo(p)" class="text-subtitle1 text-weight-medium sf-list-row__title">{{ p.name }}</router-link>
                  <div v-if="p.shortDescription" class="text-body2 text-grey-7 q-mt-xs sf-list-row__desc">{{ p.shortDescription }}</div>
                  <div class="row items-center q-mt-md q-gutter-md">
                    <span class="sf-card__price-now">{{ formatPrice(p.price) }}</span>
                    <button class="sf-btn sf-btn--primary" @click="quickAdd(p)">Add to Cart</button>
                  </div>
                </q-card-section>
              </q-card>
            </div>

            <div v-if="totalPages > 1" class="row justify-center q-mt-lg">
              <q-pagination
                v-model="page"
                :max="totalPages"
                :max-pages="7"
                boundary-numbers
                direction-links
                @update:model-value="onPageChange"
              />
            </div>
          </div>
        </div>

        <!-- YMAL rail (REQ-CNT-012.5) — configured YMAL collection (WO-101) when present,
             otherwise the recently-viewed fallback -->
        <div v-if="ymalDisplay.length" class="sf-section">
          <div class="sf-section__head"><h2 class="sf-section__title">You May Also Like</h2></div>
          <ProductCarousel :products="ymalDisplay" />
        </div>
      </template>
    </div>
  </q-page>
</template>

<script setup>
/*
 * Porto storefront category page (WO-111, builds on WO-19). Left sidebar =
 * category tree + price + spec-attribute facets + active-filter chips; main =
 * breadcrumbs, count, sort, grid(3/4)/list toggle, product grid, pagination, and
 * a "You May Also Like" rail. Preserves WO-19's data flow: the catalog category
 * endpoint can't filter by spec, so an active facet selection re-queries the
 * faceted search endpoint scoped to the resolved categoryId.
 *
 * WO-101 wires in the CategoryPageConfig content (GET /api/storefront/category-config/{id}):
 * a top banner (REQ-CNT-012.1), a promotional HTML description above the grid, pinned
 * products (REQ-CNT-012.2) that lead the listing ahead of the regular page regardless of
 * sort, and a configured "You May Also Like" collection. Each element renders only when
 * configured; with nothing configured the page is exactly the plain WO-111 grid, and the
 * YMAL rail falls back to recently-viewed.
 */
import { ref, computed, watch, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { useQuasar } from 'quasar'
import { storefrontApi, normalizeFacets, listingSortOptions, formatPrice, productImage, productRouteParam } from 'modules/storefront/api'
import { catalogCmsApi, sanitizeCmsHtml } from 'modules/storefront/catalog-cms-api'
import { useNotify } from 'composables/useNotify'
import { getApiErrorMessage } from 'services/api'
import { useCategories } from 'modules/storefront/composables/useCategories'
import { useRecentlyViewed } from 'modules/storefront/composables/useStorefrontStorage'
import { useCart } from 'modules/storefront/composables/useCart'
import FilterPanel from 'modules/storefront/components/FilterPanel.vue'
import StorefrontProductCard from 'modules/storefront/components/StorefrontProductCard.vue'
import ProductCarousel from 'modules/storefront/components/ProductCarousel.vue'

const route = useRoute()
const $q = useQuasar()
const notify = useNotify()
const { categories: categoryTree, loadCategories } = useCategories()
const { recentlyViewedIds } = useRecentlyViewed()
const { addItem } = useCart()

const PAGE_SIZE = 12
const sortOptions = listingSortOptions

const category = ref({})
const categoryId = ref(null)
const items = ref([])
const totalCount = ref(0)
const page = ref(1)
const sort = ref('')
const view = ref('grid4')
const selectedOptionIds = ref([])
const minPrice = ref(null)
const maxPrice = ref(null)
const baseFacets = ref([])
const searchFacets = ref([])
const loading = ref(false)
const notFound = ref(false)
const filtersOpen = ref(false)
const ymal = ref([])

// WO-101 configurable category content (banner / promo / pinned / YMAL). Keyed by
// resolved categoryId so it is fetched once per category, not on every page/sort/filter.
const categoryConfig = ref(null)
const configCategoryId = ref(null)

const totalPages = computed(() => Math.max(1, Math.ceil(totalCount.value / PAGE_SIZE)))

const bannerImageUrl = computed(() => (categoryConfig.value && categoryConfig.value.bannerImageUrl) || null)
const promoHtml = computed(() => sanitizeCmsHtml(categoryConfig.value && categoryConfig.value.promotionalDescription))
const pinnedProducts = computed(() =>
  Array.isArray(categoryConfig.value && categoryConfig.value.pinnedProducts) ? categoryConfig.value.pinnedProducts : []
)

// Pinned products lead the listing (page 1 only) ahead of the regular grid regardless of
// the active sort; de-duped so a pinned product never also appears in the normal results.
const displayItems = computed(() => {
  const pinned = page.value === 1 ? pinnedProducts.value : []
  if (!pinned.length) return items.value
  const pinnedIds = new Set(pinned.map((p) => p.id))
  return [...pinned, ...items.value.filter((p) => !pinnedIds.has(p.id))]
})

// A configured YMAL collection wins; otherwise fall back to the recently-viewed rail.
const ymalConfigured = computed(() =>
  Array.isArray(categoryConfig.value && categoryConfig.value.ymalProducts) ? categoryConfig.value.ymalProducts : []
)
const ymalDisplay = computed(() => (ymalConfigured.value.length ? ymalConfigured.value : ymal.value))

const facetGroups = computed(() => {
  if (!searchFacets.value.length) return baseFacets.value
  const counts = {}
  for (const g of searchFacets.value) {
    for (const o of g.options) counts[o.optionId] = o.count
  }
  return baseFacets.value.map((g) => ({
    ...g,
    options: g.options.map((o) => ({ ...o, count: counts[o.optionId] ?? 0 }))
  }))
})

// Map selected option ids → { optionId, value } chips using the facet universe.
const activeChips = computed(() => {
  const lookup = {}
  for (const g of facetGroups.value) {
    for (const o of g.options) lookup[o.optionId] = o.value
  }
  return selectedOptionIds.value.map((id) => ({ optionId: id, value: lookup[id] || 'Filter' }))
})

function categoryTo (cat) {
  return { name: 'shop-category', params: { idOrSlug: cat.slug || cat.id } }
}
function productTo (p) {
  return { name: 'shop-product', params: { idOrSlug: productRouteParam(p) } }
}

const activeKey = computed(() => route.params.idOrSlug)
function isActive (cat) {
  return String(cat.id) === String(activeKey.value) || (cat.slug && cat.slug === activeKey.value)
}
function isBranchActive (top) {
  if (isActive(top)) return true
  return (top.children || []).some((c) => isActive(c))
}

async function loadCategory () {
  loading.value = true
  notFound.value = false
  try {
    const res = await storefrontApi.categoryPage(route.params.idOrSlug, {
      page: page.value,
      pageSize: PAGE_SIZE,
      sort: sort.value || undefined
    })
    category.value = res || {}
    categoryId.value = res?.categoryId || null
    baseFacets.value = normalizeFacets(res?.filters)
    searchFacets.value = []
    items.value = Array.isArray(res?.items) ? res.items : []
    totalCount.value = res?.totalCount || 0
    // Fetch the configurable content once the category id is known (guarded per category).
    loadCategoryConfig()
  } catch (err) {
    notFound.value = true
    category.value = {}
    items.value = []
    totalCount.value = 0
    notify.error(getApiErrorMessage(err))
  } finally {
    loading.value = false
  }
}

// WO-101: load the category's configurable content (banner/promo/pinned/YMAL). Guarded
// so it fetches only when the resolved categoryId changes; degrades to a plain grid on
// an unconfigured category or a missing endpoint.
async function loadCategoryConfig () {
  const id = categoryId.value
  if (!id) {
    categoryConfig.value = null
    configCategoryId.value = null
    return
  }
  if (configCategoryId.value === id) return
  configCategoryId.value = id
  try {
    categoryConfig.value = await catalogCmsApi.categoryConfig(id)
  } catch (e) {
    categoryConfig.value = null
  }
}

// Re-query the faceted search when any spec option / price filter is active.
async function runQuery () {
  const hasFilters = selectedOptionIds.value.length || minPrice.value != null || maxPrice.value != null
  if (!hasFilters) {
    await loadCategory()
    return
  }
  loading.value = true
  try {
    const res = await storefrontApi.search({
      categoryId: categoryId.value,
      specificationOptionIds: selectedOptionIds.value,
      minPrice: minPrice.value ?? undefined,
      maxPrice: maxPrice.value ?? undefined,
      sort: sort.value || undefined,
      page: page.value,
      pageSize: PAGE_SIZE
    })
    items.value = Array.isArray(res?.items) ? res.items : []
    totalCount.value = res?.totalCount || 0
    searchFacets.value = normalizeFacets(res?.facets)
  } catch (err) {
    items.value = []
    totalCount.value = 0
    notify.error(getApiErrorMessage(err))
  } finally {
    loading.value = false
  }
}

function onFiltersChange (ids) {
  selectedOptionIds.value = ids
  page.value = 1
  runQuery()
}
function onPriceChange (which, val) {
  if (which === 'min') minPrice.value = val
  else maxPrice.value = val
  page.value = 1
  runQuery()
}
function removeOption (optionId) {
  selectedOptionIds.value = selectedOptionIds.value.filter((id) => id !== optionId)
  page.value = 1
  runQuery()
}
function clearFilters () {
  selectedOptionIds.value = []
  minPrice.value = null
  maxPrice.value = null
  page.value = 1
  loadCategory()
}
function onSortChange () {
  page.value = 1
  runQuery()
}
function onPageChange () {
  runQuery()
  if (typeof window !== 'undefined') window.scrollTo({ top: 0, behavior: 'smooth' })
}

async function quickAdd (p) {
  try {
    await addItem({ productId: p.id, quantity: 1 })
    $q.notify({ type: 'positive', message: `${p.name} added to cart`, timeout: 1500 })
  } catch (e) {
    $q.notify({ type: 'info', message: 'Please choose options for this product.' })
  }
}

async function loadYmal () {
  const ids = (recentlyViewedIds.value || []).slice(0, 8)
  if (!ids.length) { ymal.value = []; return }
  try {
    ymal.value = await storefrontApi.recentlyViewed(ids)
  } catch (e) {
    ymal.value = []
  }
}

watch(
  () => route.params.idOrSlug,
  () => {
    page.value = 1
    sort.value = ''
    selectedOptionIds.value = []
    minPrice.value = null
    maxPrice.value = null
    searchFacets.value = []
    categoryConfig.value = null
    configCategoryId.value = null
    loadCategory()
  }
)

onMounted(() => {
  loadCategories()
  loadCategory()
  loadYmal()
})
</script>

<style scoped lang="scss">
.sf-list-row { overflow: hidden; }
.sf-list-row__media {
  width: 200px;
  min-height: 160px;
  flex: 0 0 200px;
  img { width: 100%; height: 100%; object-fit: contain; display: block; }
}
.sf-list-row__title { text-decoration: none; color: var(--sf-heading); }
.sf-list-row__title:hover { color: var(--sf-accent); }
.sf-list-row__desc {
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

/* WO-101 configurable category banner */
.sf-cat-banner__img {
  display: block;
  width: 100%;
  height: auto;
  border-radius: 6px;
}
/* WO-101 promotional description sits above the grid */
.sf-cat-promo { color: var(--sf-body, inherit); }

@media (max-width: 599px) {
  .sf-list-row__media { width: 120px; flex-basis: 120px; }
}
</style>
