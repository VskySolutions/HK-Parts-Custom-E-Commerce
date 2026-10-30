<template>
  <q-page class="app-page">
    <AppListHeader
      title="Inventory"
      :breadcrumbs="[{ label: 'Home', icon: 'o_home', to: '/dashboard' }, { label: 'Inventory' }]"
    >
      <template #actions>
        <div style="min-width: 240px" class="q-mr-sm StoreSelector">
          <AppSelect
            v-model="storeId"
            label=""
            :options="storeOptions"
            :disable="!stores.length"
            placeholder="Select a store"
            hide-bottom-space
            @update:model-value="onStoreChange"
          />
        </div>
        <q-input
          v-model="search"
          dense
          outlined
          debounce="400"
          placeholder="Search by name or SKU"
          class="q-mr-sm"
          style="min-width: 220px"
          :disable="!storeId"
          @update:model-value="reload"
        >
          <template #prepend><q-icon name="o_search" /></template>
          <template v-if="search" #append>
            <q-icon name="o_close" class="cursor-pointer" @click="search = ''; reload()" />
          </template>
        </q-input>
        <q-btn outline color="primary" no-caps icon="o_tune" label="Advanced" :disable="!storeId" @click="filtersOpen = true">
          <q-badge v-if="typeFilter" color="red" floating>1</q-badge>
        </q-btn>
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
    </AppFilterDrawer>

    <!-- No store selected / no stores -->
    <q-banner v-if="!stores.length" class="bg-grey-2 rounded-borders q-mt-sm">
      No stores exist yet. Create a store before you can hold stock against it.
    </q-banner>
    <q-banner v-else-if="!storeId" class="bg-blue-1 text-blue-9 rounded-borders q-mt-sm">
      <template #avatar><q-icon name="o_store" color="blue-9" /></template>
      Select a store above to view and update its stock.
    </q-banner>

    <template v-else>
      <div class="row items-center text-caption text-grey-7 q-mb-sm q-px-xs">
        <q-icon name="o_cloud_sync" size="16px" class="q-mr-xs" />
        Editing on-hand stock for <span class="text-weight-medium text-dark q-mx-xs">{{ storeName }}</span>
        — changes are saved automatically. Variant products expand to show per-variant stock.
      </div>

      <AppDataTable
        page-key="inventory-mass"
        row-key="id"
        title="Products"
        :rows="rows"
        :columns="columns"
        :loading="loading"
        :pagination="pagination"
        class="inv-mass-table"
        @request="onRequest"
        @refresh="reload"
      >
        <template #body="p">
          <!-- Product row -->
          <q-tr :props="p" :class="{ 'inv-parent': isVariant(p.row) }">
            <q-td key="expand" :props="p" auto-width>
              <q-btn
                v-if="isVariant(p.row)"
                flat round dense
                :icon="expanded[p.row.id] ? 'o_expand_less' : 'o_expand_more'"
                @click="toggleExpand(p.row)"
              />
            </q-td>
            <q-td key="name" :props="p" class="text-left"><span class="text-weight-medium">{{ p.row.name }}</span></q-td>
            <q-td key="sku" :props="p" class="text-left text-grey-7">{{ p.row.sku || '—' }}</q-td>
            <q-td key="type" :props="p" class="text-left"><q-badge outline color="primary" :label="productTypeLabel(p.row.productType)" /></q-td>

            <template v-if="isVariant(p.row)">
              <q-td key="qty" :props="p" class="text-right">
                <span class="text-weight-medium">{{ variantProductTotal(p.row.id) }}</span>
                <div class="text-caption text-grey-6">total · all variants</div>
              </q-td>
              <q-td key="threshold" :props="p" />
              <q-td key="reserved" :props="p" class="text-right text-grey-8">{{ variantProductReserved(p.row.id) }}</q-td>
            </template>
            <template v-else>
              <q-td key="qty" :props="p">
                <q-input
                  :model-value="entry(p.row.id, null).qty"
                  dense borderless type="number" min="0" input-class="text-right"
                  :disable="!canWrite"
                  @update:model-value="(v) => onQty(p.row.id, null, v)"
                >
                  <template #append><InvCellStatus :product-id="p.row.id" :variant-id="null" /></template>
                </q-input>
              </q-td>
              <q-td key="threshold" :props="p">
                <q-input
                  :model-value="entry(p.row.id, null).threshold"
                  dense borderless type="number" min="0" input-class="text-right" placeholder="0"
                  :disable="!canWrite"
                  @update:model-value="(v) => onThreshold(p.row.id, null, v)"
                >
                  <template #append><InvCellStatus :product-id="p.row.id" :variant-id="null" /></template>
                </q-input>
              </q-td>
              <q-td key="reserved" :props="p" class="text-right text-grey-8">{{ reservedOf(p.row.id, null) }}</q-td>
            </template>
          </q-tr>

          <!-- Variant sub-rows -->
          <template v-if="isVariant(p.row) && expanded[p.row.id]">
            <q-tr v-if="variantLoading[p.row.id]" :props="p" no-hover>
              <q-td colspan="100%" class="text-grey-6"><q-spinner size="16px" class="q-mr-xs" /> Loading variants…</q-td>
            </q-tr>
            <q-tr v-else-if="!(variantsOf[p.row.id] || []).length" :props="p" no-hover>
              <q-td colspan="100%" class="text-grey-6 text-caption">No variants generated for this product yet.</q-td>
            </q-tr>
            <q-tr v-for="vr in (variantsOf[p.row.id] || [])" :key="vr.id" :props="p" no-hover class="inv-variant-row">
              <q-td />
              <q-td class="text-left text-grey-8">
                <q-icon name="o_subdirectory_arrow_right" size="16px" class="q-mr-xs text-grey-5" />{{ vr.label }}
              </q-td>
              <q-td class="text-left text-grey-7">{{ vr.sku || '—' }}</q-td>
              <q-td class="text-left text-grey-5 text-caption">Variant</q-td>
              <q-td>
                <q-input
                  :model-value="entry(p.row.id, vr.id).qty"
                  dense borderless type="number" min="0" input-class="text-right"
                  :disable="!canWrite"
                  @update:model-value="(v) => onQty(p.row.id, vr.id, v)"
                >
                  <template #append><InvCellStatus :product-id="p.row.id" :variant-id="vr.id" /></template>
                </q-input>
              </q-td>
              <q-td>
                <q-input
                  :model-value="entry(p.row.id, vr.id).threshold"
                  dense borderless type="number" min="0" input-class="text-right" placeholder="0"
                  :disable="!canWrite"
                  @update:model-value="(v) => onThreshold(p.row.id, vr.id, v)"
                >
                  <template #append><InvCellStatus :product-id="p.row.id" :variant-id="vr.id" /></template>
                </q-input>
              </q-td>
              <q-td class="text-right text-grey-8">{{ reservedOf(p.row.id, vr.id) }}</q-td>
            </q-tr>
          </template>
        </template>
      </AppDataTable>
    </template>
  </q-page>
</template>

<script setup>
/*
 * Inventory mass-update page. Pick a store, then edit on-hand stock for every product at that store in
 * one grid — simple/grouped/downloadable/gift-card products get a single per-store quantity; variant
 * products expand to per-variant quantities. Edits are staged (dirty-tracked) and persisted in a batch
 * via the inventory upsert endpoint. This is the authoritative fulfillment stock the checkout routing
 * engine reads (distinct from the catalog-level Products.StockQuantity display field).
 */
import { ref, reactive, computed, onMounted, defineComponent, h } from 'vue'
import { debounce, QSpinner, QIcon, QTooltip } from 'quasar'
import { useRoute } from 'vue-router'
import { getApiErrorMessage } from 'services/api'
import {
  productApi, inventoryApi, productAttributeApi, productTypeOptions, productTypeLabel
} from 'modules/catalog/api'
import { storeApi } from 'modules/stores/api'
import { usePermissions } from 'composables/usePermissions'
import { useNotify } from 'composables/useNotify'
import AppListHeader from 'components/common/AppListHeader.vue'
import AppDataTable from 'components/common/AppDataTable.vue'
import AppSelect from 'components/common/AppSelect.vue'
import AppFilterDrawer from 'components/common/AppFilterDrawer.vue'

const route = useRoute()
const notify = useNotify()
const { has } = usePermissions()
const canWrite = computed(() => has('Catalog.Write'))

// ---- Stores -----------------------------------------------------------------
const stores = ref([])
const storeId = ref(null)
const storeOptions = computed(() => stores.value.map((s) => ({ label: s.name, value: s.id })))
const storeName = computed(() => stores.value.find((s) => s.id === storeId.value)?.name || '')

// ---- Products grid ----------------------------------------------------------
const rows = ref([])
const loading = ref(false)
const search = ref('')
const typeFilter = ref(null)
const filtersOpen = ref(false)
const pagination = ref({ page: 1, rowsPerPage: 20, rowsNumber: 0 })

// Column definitions match the custom body slot's q-td `key`s. Server list has no sort, so no sortable.
const columns = [
  { name: 'expand', label: '', field: 'id', align: 'center' },
  { name: 'name', label: 'Product', field: 'name', align: 'left' },
  { name: 'sku', label: 'SKU', field: 'sku', align: 'left' },
  { name: 'type', label: 'Type', field: 'productType', align: 'left' },
  { name: 'qty', label: 'Stock qty', field: 'id', align: 'right' },
  { name: 'threshold', label: 'Low-stock alert', field: 'id', align: 'right' },
  { name: 'reserved', label: 'Reserved', field: 'id', align: 'right' }
]

// ---- Per-store levels + staged edits ----------------------------------------
// levelMap: persisted value at the selected store. edits: current (staged) value. key = `${productId}|${variantId||''}`.
const levelMap = reactive({})
const edits = reactive({})
const expanded = reactive({})
const variantsOf = reactive({})       // productId -> [{ id, sku, label }]
const variantLoading = reactive({})
const attributeValueMap = ref({})

const key = (productId, variantId) => `${productId}|${variantId || ''}`
const isVariant = (row) => row.productType === 'WithVariants'

// Read-only for rendering: the staged edit if one exists, else the persisted store level (or 0). Never
// mutates state (creating an edit during render would trip a Vue warning) — onQty/onThreshold do that.
function entry (productId, variantId) {
  const k = key(productId, variantId)
  if (edits[k]) return edits[k]
  const lvl = levelMap[k]
  return { qty: lvl ? lvl.stockQuantity : 0, threshold: lvl ? lvl.lowStockThreshold : 0 }
}

function ensureEdit (productId, variantId) {
  const k = key(productId, variantId)
  if (!edits[k]) {
    const lvl = levelMap[k]
    edits[k] = { qty: lvl ? lvl.stockQuantity : 0, threshold: lvl ? lvl.lowStockThreshold : 0 }
  }
  return edits[k]
}

function isDirty (productId, variantId) {
  const k = key(productId, variantId)
  const e = edits[k]
  if (!e) return false
  const lvl = levelMap[k] || { stockQuantity: 0, lowStockThreshold: 0 }
  return num(e.qty) !== (lvl.stockQuantity || 0) || num(e.threshold) !== (lvl.lowStockThreshold || 0)
}

function num (v) { return Math.max(0, Number(v) || 0) }
function onQty (productId, variantId, v) { ensureEdit(productId, variantId).qty = v; queueSave(productId, variantId) }
function onThreshold (productId, variantId, v) { ensureEdit(productId, variantId).threshold = v; queueSave(productId, variantId) }

// Auto-save: every edit debounces a per-cell upsert (matches the product page's inline save behavior).
const invSavers = {}
function queueSave (productId, variantId) {
  const k = key(productId, variantId)
  if (!invSavers[k]) {
    invSavers[k] = debounce(async () => {
      const e = edits[k]
      if (!e || !storeId.value) return
      saveState[k] = { saving: true, saved: false }
      try {
        const dto = await inventoryApi.upsertLevel({
          productId,
          productVariantId: variantId,
          storeId: storeId.value,
          stockQuantity: num(e.qty),
          lowStockThreshold: e.threshold === '' || e.threshold == null ? null : num(e.threshold)
        })
        levelMap[key(dto.productId, dto.productVariantId)] = { stockQuantity: dto.stockQuantity, lowStockThreshold: dto.lowStockThreshold, reservedQuantity: dto.reservedQuantity }
        // Re-sync the edit to the persisted values so the cell is no longer dirty.
        e.qty = dto.stockQuantity
        e.threshold = dto.lowStockThreshold
        saveState[k] = { saving: false, saved: true }
        setTimeout(() => { if (saveState[k]) saveState[k].saved = false }, 2000)
      } catch (err) {
        saveState[k] = { saving: false, saved: false }
        notify.error(getApiErrorMessage(err))
      }
    }, 700)
  }
  invSavers[k]()
}

// Total on-hand for a variant product at the store: sum of its variant levels (staged edit wins over
// the persisted level). Works whether or not the row is expanded, since levelMap holds all store levels.
function variantProductTotal (productId) {
  const prefix = `${productId}|`
  const keys = new Set([...Object.keys(edits), ...Object.keys(levelMap)])
  let total = 0
  for (const k of keys) {
    if (!k.startsWith(prefix) || k === prefix) continue // skip other products + the null-variant key
    total += edits[k] ? num(edits[k].qty) : (Number(levelMap[k]?.stockQuantity) || 0)
  }
  return total
}

// Reserved quantity is system-managed (set by confirmed-but-unshipped orders) — read-only display.
function reservedOf (productId, variantId) {
  return Number(levelMap[key(productId, variantId)]?.reservedQuantity) || 0
}
function variantProductReserved (productId) {
  const prefix = `${productId}|`
  let total = 0
  for (const k of Object.keys(levelMap)) {
    if (k.startsWith(prefix) && k !== prefix) total += Number(levelMap[k].reservedQuantity) || 0
  }
  return total
}

// Per-cell save state during a batch save (matches the product page's inline spinner/check design).
const saveState = reactive({})
const EMPTY_STATE = Object.freeze({ saving: false, saved: false })
function cellState (productId, variantId) {
  return saveState[key(productId, variantId)] || EMPTY_STATE
}

// The inline status shown in each stock/alert input's #append — spinner while saving, a check just
// after, or an "unsaved" marker while the cell is dirty. Mirrors the variant "Stock @ store" cell.
const InvCellStatus = defineComponent({
  props: {
    productId: { type: String, required: true },
    variantId: { type: String, default: null }
  },
  setup (props) {
    return () => {
      const st = cellState(props.productId, props.variantId)
      if (st.saving) return h(QSpinner, { size: '14px', color: 'primary' })
      if (st.saved) return h(QIcon, { name: 'o_check_circle', color: 'positive', size: '14px' })
      if (isDirty(props.productId, props.variantId)) {
        return h(QIcon, { name: 'o_tune', color: 'orange', size: '14px' }, () => h(QTooltip, () => 'Unsaved'))
      }
      return null
    }
  }
})

// ---- Loading ----------------------------------------------------------------
async function loadStores () {
  try {
    const result = await storeApi.list({ page: 1, pageSize: 200 })
    const items = Array.isArray(result) ? result : result?.items || []
    stores.value = items.map((s) => ({ id: s.id, name: s.name }))
    if (!storeId.value && stores.value.length) {
      // Preselect the store passed via ?storeId= (from the store list "Inventory" action), else the first.
      const q = route.query.storeId
      storeId.value = stores.value.some((s) => s.id === q) ? q : stores.value[0].id
    }
  } catch (err) {
    stores.value = []
    notify.error(getApiErrorMessage(err))
  }
}

async function loadAttributeMap () {
  try {
    const result = await productAttributeApi.list({ page: 1, pageSize: 200 })
    const items = Array.isArray(result) ? result : result?.items || []
    const map = {}
    for (const attr of items) {
      for (const val of attr.values || []) map[val.id] = `${attr.name}: ${val.value}`
    }
    attributeValueMap.value = map
  } catch (err) { attributeValueMap.value = {} }
}

// Load every existing level at the store into levelMap (products with no row default to 0).
async function loadStoreLevels () {
  for (const k of Object.keys(levelMap)) delete levelMap[k]
  if (!storeId.value) return
  try {
    const result = await inventoryApi.list({ storeId: storeId.value, page: 1, pageSize: 1000 })
    const items = Array.isArray(result) ? result : result?.items || []
    for (const l of items) {
      levelMap[key(l.productId, l.productVariantId)] = { stockQuantity: l.stockQuantity, lowStockThreshold: l.lowStockThreshold, reservedQuantity: l.reservedQuantity }
    }
  } catch (err) { notify.error(getApiErrorMessage(err)) }
}

async function fetchProducts (reqProps) {
  if (!storeId.value) return
  const p = reqProps?.pagination || pagination.value
  loading.value = true
  try {
    const result = await productApi.list({
      page: p.page,
      pageSize: p.rowsPerPage,
      search: search.value || undefined,
      type: typeFilter.value || undefined
    })
    const items = Array.isArray(result) ? result : result?.items || []
    const total = Array.isArray(result) ? result.length : result?.totalCount ?? items.length
    rows.value = items
    pagination.value = { ...p, rowsNumber: total }
    // Re-hydrate any expanded variant products still on this page.
    for (const row of items) {
      if (isVariant(row) && expanded[row.id] && !variantsOf[row.id]) loadVariants(row)
    }
  } catch (err) {
    rows.value = []
    notify.error(getApiErrorMessage(err))
  } finally {
    loading.value = false
  }
}

function seedEdits () {
  // Drop staged edits so freshly loaded persisted values show (called after a store switch / save).
  for (const k of Object.keys(edits)) delete edits[k]
}

async function loadVariants (row) {
  if (variantsOf[row.id]) return
  variantLoading[row.id] = true
  try {
    const full = await productApi.get(row.id)
    variantsOf[row.id] = (full.variants || []).map((v) => ({
      id: v.id,
      sku: v.sku,
      label: labelForVariant(v)
    }))
  } catch (err) {
    variantsOf[row.id] = []
    notify.error(getApiErrorMessage(err))
  } finally {
    variantLoading[row.id] = false
  }
}

function labelForVariant (v) {
  const parts = (v.attributeValueIds || []).map((id) => attributeValueMap.value[id] || id)
  return parts.length ? parts.join(', ') : (v.sku || 'Variant')
}

function toggleExpand (row) {
  expanded[row.id] = !expanded[row.id]
  if (expanded[row.id]) loadVariants(row)
}

// ---- Actions ----------------------------------------------------------------
async function onStoreChange () {
  // Cancel any pending auto-saves for the previous store, then reset the editing state.
  for (const s of Object.values(invSavers)) s.cancel?.()
  for (const k of Object.keys(saveState)) delete saveState[k]
  seedEdits()
  pagination.value.page = 1
  await loadStoreLevels()
  await fetchProducts()
}

function reload () {
  fetchProducts({ pagination: { ...pagination.value, page: 1 } })
}

function onRequest (reqProps) {
  fetchProducts(reqProps)
}

function clearFilters () {
  typeFilter.value = null
  reload()
}

onMounted(async () => {
  await Promise.all([loadStores(), loadAttributeMap()])
  if (storeId.value) {
    await loadStoreLevels()
    await fetchProducts()
  }
})
</script>

<style scoped lang="scss">
.inv-mass-table {
  th { font-weight: 600; }
  .inv-parent td { background: rgba(0, 0, 0, 0.015); }
  .inv-variant-row td { background: rgba(0, 0, 0, 0.02); }
}
</style>
