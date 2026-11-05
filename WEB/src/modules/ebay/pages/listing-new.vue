<template>
  <q-page class="app-page">
    <AppDetailHeader
      title="New eBay listing"
      :breadcrumbs="[
        { label: 'Home', icon: 'o_home', to: '/dashboard' },
        { label: 'eBay Listings', to: { name: 'ebay-listings' } },
        { label: 'New listing' }
      ]"
      show-back
      @back="router.push({ name: 'ebay-listings' })"
    />

    <q-stepper v-model="step" flat bordered animated color="primary" header-nav class="app-section">
      <!-- ============ STEP 1: PRODUCT ============ -->
      <q-step :name="1" title="Select Product" icon="o_inventory_2" :done="!!product">
        <div class="text-caption text-grey-7 q-mb-sm">
          Only enabled, in-stock Simple or Bundle products in this store that are not already listed on eBay are shown.
        </div>
        <q-input v-model="search" dense outlined debounce="400" placeholder="Search by name or SKU" autofocus class="q-mb-md" @update:model-value="searchProducts">
          <template #prepend><q-icon name="o_search" /></template>
        </q-input>
        <q-table
          flat
          bordered
          dense
          :rows="products"
          :columns="productColumns"
          row-key="id"
          :loading="searching"
          :rows-per-page-options="[0]"
          hide-pagination
          no-data-label="No eligible products found"
        >
          <template #body-cell-select="cell">
            <q-td :props="cell">
              <q-btn unelevated dense no-caps color="primary" label="Select" @click="selectProduct(cell.row)" />
            </q-td>
          </template>
        </q-table>
      </q-step>

      <!-- ============ STEP 2: CATEGORY ============ -->
      <q-step :name="2" title="Select eBay Category" icon="o_account_tree" :done="!!categoryId" :disable="!product">
        <div v-if="product" class="text-body2 q-mb-md">
          Listing <b>{{ product.name }}</b> <span class="text-grey-7">({{ product.sku || 'no SKU' }})</span>
        </div>

        <q-card v-if="settings?.defaultCategoryId && !chooseOther" flat bordered class="q-pa-md q-mb-md">
          <div class="text-body2 q-mb-sm">Use default category: <b>{{ settings.defaultCategoryName || settings.defaultCategoryId }}</b></div>
          <div class="row q-gutter-sm">
            <q-btn unelevated no-caps color="primary" label="Continue" :loading="creating" @click="useDefault" />
            <q-btn flat no-caps color="primary" label="Choose another category" @click="chooseOther = true" />
          </div>
        </q-card>

        <template v-else>
          <EbayCategoryTree v-model="categoryId" @select="(c) => (categoryName = c.name)" />
          <div class="row items-center q-mt-md">
            <span v-if="categoryId" class="text-body2">Selected: <b>{{ categoryName || categoryId }}</b></span>
            <q-space />
            <q-btn flat no-caps label="Back" @click="step = 1" />
            <q-btn unelevated no-caps color="primary" label="Continue" :disable="!categoryId" :loading="creating" @click="create" />
          </div>
        </template>
      </q-step>
    </q-stepper>
  </q-page>
</template>

<script setup>
/*
 * Create-listing wizard. Step 1 picks an eligible product, step 2 an eBay leaf category (or the store
 * default), then the server creates the draft (prefilled title/SKU/description/price/quantity/policies)
 * and we open its edit page.
 */
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { getApiErrorMessage } from 'services/api'
import { ebayApi, money } from 'modules/ebay/api'
import { useNotify } from 'composables/useNotify'
import EbayCategoryTree from 'modules/ebay/components/EbayCategoryTree.vue'

const router = useRouter()
const notify = useNotify()

const step = ref(1)
const search = ref('')
const products = ref([])
const searching = ref(false)
const product = ref(null)
const settings = ref(null)
const chooseOther = ref(false)
const categoryId = ref(null)
const categoryName = ref('')
const creating = ref(false)

const productColumns = [
  { name: 'sku', label: 'SKU', field: 'sku', align: 'left', format: (v) => v || '—' },
  { name: 'name', label: 'Name', field: 'name', align: 'left' },
  { name: 'productType', label: 'Type', field: 'productType', align: 'left', format: (v) => (v === 'Grouped' ? 'Bundle' : 'Simple') },
  { name: 'storeQuantity', label: 'In stock', field: 'storeQuantity', align: 'right' },
  { name: 'price', label: 'Price', field: 'price', align: 'right', format: money },
  { name: 'select', label: '', field: 'id', align: 'right' }
]

async function searchProducts () {
  searching.value = true
  try {
    products.value = await ebayApi.eligibleProducts(search.value)
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    searching.value = false
  }
}

function selectProduct (row) {
  product.value = row
  step.value = 2
}

function useDefault () {
  categoryId.value = settings.value.defaultCategoryId
  create()
}

async function create () {
  creating.value = true
  try {
    const listing = await ebayApi.createListing({ productId: product.value.id, categoryId: categoryId.value })
    notify.success('Draft listing created. Review it, then set Status to Active to publish.')
    router.replace({ name: 'ebay-listing-detail', params: { id: listing.id } })
  } catch (err) {
    const status = err?.response?.status
    notify.error(getApiErrorMessage(err))
    if (status === 409 || status === 400) {
      // Already listed / no longer eligible: back to step 1 with a fresh list.
      product.value = null
      step.value = 1
      searchProducts()
    }
  } finally {
    creating.value = false
  }
}

onMounted(async () => {
  searchProducts()
  try {
    settings.value = await ebayApi.getSettings()
  } catch (err) {
    settings.value = null
  }
})
</script>
