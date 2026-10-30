<template>
  <q-page class="q-pa-md storefront-container storefront-root">
    <div class="q-mb-md">
      <!-- Heading: configured heading (WO-105) wins, else the original default copy -->
      <h1 class="text-h5 text-weight-bold q-my-none">
        <template v-if="content.heading">{{ content.heading }}<span v-if="query"> &ldquo;{{ query }}&rdquo;</span></template>
        <template v-else-if="query">Results for &ldquo;{{ query }}&rdquo;</template>
        <template v-else>All products</template>
      </h1>

      <!-- In-page search refine box — uses the configured placeholder (WO-105) -->
      <q-input
        v-model="queryInput"
        outlined
        dense
        clearable
        class="sf-search__input q-mt-sm"
        :placeholder="placeholderText"
        @keyup.enter="submitSearch"
        @clear="submitSearch"
      >
        <template #append>
          <q-btn flat dense round icon="o_search" aria-label="Search" @click="submitSearch" />
        </template>
      </q-input>
    </div>

    <div class="row q-col-gutter-lg">
      <!-- Filters -->
      <div class="col-12 col-md-3">
        <q-card flat bordered class="q-pa-md">
          <FilterPanel
            :facets="facetGroups"
            :model-value="selectedOptionIds"
            show-price
            :min-price="minPrice"
            :max-price="maxPrice"
            @update:model-value="onFiltersChange"
            @update:min-price="(v) => onPriceChange('min', v)"
            @update:max-price="(v) => onPriceChange('max', v)"
          />
        </q-card>
      </div>

      <!-- Results -->
      <div class="col-12 col-md-9">
        <div class="row items-center q-mb-md">
          <div class="text-body2 text-grey-7 col">
            <!-- Configured results-count label (WO-105), {count} token replaced with the total -->
            <template v-if="content.resultsCountLabel">{{ resultsCountText }}</template>
            <template v-else>
              <span class="text-weight-medium text-dark">{{ totalCount }}</span>
              {{ totalCount === 1 ? 'result' : 'results' }}
            </template>
          </div>
          <q-select
            v-model="sort"
            dense
            outlined
            emit-value
            map-options
            :options="sortOptions"
            label="Sort by"
            style="min-width: 200px"
            @update:model-value="onSortChange"
          />
        </div>

        <q-inner-loading :showing="loading" color="primary" />

        <!-- No results -->
        <div v-if="!loading && !items.length" class="q-py-xl">
          <div class="column flex-center text-center text-grey-6">
            <q-icon name="o_search_off" size="64px" class="q-mb-md" />
            <div class="text-h6 text-grey-8">No products found</div>
            <!-- Configured no-results message (WO-105, HTML) or the original default copy -->
            <!-- eslint-disable-next-line vue/no-v-html -->
            <div v-if="noResultsHtml" class="q-mt-xs storefront-rich-text" v-html="noResultsHtml" />
            <div v-else class="q-mt-xs">
              We couldn't find anything matching your search. Try different keywords or clear your filters.
            </div>
            <q-btn
              v-if="hasFilters"
              flat
              no-caps
              color="primary"
              label="Clear filters"
              class="q-mt-md"
              @click="clearFilters"
            />
          </div>

          <!-- No-results promotional banner (WO-105) — image + CTA, links out when configured -->
          <component
            :is="bannerTag"
            v-if="noResultsBanner"
            v-bind="bannerBind"
            class="sf-noresults-banner q-mt-xl"
          >
            <img
              v-if="noResultsBanner.imageUrl"
              :src="$media(noResultsBanner.imageUrl)"
              :alt="noResultsBanner.title || 'Promotion'"
              class="sf-noresults-banner__img"
            >
            <div class="sf-noresults-banner__body">
              <div v-if="noResultsBanner.title" class="sf-noresults-banner__title">{{ noResultsBanner.title }}</div>
              <div v-if="noResultsBanner.subtitle" class="sf-noresults-banner__subtitle">{{ noResultsBanner.subtitle }}</div>
              <span v-if="noResultsBanner.ctaLabel" class="sf-btn sf-btn--primary q-mt-sm">{{ noResultsBanner.ctaLabel }}</span>
            </div>
          </component>

          <!-- No-results product rail (WO-105) -->
          <div v-if="noResultsProducts.length" class="sf-section q-mt-xl">
            <div class="sf-section__head"><h2 class="sf-section__title">You might also like</h2></div>
            <ProductCarousel :products="noResultsProducts" />
          </div>
        </div>

        <div class="row q-col-gutter-md">
          <div
            v-for="p in items"
            :key="p.id"
            class="col-6 col-sm-4 col-md-3"
          >
            <StorefrontProductCard :product="p" />
          </div>
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
  </q-page>
</template>

<script setup>
/*
 * Storefront search results (WO-19, AC-STF-002). Reads the `?q=` query, calls the
 * faceted search endpoint and renders a results grid with a live match count, a
 * facet FilterPanel (spec attributes + price range), sort and pagination. Shows a
 * friendly no-results state; the returned facets are surfaced for refinement.
 *
 * WO-105 layers on configurable content (GET /api/storefront/search-content):
 * a custom heading, the refine-box placeholder, a results-count label (with a
 * {count} token), and — when there are no results — a custom HTML message plus an
 * optional promotional banner and product rail. Falls back to the original copy
 * when the content endpoint is unavailable.
 */
import { ref, computed, watch, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { storefrontApi, normalizeFacets, searchSortOptions } from 'modules/storefront/api'
import { catalogCmsApi, sanitizeCmsHtml } from 'modules/storefront/catalog-cms-api'
import { useNotify } from 'composables/useNotify'
import { getApiErrorMessage } from 'services/api'
import StorefrontProductCard from 'modules/storefront/components/StorefrontProductCard.vue'
import FilterPanel from 'modules/storefront/components/FilterPanel.vue'
import ProductCarousel from 'modules/storefront/components/ProductCarousel.vue'

const route = useRoute()
const router = useRouter()
const notify = useNotify()

const PAGE_SIZE = 12
const sortOptions = searchSortOptions

const query = ref('')
const queryInput = ref('')
const items = ref([])
const totalCount = ref(0)
const page = ref(1)
const sort = ref('relevance')
const selectedOptionIds = ref([])
const minPrice = ref(null)
const maxPrice = ref(null)
const facetGroups = ref([])
const loading = ref(false)

// WO-105 configurable search-page content (heading, placeholder, count label, no-results promo).
const content = ref({})

const totalPages = computed(() => Math.max(1, Math.ceil(totalCount.value / PAGE_SIZE)))

const hasFilters = computed(
  () => selectedOptionIds.value.length > 0 || minPrice.value != null || maxPrice.value != null
)

const placeholderText = computed(() => content.value.placeholderText || 'Search products…')

// Configured count label; a {count} token is replaced with the total, else the total is prefixed.
const resultsCountText = computed(() => {
  const label = content.value.resultsCountLabel || ''
  return label.includes('{count}')
    ? label.replace(/\{count\}/g, String(totalCount.value))
    : `${totalCount.value} ${label}`.trim()
})

const noResultsHtml = computed(() => sanitizeCmsHtml(content.value.noResultsMessage))
const noResultsBanner = computed(() => content.value.noResultsBanner || null)
const noResultsProducts = computed(() =>
  Array.isArray(content.value.noResultsProducts) ? content.value.noResultsProducts : []
)

// The no-results banner links out when configured: an internal path uses <router-link>,
// an absolute URL a plain <a>, and no link renders as a plain <div>.
const bannerTag = computed(() => {
  const url = noResultsBanner.value && noResultsBanner.value.linkUrl
  if (!url) return 'div'
  return url.startsWith('/') ? 'router-link' : 'a'
})
const bannerBind = computed(() => {
  const url = noResultsBanner.value && noResultsBanner.value.linkUrl
  if (!url) return {}
  return url.startsWith('/') ? { to: url } : { href: url, rel: 'noopener' }
})

async function runSearch () {
  loading.value = true
  try {
    const res = await storefrontApi.search({
      query: query.value || undefined,
      categoryId: route.query.categoryId || undefined,
      manufacturerId: route.query.manufacturerId || undefined,
      specificationOptionIds: selectedOptionIds.value,
      minPrice: minPrice.value ?? undefined,
      maxPrice: maxPrice.value ?? undefined,
      sort: sort.value || undefined,
      page: page.value,
      pageSize: PAGE_SIZE
    })
    items.value = Array.isArray(res?.items) ? res.items : []
    totalCount.value = res?.totalCount || 0
    facetGroups.value = normalizeFacets(res?.facets)
  } catch (err) {
    items.value = []
    totalCount.value = 0
    facetGroups.value = []
    notify.error(getApiErrorMessage(err))
  } finally {
    loading.value = false
  }
}

function onFiltersChange (ids) {
  selectedOptionIds.value = ids
  page.value = 1
  runSearch()
}

function onPriceChange (which, value) {
  if (which === 'min') minPrice.value = value
  else maxPrice.value = value
  page.value = 1
  runSearch()
}

function onSortChange () {
  page.value = 1
  runSearch()
}

function onPageChange () {
  runSearch()
  if (typeof window !== 'undefined') window.scrollTo({ top: 0, behavior: 'smooth' })
}

function clearFilters () {
  selectedOptionIds.value = []
  minPrice.value = null
  maxPrice.value = null
  page.value = 1
  runSearch()
}

// Submit the refine box: push the new query onto the route (the watch re-runs the
// search). Re-runs directly when the query is unchanged so a re-submit still works.
function submitSearch () {
  const q = (queryInput.value || '').trim()
  if (q === (query.value || '')) {
    page.value = 1
    runSearch()
    return
  }
  const next = { ...route.query }
  if (q) next.q = q
  else delete next.q
  router.push({ name: 'shop-search', query: next })
}

// WO-105: load the configurable search-page content once. Missing/failed → keep defaults.
async function loadContent () {
  try {
    const res = await catalogCmsApi.searchContent()
    content.value = res || {}
  } catch (e) {
    content.value = {}
  }
}

// React to a new query / category / manufacturer coming from the header or links.
watch(
  () => [route.query.q, route.query.categoryId, route.query.manufacturerId],
  ([q]) => {
    query.value = typeof q === 'string' ? q : ''
    queryInput.value = query.value
    selectedOptionIds.value = []
    minPrice.value = null
    maxPrice.value = null
    page.value = 1
    runSearch()
  }
)

onMounted(() => {
  query.value = typeof route.query.q === 'string' ? route.query.q : ''
  queryInput.value = query.value
  loadContent()
  runSearch()
})
</script>

<style scoped lang="scss">
.storefront-container {
  max-width: 1400px;
  margin: 0 auto;
}
.sf-search__input {
  max-width: 520px;
}

/* WO-105 no-results promotional banner */
.sf-noresults-banner {
  display: flex;
  align-items: center;
  gap: 24px;
  flex-wrap: wrap;
  justify-content: center;
  text-align: center;
  text-decoration: none;
  color: inherit;
}
.sf-noresults-banner__img {
  max-width: 100%;
  height: auto;
  border-radius: 6px;
}
.sf-noresults-banner__title {
  font-size: 20px;
  font-weight: 600;
  color: var(--sf-heading, inherit);
}
.sf-noresults-banner__subtitle {
  margin-top: 4px;
  color: var(--sf-body, inherit);
}
</style>
