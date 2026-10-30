<template>
  <q-page class="app-page">
    <AppListHeader
      title="Products"
      :breadcrumbs="[{ label: 'Home', icon: 'o_home', to: '/dashboard' }, { label: 'Products' }]"
      :show-add="canWrite"
      add-label="New product"
      @add="onAdd"
    >
      <template #actions>
        <q-input
          v-model="search"
          dense
          outlined
          debounce="400"
          placeholder="Quick search by name or SKU"
          class="q-mr-sm"
          style="min-width: 240px"
          @update:model-value="reload"
        >
          <template #prepend><q-icon name="o_search" /></template>
          <template v-if="search" #append>
            <q-icon name="o_close" class="cursor-pointer" @click="search = ''; reload()" />
          </template>
        </q-input>
        <q-btn outline color="primary" no-caps icon="o_tune" label="Advanced" class="q-mr-sm" @click="filtersOpen = true">
          <q-badge v-if="activeFilterCount" color="red" floating>{{ activeFilterCount }}</q-badge>
        </q-btn>
        <q-btn v-if="canWrite" outline color="primary" no-caps icon="o_swap_vert" label="Import / Export" @click="goImportExport" />
      </template>
    </AppListHeader>

    <AppFilterDrawer v-model="filtersOpen" title="Filter products" @clear="clearFilters">
      <AppSelect
        v-model="typeFilter"
        label="Product type"
        clearable
        placeholder="Any type"
        :options="productTypeOptions"
        @update:model-value="reload"
      />
      <AppSelect
        v-model="publishedFilter"
        label="Status"
        :options="publishedOptions"
        @update:model-value="reload"
      />
      <AppSelect
        v-model="featuredFilter"
        label="Featured"
        :options="featuredOptions"
        @update:model-value="reload"
      />
    </AppFilterDrawer>

    <AppDataTable
      page-key="catalog-products"
      row-key="id"
      title="All products"
      :rows="rows"
      :columns="columns"
      :loading="loading"
      :pagination="pagination"
      show-actions
      @request="onRequest"
      @refresh="reload"
    >
      <template #body-cell-name="cell">
        <q-td :props="cell">
          <a class="text-primary cursor-pointer text-weight-medium" @click="onManage(cell.row)">{{ cell.row.name }}</a>
        </q-td>
      </template>

      <template #body-cell-productType="cell">
        <q-td :props="cell">
          <q-badge outline color="primary" :label="productTypeLabel(cell.row.productType)" />
        </q-td>
      </template>

      <template #body-cell-price="cell">
        <q-td :props="cell">{{ formatPrice(cell.row.price) }}</q-td>
      </template>

      <template #body-cell-isPublished="cell">
        <q-td :props="cell">
          <q-badge
            :color="cell.row.isPublished ? 'positive' : 'grey'"
            :label="cell.row.isPublished ? 'Published' : 'Draft'"
          />
        </q-td>
      </template>

      <template #body-cell-isFeatured="cell">
        <q-td :props="cell">
          <q-icon v-if="cell.row.isFeatured" name="o_star" color="amber-7" size="20px"><q-tooltip>Featured</q-tooltip></q-icon>
          <span v-else class="text-grey-5">—</span>
        </q-td>
      </template>

      <template #actions="{ row }">
        <q-btn
          flat
          round
          dense
          icon="o_visibility"
          color="primary"
          type="a"
          :href="storePreviewUrl(row)"
          target="_blank"
          rel="noopener"
          @click.stop
        >
          <q-tooltip>Preview in store (new tab)</q-tooltip>
        </q-btn>
        <q-btn flat round dense icon="o_tune" @click="onManage(row)">
          <q-tooltip>Manage</q-tooltip>
        </q-btn>
        <q-btn v-if="canWrite" flat round dense icon="o_delete" color="negative" @click="onDelete(row)">
          <q-tooltip>Delete</q-tooltip>
        </q-btn>
      </template>
    </AppDataTable>
  </q-page>
</template>

<script setup>
/*
 * Products list page (WO-15; standardized). AppListHeader toolbar (search + type + published filters)
 * + AppDataTable (server pagination, card title + refresh). Create and edit both use the full-page
 * product create/detail page (`catalog-product-new` / `catalog-product-detail`) — no drawer.
 */
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { getApiErrorMessage } from 'services/api'
import { productApi, productTypeOptions, productTypeLabel } from 'modules/catalog/api'
import { usePermissions } from 'composables/usePermissions'
import { useNotify } from 'composables/useNotify'
import { deleteConfirmation } from 'dialogs/delete_confirmation'

const router = useRouter()
const notify = useNotify()
const { has } = usePermissions()
const canWrite = computed(() => has('Catalog.Write'))

const columns = [
  { name: 'name', label: 'Name', field: 'name', align: 'left', sortable: true },
  { name: 'sku', label: 'SKU', field: 'sku', align: 'left', sortable: true },
  { name: 'productType', label: 'Type', field: 'productType', align: 'left', sortable: true },
  { name: 'price', label: 'Price', field: 'price', align: 'right', sortable: true },
  { name: 'stockQuantity', label: 'Stock', field: 'stockQuantity', align: 'right' },
  { name: 'isPublished', label: 'Published', field: 'isPublished', align: 'center', sortable: true },
  { name: 'isFeatured', label: 'Featured', field: 'isFeatured', align: 'center', sortable: true }
]

const publishedOptions = [
  { label: 'All', value: null },
  { label: 'Published', value: true },
  { label: 'Draft', value: false }
]

const featuredOptions = [
  { label: 'All', value: null },
  { label: 'Featured', value: true },
  { label: 'Not featured', value: false }
]

const rows = ref([])
const loading = ref(false)
const search = ref('')
const typeFilter = ref(null)
const publishedFilter = ref(null)
const featuredFilter = ref(null)
const filtersOpen = ref(false)
const pagination = ref({ page: 1, rowsPerPage: 10, rowsNumber: 0 })

// Count of active advanced filters (drives the "Advanced" button badge).
const activeFilterCount = computed(() =>
  (typeFilter.value ? 1 : 0) + (publishedFilter.value !== null ? 1 : 0) + (featuredFilter.value !== null ? 1 : 0)
)

function clearFilters () {
  typeFilter.value = null
  publishedFilter.value = null
  featuredFilter.value = null
  reload()
}

function formatPrice (value) {
  if (value === null || value === undefined) return '—'
  return Number(value).toFixed(2)
}

async function fetch (props) {
  const p = props?.pagination || pagination.value
  loading.value = true
  try {
    const result = await productApi.list({
      page: p.page,
      pageSize: p.rowsPerPage,
      search: search.value || undefined,
      type: typeFilter.value || undefined,
      isPublished: publishedFilter.value === null ? undefined : publishedFilter.value,
      isFeatured: featuredFilter.value === null ? undefined : featuredFilter.value,
      sortBy: p.sortBy || undefined,
      sortDescending: !!p.descending
    })
    const items = Array.isArray(result) ? result : result?.items || []
    const total = Array.isArray(result) ? result.length : result?.totalCount ?? items.length
    rows.value = items
    pagination.value = { ...p, rowsNumber: total }
  } catch (err) {
    rows.value = []
    pagination.value = { ...p, rowsNumber: 0 }
    notify.error(getApiErrorMessage(err))
  } finally {
    loading.value = false
  }
}

function onRequest (props) {
  fetch(props)
}

function reload () {
  fetch({ pagination: { ...pagination.value, page: 1 } })
}

function onAdd () {
  router.push({ name: 'catalog-product-new' })
}

function goImportExport () {
  router.push({ name: 'catalog-product-import-export' })
}

function onManage (row) {
  router.push({ name: 'catalog-product-detail', params: { id: row.id } })
}

// Storefront product URL (slug preferred, id fallback) for the "Preview in store" action — opened in a
// new tab so the admin keeps their place in the list.
function storePreviewUrl (row) {
  return router.resolve({ name: 'shop-product', params: { idOrSlug: row.slug || row.id } }).href
}

async function onDelete (row) {
  if (!(await deleteConfirmation(`the product "${row.name}"`))) return
  try {
    await productApi.remove(row.id)
    notify.success('Product deleted')
    reload()
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  }
}

onMounted(() => fetch())
</script>
