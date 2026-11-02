<template>
  <q-page class="app-page">
    <AppDetailHeader
      :title="isCreate ? 'New category' : (entity?.name || 'Category')"
      :breadcrumbs="[
        { label: 'Home', icon: 'o_home', to: '/dashboard' },
        { label: 'Categories', to: { name: 'catalog-categories' } },
        { label: isCreate ? 'New category' : (entity?.name || 'Category') }
      ]"
      :status="!isCreate && entity ? (form.isEnabled ? 'Enabled' : 'Disabled') : ''"
      :status-color="form.isEnabled ? 'positive' : 'grey'"
      show-back
      @back="router.push({ name: 'catalog-categories' })"
    >
      <template #actions>
        <q-chip
          v-if="saveStatus"
          :icon="saveStatus.icon"
          :color="saveStatus.chip"
          :text-color="saveStatus.text"
          square
          dense
          class="q-mr-sm text-caption"
        >
          <q-spinner v-if="saveStatus.spin" size="14px" class="q-mr-xs" />
          {{ saveStatus.label }}
        </q-chip>
      </template>
    </AppDetailHeader>

    <q-inner-loading :showing="loading" color="primary" />

    <q-banner v-if="!loading && !isCreate && !entity" class="bg-grey-2 rounded-borders">
      Category not found.
    </q-banner>

    <template v-if="isCreate || entity">
      <div v-if="!isCreate" class="row items-center text-caption text-grey-7 q-mb-sm q-px-xs">
        <q-icon name="o_cloud_sync" size="16px" class="q-mr-xs" />
        Changes are saved automatically as you edit — no need to press save.
      </div>

      <q-card flat bordered class="app-section">
        <q-tabs
          v-model="tab"
          align="left"
          active-color="primary"
          indicator-color="primary"
          class="text-grey-7 app-detail-tabs"
          no-caps
          inline-label
        >
          <q-tab name="general" icon="o_info" label="General" />
          <q-tab name="seo" icon="o_search" label="SEO" :disable="isCreate" />
          <q-tab name="filters" :disable="isCreate">
            <div class="row items-center no-wrap">
              <q-icon name="o_filter_list" class="q-mr-xs" />Filters
              <q-badge v-if="!isCreate && filterCount" color="grey-5" class="q-ml-xs">{{ filterCount }}</q-badge>
            </div>
          </q-tab>
          <q-tab name="products" :disable="isCreate">
            <div class="row items-center no-wrap">
              <q-icon name="o_inventory_2" class="q-mr-xs" />Products
              <q-badge v-if="!isCreate && productTotal" color="grey-5" class="q-ml-xs">{{ productTotal }}</q-badge>
            </div>
          </q-tab>
          <q-tab name="media" :disable="isCreate">
            <div class="row items-center no-wrap">
              <q-icon name="o_photo_library" class="q-mr-xs" />Media
              <q-badge v-if="!isCreate && pictures.length" color="grey-5" class="q-ml-xs">{{ pictures.length }}</q-badge>
            </div>
          </q-tab>
        </q-tabs>

        <q-separator />

        <q-tab-panels v-model="tab" animated keep-alive>
          <!-- ============ GENERAL ============ -->
          <q-tab-panel name="general" class="q-gutter-y-xs">
            <div class="row q-col-gutter-sm">
              <div class="col-12 col-md-8">
                <AppTextField v-model="form.name" label="Name" required :v="v$.name" placeholder="e.g. Men's Clothing" :disable="!canWrite" />
              </div>
              <div class="col-12 col-md-4">
                <AppSelect
                  v-model="form.parentId"
                  label="Parent category"
                  placeholder="None (top-level)"
                  :options="availableParents"
                  clearable
                  :disable="!canWrite"
                  hint="Where this sits in the category tree"
                />
              </div>
            </div>

            <AppTextField v-model="form.slug" label="Slug" :v="v$.slug" placeholder="e.g. mens-clothing" hint="Storefront URL — auto-filled from the name until you edit it" :disable="!canWrite" />
            <AppTextField v-model="form.summary" label="Summary" type="textarea" autogrow maxlength="2000" :v="v$.summary" placeholder="A short teaser for category tiles and listings" :disable="!canWrite" />
            <AppRichText v-model="form.description" label="Description" placeholder="Describe this category…" hint="Rich text shown on the category page" :disable="!canWrite" />

            <q-separator class="q-my-sm" />
            <div class="row q-col-gutter-sm items-center">
              <div class="col-6 col-md-3">
                <AppTextField v-model="form.displayOrder" label="Display order" type="number" hint="Lower shows first" :disable="!canWrite" />
              </div>
              <div class="col-auto q-mt-md">
                <q-toggle v-model="form.isEnabled" label="Enabled" color="primary" :disable="!canWrite" />
              </div>
              <div class="col-auto q-mt-md">
                <q-toggle v-model="form.showInMenu" label="Display in menu" color="primary" :disable="!canWrite">
                  <q-tooltip>Show this category in the storefront navigation menu</q-tooltip>
                </q-toggle>
              </div>
            </div>

            <!-- Fixed image slots (edit only — uploads need the category to exist) -->
            <template v-if="!isCreate">
              <q-separator class="q-my-sm" />
              <div class="row q-col-gutter-md">
                <div class="col-12 col-md-6 q-gutter-y-sm">
                  <MediaImageField v-model="form.imageMediaId" v-model:url="imageUrl" label="Image 1" hint="e.g. the listing / menu tile" :alt="form.imageAltText || form.name" :disable="!canWrite" />
                  <AppTextField v-model="form.imageAltText" label="Image 1 alt text" maxlength="300" :v="v$.imageAltText" placeholder="Describe the image for screen readers" :disable="!canWrite" />
                </div>
                <div class="col-12 col-md-6 q-gutter-y-sm">
                  <MediaImageField v-model="form.image2MediaId" v-model:url="image2Url" label="Image 2" hint="e.g. the category page banner" :alt="form.image2AltText || form.name" :disable="!canWrite" />
                  <AppTextField v-model="form.image2AltText" label="Image 2 alt text" maxlength="300" :v="v$.image2AltText" placeholder="Describe the image for screen readers" :disable="!canWrite" />
                </div>
              </div>
            </template>
          </q-tab-panel>

          <!-- ============ SEO ============ -->
          <q-tab-panel name="seo">
            <AppSeoPanel
              entity-type="Category"
              :entity-id="entity?.id"
              :slug="form.slug"
              :fallback-title="form.name"
              :fallback-description="form.description"
              fallback-label="category name"
              :disable="!canWrite"
              :run-save="runSave"
            />
          </q-tab-panel>

          <!-- ============ FILTERS ============ -->
          <q-tab-panel name="filters" class="q-gutter-y-sm">
            <div class="app-section__title q-mb-xs">Select filterable attributes</div>
            <div class="text-caption text-grey-7 q-mb-sm">Checked attributes are offered as filters on this category's product listing.</div>

            <q-card flat bordered class="q-pa-sm filter-list">
              <q-checkbox v-model="showBrandFilter" label="Brand" dense class="filter-list__item" :disable="!canWrite" @update:model-value="queueFilters" />
              <q-checkbox
                v-for="opt in specAttributeOptions"
                :key="opt.value"
                v-model="filterAttributeIds"
                :val="opt.value"
                dense
                class="filter-list__item"
                :disable="!canWrite"
                @update:model-value="queueFilters"
              >
                {{ opt.label }}
                <q-badge v-if="!opt.isFilterable" color="grey-4" text-color="grey-8" class="q-ml-xs">not filterable</q-badge>
              </q-checkbox>
            </q-card>

            <div v-if="!specAttributeOptions.length" class="text-caption text-grey-6">
              No attributes yet — add them under
              <router-link :to="{ name: 'catalog-attributes' }" class="text-primary">Catalog → Attributes</router-link>
              as specification attributes (e.g. Allow International Shipping, Condition, Shipping Package Size) and they'll appear here.
            </div>
            <div v-else-if="specAttributeOptions.some((o) => !o.isFilterable)" class="text-caption text-grey-6">
              Attributes marked “not filterable” are hidden from storefront filters until “Filterable” is switched on for them under Catalog → Attributes.
            </div>
          </q-tab-panel>

          <!-- ============ PRODUCTS ============ -->
          <q-tab-panel name="products">
            <q-table
              flat bordered dense
              class="app-table-card"
              row-key="id"
              :rows="products"
              :columns="productColumns"
              :loading="productsLoading"
              v-model:pagination="productPagination"
              :rows-per-page-options="[10, 25, 50, 100]"
              binary-state-sort
              @request="loadProducts"
              @row-click="(evt, row) => router.push({ name: 'catalog-product-detail', params: { id: row.id } })"
            >
              <template #top>
                <div class="app-section__title col ellipsis">Products in this category</div>
                <q-input v-model="productSearch" dense outlined debounce="300" placeholder="Search name, SKU or GTIN" style="min-width: 240px" @update:model-value="reloadProducts">
                  <template #prepend><q-icon name="o_search" /></template>
                </q-input>
              </template>
              <template #body-cell-image="p">
                <q-td :props="p">
                  <img v-if="p.row.imageUrl" :src="$media(p.row.imageUrl)" :alt="p.row.name" class="product-thumb">
                  <q-icon v-else name="o_image_not_supported" size="24px" color="grey-5" />
                </q-td>
              </template>
              <template #no-data>
                <div class="full-width text-center text-grey-6 q-pa-md">No products assigned — assign categories from a product's Organization tab.</div>
              </template>
            </q-table>
          </q-tab-panel>

          <!-- ============ MEDIA ============ -->
          <q-tab-panel name="media" class="q-gutter-y-sm">
            <AppFieldLabel label="Images">
              <template #hint>Uploaded to the media library; display order follows the upload sequence</template>
            </AppFieldLabel>
            <div v-if="pictures.length" class="row q-col-gutter-sm q-mb-sm">
              <div v-for="pic in pictures" :key="pic.id" class="col-auto">
                <div class="detail-pic">
                  <img :src="$media(pic.url)" :alt="pic.altText || (entity && entity.name)" class="detail-pic__img" @click="openPicture(pic)">
                  <q-btn v-if="canWrite" round dense size="xs" color="negative" icon="o_close" class="detail-pic__remove" @click="removePicture(pic)" />
                </div>
              </div>
            </div>
            <div v-else class="text-grey-6 text-caption q-mb-sm">No images yet.</div>

            <q-file
              v-if="canWrite"
              v-model="imageFiles"
              multiple dense outlined
              accept="image/*"
              label="Upload images"
              hint="PNG, JPG, GIF, WEBP — added to the media library"
              :loading="uploadingImages"
              @update:model-value="onImageFiles"
            >
              <template #prepend><q-icon name="o_add_photo_alternate" /></template>
            </q-file>
          </q-tab-panel>
        </q-tab-panels>

        <template v-if="isCreate">
          <q-separator />
          <q-card-actions class="q-pa-md">
            <div class="text-caption text-grey-7">
              Create the category to unlock images, SEO, filters, products and media — all auto-saved from then on.
            </div>
            <q-space />
            <q-btn v-if="canWrite" unelevated color="primary" no-caps icon="o_check" label="Create category" :loading="creating" @click="create" />
          </q-card-actions>
        </template>
      </q-card>
    </template>

    <MediaSeoDialog v-model="lightboxOpen" :media-id="selectedMediaId" :fallback-url="lightboxUrl" @saved="onPictureSaved" />

    <AppRecordMeta entity-type="category" :record-id="entity?.id" />
  </q-page>
</template>

<script setup>
/*
 * Category create + manage page (full-page auto-save pattern via useDetailForm). Create mode: only
 * General is enabled; SEO / Media unlock once the category exists. Manage mode: fields auto-save on
 * change; media (CategoryPictures) add/remove stay explicit.
 */
import { ref, computed } from 'vue'
import { useRouter } from 'vue-router'
import { getApiErrorMessage } from 'services/api'
import { categoryApi, mediaApi, productApi, specificationAttributeApi } from 'modules/catalog/api'
import { usePermissions } from 'composables/usePermissions'
import { useNotify } from 'composables/useNotify'
import { useDetailForm } from 'composables/useDetailForm'
import { deleteConfirmation } from 'dialogs/delete_confirmation'
import { required, maxLength } from 'validators'
import AppDetailHeader from 'components/common/AppDetailHeader.vue'
import AppTextField from 'components/common/AppTextField.vue'
import AppSelect from 'components/common/AppSelect.vue'
import AppRichText from 'components/common/AppRichText.vue'
import AppFieldLabel from 'components/common/AppFieldLabel.vue'
import MediaImageField from 'modules/catalog/components/MediaImageField.vue'
import { debounce } from 'quasar'

const router = useRouter()
const notify = useNotify()
const { has } = usePermissions()
const canWrite = computed(() => has('Catalog.Write'))

const tab = ref('general')

function buildPayload (form) {
  return {
    name: form.name,
    parentId: form.parentId || null,
    slug: form.slug || null,
    description: form.description || null,
    displayOrder: Number(form.displayOrder) || 0,
    isEnabled: form.isEnabled,
    summary: form.summary || null,
    showInMenu: form.showInMenu,
    imageMediaId: form.imageMediaId || null,
    imageAltText: form.imageAltText || null,
    image2MediaId: form.image2MediaId || null,
    image2AltText: form.image2AltText || null
  }
}

// Preview URLs for the media-backed image fields (the form only holds media ids).
const imageUrl = ref(null)
const image2Url = ref(null)

const {
  form, v$, entity, loading, creating, isCreate, id, saveStatus, create, markSaved, runSave
} = useDetailForm({
  createRouteName: 'catalog-category-new',
  detailRouteName: 'catalog-category-detail',
  entityLabel: 'category',
  deriveSlug: true,
  api: categoryApi,
  buildPayload,
  empty: {
    name: '', parentId: null, slug: '', description: '',
    displayOrder: 0, isEnabled: true,
    summary: '', showInMenu: true,
    imageMediaId: null, imageAltText: '', image2MediaId: null, image2AltText: ''
  },
  rules: {
    name: { required, maxLength: maxLength(200) },
    slug: { maxLength: maxLength(220) },
    summary: { maxLength: maxLength(2000) },
    imageAltText: { maxLength: maxLength(300) },
    image2AltText: { maxLength: maxLength(300) }
  },
  hydrateForm: (f, e) => {
    imageUrl.value = e.imageUrl || null
    image2Url.value = e.image2Url || null
  },
  afterLoad: (e) => { loadTree(); loadPictures(); hydrateFilters(e); loadSpecAttributes(); reloadProducts() },
  resetExtra: () => {
    pictures.value = []; products.value = []; productTotal.value = 0
    imageUrl.value = null; image2Url.value = null
    loadTree()
  }
})

// ---- Filters (brand toggle + ordered specification attributes) ------------------
const showBrandFilter = ref(false)
const filterAttributeIds = ref([])
const specAttributeOptions = ref([])
const filterCount = computed(() => filterAttributeIds.value.length + (showBrandFilter.value ? 1 : 0))
let hydratingFilters = false

function hydrateFilters (e) {
  hydratingFilters = true
  showBrandFilter.value = !!e.showBrandFilter
  filterAttributeIds.value = [...(e.filterSpecificationAttributeIds || [])]
  hydratingFilters = false
}

async function loadSpecAttributes () {
  try {
    // Every specification attribute is listed (like the PHP Filters tab lists every dropdown attribute);
    // non-filterable ones are flagged rather than hidden.
    const result = await specificationAttributeApi.list({ page: 1, pageSize: 200 })
    const items = Array.isArray(result) ? result : result?.items || []
    specAttributeOptions.value = items.map((a) => ({ label: a.name, value: a.id, isFilterable: a.isFilterable !== false }))
  } catch (err) { specAttributeOptions.value = [] }
}

const queueFilters = debounce(async () => {
  if (hydratingFilters) return
  try {
    // Checkboxes have no pick order — save in the attribute list's display order.
    const checked = new Set(filterAttributeIds.value)
    await categoryApi.setFilters(id.value, {
      showBrandFilter: showBrandFilter.value,
      specificationAttributeIds: specAttributeOptions.value.filter((o) => checked.has(o.value)).map((o) => o.value)
    })
    markSaved()
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  }
}, 700)

// ---- Products in this category (server-paged) -----------------------------------
const products = ref([])
const productTotal = ref(0)
const productsLoading = ref(false)
const productSearch = ref('')
const productPagination = ref({ sortBy: 'name', descending: false, page: 1, rowsPerPage: 25, rowsNumber: 0 })
const productColumns = [
  { name: 'image', label: 'Image', align: 'left', field: 'imageUrl', style: 'width: 64px' },
  { name: 'name', label: 'Name', align: 'left', field: 'name', sortable: true },
  { name: 'sku', label: 'SKU', align: 'left', field: (r) => r.sku || '—', sortable: true },
  // Stock is a rollup over per-store inventory — not sortable server-side.
  { name: 'stockQuantity', label: 'Qty in stock', align: 'right', field: 'stockQuantity' }
]

async function loadProducts (props) {
  const pag = props?.pagination || productPagination.value
  productsLoading.value = true
  try {
    const result = await productApi.list({
      categoryId: id.value,
      search: productSearch.value || undefined,
      page: pag.page,
      pageSize: pag.rowsPerPage || 100,
      sortBy: pag.sortBy || undefined,
      sortDescending: pag.descending
    })
    products.value = result?.items || []
    productTotal.value = result?.totalCount ?? products.value.length
    productPagination.value = { ...pag, rowsNumber: productTotal.value }
  } catch (err) {
    products.value = []
    notify.error(getApiErrorMessage(err))
  } finally {
    productsLoading.value = false
  }
}
function reloadProducts () { loadProducts({ pagination: { ...productPagination.value, page: 1 } }) }

// ---- Parent options (flattened tree, current node excluded) --------------------
const parentOptions = ref([])
const availableParents = computed(() => parentOptions.value.filter((o) => o.value !== id.value))

function flattenTree (nodes, depth = 0, acc = []) {
  for (const node of nodes || []) {
    acc.push({ label: `${'— '.repeat(depth)}${node.name}`, value: node.id })
    if (node.children && node.children.length) flattenTree(node.children, depth + 1, acc)
  }
  return acc
}
async function loadTree () {
  try {
    const tree = await categoryApi.tree()
    parentOptions.value = flattenTree(Array.isArray(tree) ? tree : [])
  } catch (err) { parentOptions.value = [] }
}

// ---- Media (CategoryPictures) --------------------------------------------------
const pictures = ref([])
const imageFiles = ref(null)
const uploadingImages = ref(false)
const lightboxOpen = ref(false)
const lightboxUrl = ref('')
const selectedMediaId = ref(null)

async function loadPictures () {
  try {
    pictures.value = await categoryApi.listPictures(id.value)
  } catch (err) { pictures.value = [] }
}

function openPicture (pic) {
  selectedMediaId.value = pic.mediaId || null
  lightboxUrl.value = pic.url
  lightboxOpen.value = true
}
async function onPictureSaved () {
  try { pictures.value = await categoryApi.listPictures(id.value) } catch (e) { /* keep current */ }
}

async function onImageFiles (selection) {
  const files = Array.isArray(selection) ? selection : (selection ? [selection] : [])
  if (!files.length) return
  uploadingImages.value = true
  try {
    let added = 0
    for (const file of files) {
      const draft = await mediaApi.prepare(file)
      const committed = await mediaApi.commit({
        tempId: draft.tempId,
        seoFileName: draft.suggestedSeoFileName,
        altText: form.name || null,
        title: null,
        caption: null,
        description: null
      })
      await categoryApi.assignPicture(id.value, { mediaId: committed.mediaId })
      added++
    }
    notify.success(added > 1 ? `${added} images added` : 'Image added')
    markSaved()
    await loadPictures()
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    uploadingImages.value = false
    imageFiles.value = null
  }
}

async function removePicture (pic) {
  if (!(await deleteConfirmation('this image', { title: 'Remove', okLabel: 'Remove', message: 'Are you sure you want to remove this image?' }))) return
  try {
    await categoryApi.removePicture(pic.id)
    pictures.value = pictures.value.filter((p) => p.id !== pic.id)
    notify.success('Image removed')
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  }
}
</script>

<style scoped lang="scss">
.app-detail-tabs {
  :deep(.q-tab) { min-height: 44px; }
}

.filter-list {
  display: flex; flex-direction: column; max-width: 560px;
  &__item { padding: 4px 0; }
}

.product-thumb {
  width: 40px; height: 40px; object-fit: cover; display: block;
  border: 1px solid rgba(0, 0, 0, 0.12); border-radius: 4px; background: #fafafa;
}

.detail-pic {
  position: relative;
  width: 96px;
  &__img {
    width: 96px; height: 96px; object-fit: cover;
    border: 1px solid rgba(0, 0, 0, 0.12); border-radius: 6px; cursor: pointer;
  }
  &__remove { position: absolute; top: -8px; right: -8px; }
}

.detail-lightbox {
  position: relative; width: 96vw; max-width: 1100px; background: transparent; box-shadow: none;
  &__img { width: 100%; height: auto; display: block; border-radius: 6px; }
  &__close { position: absolute; top: 8px; right: 8px; z-index: 2; background: rgba(255, 255, 255, 0.9); }
}
</style>
