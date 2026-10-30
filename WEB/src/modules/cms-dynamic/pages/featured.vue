<template>
  <q-page class="app-page">
    <AppListHeader
      title="Featured"
      subtitle="Hand-pick the products and categories highlighted across the storefront."
      :breadcrumbs="[{ label: 'Home', icon: 'o_home', to: '/dashboard' }, { label: 'Featured' }]"
    />

    <q-card flat bordered class="app-section">
      <q-tabs
        v-model="tab"
        align="left"
        active-color="primary"
        indicator-color="primary"
        class="text-grey-7"
        no-caps
        inline-label
      >
        <q-tab name="products" icon="o_star" label="Featured products" />
        <q-tab name="categories" icon="o_category" label="Featured categories" />
      </q-tabs>
      <q-separator />

      <q-tab-panels v-model="tab" animated keep-alive>
        <!-- ============ FEATURED PRODUCTS ============ -->
        <q-tab-panel name="products">
          <div class="row items-center q-mb-md">
            <div class="col">
              <div class="app-section__title">Featured products</div>
              <div class="text-caption text-grey-7">Ordered as shown in featured rows. Use the arrows to reorder.</div>
            </div>
            <q-badge color="blue-1" text-color="primary" :label="`${products.length} products`" />
          </div>

          <div v-if="canWrite" class="q-mb-md" style="max-width: 460px">
            <ProductPicker :exclude-ids="productIds" :disable="savingProducts" @select="addProduct" />
          </div>

          <q-inner-loading :showing="loadingProducts" color="primary" />

          <div v-if="!loadingProducts && !products.length" class="text-grey-6 text-caption q-py-md text-center">
            No featured products yet — search above to add the first one.
          </div>

          <q-list v-if="products.length" separator>
            <q-item v-for="(p, i) in products" :key="p.id" class="q-py-sm">
              <q-item-section side>
                <div class="column">
                  <q-btn flat dense round size="sm" icon="o_arrow_upward" :disable="i === 0 || !canWrite || savingProducts" @click="moveProduct(i, -1)">
                    <q-tooltip>Move up</q-tooltip>
                  </q-btn>
                  <q-btn flat dense round size="sm" icon="o_arrow_downward" :disable="i === products.length - 1 || !canWrite || savingProducts" @click="moveProduct(i, 1)">
                    <q-tooltip>Move down</q-tooltip>
                  </q-btn>
                </div>
              </q-item-section>

              <q-item-section side>
                <q-avatar rounded size="44px" color="grey-2" text-color="grey-6">
                  <img v-if="p.imageUrl" :src="$media(p.imageUrl)" :alt="p.name">
                  <q-icon v-else name="o_image" size="20px" />
                </q-avatar>
              </q-item-section>

              <q-item-section>
                <q-item-label lines="1">{{ p.name }}</q-item-label>
                <q-item-label caption>
                  <span v-if="p.sku">SKU: {{ p.sku }}</span>
                  <span v-if="p.price != null"> · {{ formatPrice(p.price) }}</span>
                </q-item-label>
              </q-item-section>

              <q-item-section side>
                <q-badge :color="p.isPublished ? 'positive' : 'grey'" :label="p.isPublished ? 'Published' : 'Unpublished'" />
              </q-item-section>

              <q-item-section side>
                <q-btn flat round dense icon="o_star_border" color="negative" :disable="!canWrite || savingProducts" @click="removeProduct(p)">
                  <q-tooltip>Remove from featured</q-tooltip>
                </q-btn>
              </q-item-section>
            </q-item>
          </q-list>
        </q-tab-panel>

        <!-- ============ FEATURED CATEGORIES ============ -->
        <q-tab-panel name="categories">
          <div class="app-section__title q-mb-xs">Featured categories</div>
          <div class="text-caption text-grey-7 q-mb-md">Pick the categories to highlight. Order follows the catalog.</div>

          <div v-if="canWrite" class="q-mb-md" style="max-width: 560px">
            <AppSelect
              :model-value="selectedCategoryIds"
              label="Featured categories"
              :options="categoryOptions"
              multiple
              use-chips
              use-input
              :loading="savingCats"
              hint="Add or remove categories — changes apply immediately"
              @filter="filterCategories"
              @update:model-value="onCategoriesChange"
            />
          </div>

          <q-inner-loading :showing="loadingCats" color="primary" />

          <div v-if="!loadingCats && !categories.length" class="text-grey-6 text-caption q-py-md text-center">
            No featured categories yet — pick some above.
          </div>

          <div v-if="categories.length" class="row q-col-gutter-md">
            <div v-for="c in categories" :key="c.id" class="col-12 col-sm-6 col-md-4">
              <q-card flat bordered class="row items-center no-wrap q-pa-sm">
                <q-avatar rounded size="48px" color="grey-2" text-color="grey-6" class="q-mr-sm">
                  <img v-if="c.imageUrl" :src="$media(c.imageUrl)" :alt="c.name">
                  <q-icon v-else name="o_category" size="22px" />
                </q-avatar>
                <div class="col ellipsis">
                  <div class="text-weight-medium ellipsis">{{ c.name }}</div>
                  <div v-if="c.slug" class="text-caption text-grey-6 ellipsis">/{{ c.slug }}</div>
                </div>
                <q-btn flat round dense icon="o_close" color="negative" :disable="!canWrite || savingCats" @click="removeCategory(c)">
                  <q-tooltip>Remove from featured</q-tooltip>
                </q-btn>
              </q-card>
            </div>
          </div>
        </q-tab-panel>
      </q-tab-panels>
    </q-card>
  </q-page>
</template>

<script setup>
/*
 * Featured products & categories manager (WO-98). Two tabs:
 *  - Featured products: add via the product picker (PUT /products/{id} { isFeatured:true, order }),
 *    reorder with up/down arrows (PUT /products/reorder with the whole ordered id list), remove
 *    (PUT /products/{id} { isFeatured:false }).
 *  - Featured categories: a multi-select adds/removes categories (PUT /categories/{id} { isFeatured }),
 *    with per-card remove. Categories carry no reorder endpoint, so they're an unordered set.
 */
import { ref, computed, onMounted } from 'vue'
import { featuredApi } from '../api'
import { categoryApi } from 'modules/catalog/api'
import { moveInArray } from '../reorder'
import ProductPicker from '../components/ProductPicker.vue'
import { getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'
import { usePermissions } from 'composables/usePermissions'

const notify = useNotify()
const { has } = usePermissions()
const canWrite = computed(() => has('Cms.Write'))

const tab = ref('products')

// ---- Featured products -----------------------------------------------------
const products = ref([])
const loadingProducts = ref(false)
const savingProducts = ref(false)
const productIds = computed(() => products.value.map((p) => p.id))

function formatPrice (v) {
  const n = Number(v)
  return Number.isFinite(n) ? n.toFixed(2) : '—'
}

async function loadProducts () {
  loadingProducts.value = true
  try {
    const result = await featuredApi.listProducts()
    const items = Array.isArray(result) ? result : result?.items || result?.data || []
    products.value = items.slice().sort((a, b) => (a.featuredDisplayOrder || 0) - (b.featuredDisplayOrder || 0))
  } catch (err) {
    products.value = []
    notify.error(getApiErrorMessage(err))
  } finally {
    loadingProducts.value = false
  }
}

async function addProduct (product) {
  if (products.value.some((p) => p.id === product.id)) return
  savingProducts.value = true
  try {
    await featuredApi.setProduct(product.id, { isFeatured: true, featuredDisplayOrder: products.value.length })
    await loadProducts()
    notify.success('Product added to featured')
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    savingProducts.value = false
  }
}

async function removeProduct (p) {
  savingProducts.value = true
  try {
    await featuredApi.setProduct(p.id, { isFeatured: false })
    await loadProducts()
    notify.success('Product removed from featured')
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    savingProducts.value = false
  }
}

async function moveProduct (i, dir) {
  const target = i + dir
  if (target < 0 || target >= products.value.length) return
  const prev = products.value
  products.value = moveInArray(products.value, i, dir)
  savingProducts.value = true
  try {
    await featuredApi.reorderProducts(products.value.map((p) => p.id))
  } catch (err) {
    products.value = prev
    notify.error(getApiErrorMessage(err))
  } finally {
    savingProducts.value = false
  }
}

// ---- Featured categories ---------------------------------------------------
const categories = ref([])            // the currently-featured categories
const loadingCats = ref(false)
const savingCats = ref(false)
const allCategoryOptions = ref([])    // every category, for the multi-select
const categoryOptions = ref([])       // filtered view bound to the select
const selectedCategoryIds = ref([])

async function loadCategories () {
  loadingCats.value = true
  try {
    const result = await featuredApi.listCategories()
    categories.value = Array.isArray(result) ? result : result?.items || result?.data || []
    selectedCategoryIds.value = categories.value.map((c) => c.id)
  } catch (err) {
    categories.value = []
    notify.error(getApiErrorMessage(err))
  } finally {
    loadingCats.value = false
  }
}

async function loadAllCategories () {
  try {
    const result = await categoryApi.list({ page: 1, pageSize: 500 })
    const items = Array.isArray(result) ? result : result?.items || result?.data || []
    allCategoryOptions.value = items.map((c) => ({ label: c.name, value: c.id }))
    categoryOptions.value = allCategoryOptions.value
  } catch {
    allCategoryOptions.value = []
    categoryOptions.value = []
  }
}

// Local text filter over the loaded category options (q-select use-input).
function filterCategories (val, update) {
  update(() => {
    const q = (val || '').toLowerCase()
    categoryOptions.value = q
      ? allCategoryOptions.value.filter((o) => o.label.toLowerCase().includes(q))
      : allCategoryOptions.value
  })
}

// Diff the multi-select against the current featured set and PUT only what changed.
async function onCategoriesChange (nextIds) {
  const current = new Set(categories.value.map((c) => c.id))
  const next = new Set(nextIds)
  const toAdd = [...next].filter((id) => !current.has(id))
  const toRemove = [...current].filter((id) => !next.has(id))
  if (!toAdd.length && !toRemove.length) return
  selectedCategoryIds.value = nextIds
  savingCats.value = true
  try {
    await Promise.all([
      ...toAdd.map((id) => featuredApi.setCategory(id, true)),
      ...toRemove.map((id) => featuredApi.setCategory(id, false))
    ])
    await loadCategories()
    notify.success('Featured categories updated')
  } catch (err) {
    notify.error(getApiErrorMessage(err))
    await loadCategories()
  } finally {
    savingCats.value = false
  }
}

async function removeCategory (c) {
  savingCats.value = true
  try {
    await featuredApi.setCategory(c.id, false)
    await loadCategories()
    notify.success('Category removed from featured')
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    savingCats.value = false
  }
}

onMounted(() => {
  loadProducts()
  loadCategories()
  loadAllCategories()
})
</script>
