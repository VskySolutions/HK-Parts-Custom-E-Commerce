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
            <AppRichText v-model="form.description" label="Description" placeholder="Describe this category…" hint="Rich text shown on the category page" :disable="!canWrite" />

            <q-separator class="q-my-sm" />
            <div class="row q-col-gutter-sm items-center">
              <div class="col-6 col-md-3">
                <AppTextField v-model="form.displayOrder" label="Display order" type="number" hint="Lower shows first" :disable="!canWrite" />
              </div>
              <div class="col-auto q-mt-md">
                <q-toggle v-model="form.isEnabled" label="Enabled" color="primary" :disable="!canWrite" />
              </div>
            </div>
          </q-tab-panel>

          <!-- ============ SEO ============ -->
          <q-tab-panel name="seo" class="q-gutter-y-sm">
            <q-card flat bordered class="q-pa-md q-mb-md seo-preview">
              <div class="seo-preview__title ellipsis">{{ seoPreview.title }}</div>
              <div class="seo-preview__url ellipsis">{{ seoPreview.url }}</div>
              <div class="seo-preview__desc">{{ seoPreview.description }}</div>
            </q-card>

            <AppTextField v-model="form.metaTitle" label="Meta title" placeholder="Defaults to the category name" :disable="!canWrite" maxlength="300">
              <template #hint>{{ metaTitleHint }}</template>
            </AppTextField>
            <AppTextField v-model="form.metaDescription" label="Meta description" type="textarea" autogrow placeholder="Plain-text summary for search engines" :disable="!canWrite" maxlength="500">
              <template #hint>{{ metaDescriptionHint }}</template>
            </AppTextField>
            <AppTextField v-model="form.metaKeywords" label="Meta keywords" placeholder="Comma-separated keywords" :disable="!canWrite" maxlength="500" />
            <AppTextField v-model="form.canonicalUrl" label="Canonical URL" placeholder="Optional — for duplicate-content canonicalization" :disable="!canWrite" />
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
              Create the category to unlock SEO and media — all auto-saved from then on.
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
import { categoryApi, mediaApi } from 'modules/catalog/api'
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
    metaTitle: form.metaTitle || null,
    metaDescription: form.metaDescription || null,
    metaKeywords: form.metaKeywords || null,
    canonicalUrl: form.canonicalUrl || null,
    displayOrder: Number(form.displayOrder) || 0,
    isEnabled: form.isEnabled
  }
}

const {
  form, v$, entity, loading, creating, isCreate, id, saveStatus, create, markSaved
} = useDetailForm({
  createRouteName: 'catalog-category-new',
  detailRouteName: 'catalog-category-detail',
  entityLabel: 'category',
  deriveSlug: true,
  api: categoryApi,
  buildPayload,
  empty: {
    name: '', parentId: null, slug: '', description: '',
    metaTitle: '', metaDescription: '', metaKeywords: '', canonicalUrl: '',
    displayOrder: 0, isEnabled: true
  },
  rules: {
    name: { required, maxLength: maxLength(200) },
    slug: { maxLength: maxLength(220) }
  },
  afterLoad: () => { loadTree(); loadPictures() },
  resetExtra: () => { pictures.value = []; loadTree() }
})

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

// ---- SEO preview ---------------------------------------------------------------
const seoPreview = computed(() => {
  const raw = form.metaDescription || 'A description of this category will appear here in search results.'
  return {
    title: form.metaTitle || form.name || 'Category title',
    url: `yourstore.com › shop › c › ${form.slug || 'category-slug'}`,
    description: raw.length > 160 ? `${raw.slice(0, 157)}…` : raw
  }
})
function lengthHint (value, ideal, fallbackLabel) {
  const n = (value || '').length
  if (!n) return `Falls back to the ${fallbackLabel}. Aim for ~${ideal} characters.`
  const note = n > ideal ? ' — may be truncated in results' : (n < ideal * 0.5 ? ' — consider adding detail' : ' — good length')
  return `${n} / ${ideal} recommended${note}`
}
const metaTitleHint = computed(() => lengthHint(form.metaTitle, 60, 'category name'))
const metaDescriptionHint = computed(() => lengthHint(form.metaDescription, 160, 'name'))

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

.seo-preview {
  max-width: 600px;
  &__title { color: #1a0dab; font-size: 18px; line-height: 1.3; }
  &__url { color: #006621; font-size: 13px; }
  &__desc { color: #545454; font-size: 13px; margin-top: 2px; }
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
