<template>
  <q-page class="app-page">
    <AppListHeader
      title="Category Page Config"
      subtitle="Add a banner, promo copy, a “you may also like” collection and pinned products to a category page."
      :breadcrumbs="[{ label: 'Home', icon: 'o_home', to: '/dashboard' }, { label: 'Category Page Config' }]"
    />

    <!-- Category picker -->
    <q-card flat bordered class="app-section q-pa-md">
      <div style="max-width: 520px">
        <AppSelect
          :model-value="categoryId"
          label="Category"
          :options="categoryOptions"
          use-input
          clearable
          hint="Choose a category to configure its storefront page"
          @filter="filterCategories"
          @update:model-value="onSelectCategory"
        />
      </div>
    </q-card>

    <template v-if="categoryId">
      <q-inner-loading :showing="loading" color="primary" />

      <!-- Banner + promo + YMAL -->
      <q-card flat bordered class="app-section q-pa-md q-mt-md q-gutter-y-sm">
        <div class="app-section__title q-mb-xs">Page content</div>

        <AppFileUpload
          media
          v-model="form.bannerMediaId"
          v-model:preview-url="form.bannerImageUrl"
          label="Banner image"
          accept="image/*"
          extensions-label="PNG, JPG, WEBP"
          :disable="!canWrite"
        />

        <AppRichText v-model="form.promotionalDescription" label="Promotional description" placeholder="Optional promo copy shown on the category page…" :disable="!canWrite" />

        <AppSelect
          v-model="form.ymalCollectionId"
          label="“You may also like” collection"
          :options="collectionOptions"
          clearable
          hint="A product collection surfaced as a recommendations row"
          :disable="!canWrite"
        >
          <template #no-option>
            <q-item><q-item-section class="text-grey-6">No collections yet</q-item-section></q-item>
          </template>
        </AppSelect>
      </q-card>

      <!-- Pinned products -->
      <q-card flat bordered class="app-section q-pa-md q-mt-md">
        <div class="row items-center q-mb-sm">
          <div class="col">
            <div class="app-section__title">Pinned products</div>
            <div class="text-caption text-grey-7">Shown first on the category page, in this order. Saved with the page.</div>
          </div>
          <q-badge color="blue-1" text-color="primary" :label="`${pinned.length} pinned`" />
        </div>

        <div v-if="canWrite" class="q-mb-md" style="max-width: 460px">
          <ProductPicker :exclude-ids="pinnedIds" @select="addPinned" />
        </div>

        <div v-if="!pinned.length" class="text-grey-6 text-caption q-py-md text-center">
          No pinned products — search above to add one.
        </div>

        <q-list v-else separator>
          <q-item v-for="(p, i) in pinned" :key="p.productId" class="q-py-sm">
            <q-item-section side>
              <div class="column">
                <q-btn flat dense round size="sm" icon="o_arrow_upward" :disable="i === 0 || !canWrite" @click="movePinned(i, -1)">
                  <q-tooltip>Move up</q-tooltip>
                </q-btn>
                <q-btn flat dense round size="sm" icon="o_arrow_downward" :disable="i === pinned.length - 1 || !canWrite" @click="movePinned(i, 1)">
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
              <q-item-label v-if="p.sku" caption>SKU: {{ p.sku }}</q-item-label>
            </q-item-section>

            <q-item-section side>
              <q-btn flat round dense icon="o_delete" color="negative" :disable="!canWrite" @click="removePinned(i)">
                <q-tooltip>Remove</q-tooltip>
              </q-btn>
            </q-item-section>
          </q-item>
        </q-list>
      </q-card>

      <!-- Actions -->
      <div class="row items-center q-mt-md">
        <q-btn v-if="hasConfig && canWrite" flat no-caps color="negative" icon="o_delete_sweep" label="Reset page config" :loading="removing" @click="onRemoveConfig" />
        <q-space />
        <q-btn v-if="canWrite" unelevated no-caps color="primary" icon="o_save" label="Save category page" :loading="saving" @click="save" />
      </div>
    </template>

    <q-banner v-else class="bg-grey-2 rounded-borders q-mt-md">
      Pick a category above to configure its page.
    </q-banner>
  </q-page>
</template>

<script setup>
/*
 * Category Page Config manager (WO-99). Pick a category, then edit its storefront page: a banner
 * (AppFileUpload media mode → bannerMediaId + preview), promotional copy (AppRichText), a YMAL
 * product collection, and an ordered list of pinned products (product picker + up/down arrows —
 * order is part of the Save, there's no separate reorder endpoint). Save PUTs the whole config with
 * pinnedProductIds in order; Reset DELETEs it. The GET returns an empty/default record when none
 * exists yet, so the form simply starts blank.
 */
import { ref, reactive, computed, onMounted } from 'vue'
import { categoryPageConfigApi, collectionApi } from '../api'
import { categoryApi } from 'modules/catalog/api'
import { moveInArray } from '../reorder'
import ProductPicker from '../components/ProductPicker.vue'
import { getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'
import { usePermissions } from 'composables/usePermissions'
import { deleteConfirmation } from 'dialogs/delete_confirmation'

const notify = useNotify()
const { has } = usePermissions()
const canWrite = computed(() => has('Cms.Write'))

const categoryId = ref(null)
const loading = ref(false)
const saving = ref(false)
const removing = ref(false)
const hasConfig = ref(false)

const form = reactive({
  bannerMediaId: null,
  bannerImageUrl: '',
  promotionalDescription: '',
  ymalCollectionId: null
})

const pinned = ref([]) // [{ productId, name, sku, imageUrl }]
const pinnedIds = computed(() => pinned.value.map((p) => p.productId))

const allCategoryOptions = ref([])
const categoryOptions = ref([])
const collectionOptions = ref([])

function resetForm () {
  form.bannerMediaId = null
  form.bannerImageUrl = ''
  form.promotionalDescription = ''
  form.ymalCollectionId = null
  pinned.value = []
  hasConfig.value = false
}

// ---- Load option catalogs --------------------------------------------------
async function loadCategories () {
  try {
    const result = await categoryApi.list({ page: 1, pageSize: 500 })
    const items = Array.isArray(result) ? result : result?.items || result?.data || []
    allCategoryOptions.value = items.map((c) => ({ label: c.name, value: c.id }))
    categoryOptions.value = allCategoryOptions.value
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  }
}

async function loadCollections () {
  try {
    const result = await collectionApi.list({ page: 1, pageSize: 200 })
    const items = Array.isArray(result) ? result : result?.items || result?.data || []
    collectionOptions.value = items.map((c) => ({ label: c.name, value: c.id }))
  } catch {
    collectionOptions.value = []
  }
}

function filterCategories (val, update) {
  update(() => {
    const q = (val || '').toLowerCase()
    categoryOptions.value = q
      ? allCategoryOptions.value.filter((o) => o.label.toLowerCase().includes(q))
      : allCategoryOptions.value
  })
}

// ---- Category selection / load config --------------------------------------
async function onSelectCategory (val) {
  categoryId.value = val
  resetForm()
  if (!val) return
  loading.value = true
  try {
    const cfg = await categoryPageConfigApi.get(val)
    form.bannerMediaId = cfg?.bannerMediaId || null
    form.bannerImageUrl = cfg?.bannerImageUrl || ''
    form.promotionalDescription = cfg?.promotionalDescription || ''
    form.ymalCollectionId = cfg?.ymalCollectionId || null
    pinned.value = (cfg?.pinnedProducts || [])
      .slice()
      .sort((a, b) => (a.displayOrder || 0) - (b.displayOrder || 0))
      .map((p) => ({ productId: p.productId, name: p.name, sku: p.sku, imageUrl: p.imageUrl }))
    hasConfig.value = !!(cfg?.bannerMediaId || cfg?.promotionalDescription || cfg?.ymalCollectionId || pinned.value.length)
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    loading.value = false
  }
}

// ---- Pinned products (local; order sent on Save) ---------------------------
function addPinned (product) {
  if (pinned.value.some((p) => p.productId === product.id)) return
  pinned.value = pinned.value.concat({
    productId: product.id,
    name: product.name,
    sku: product.sku,
    imageUrl: product.imageUrl
  })
}

function removePinned (i) {
  pinned.value = pinned.value.filter((_, idx) => idx !== i)
}

function movePinned (i, dir) {
  pinned.value = moveInArray(pinned.value, i, dir)
}

// ---- Save / reset ----------------------------------------------------------
async function save () {
  saving.value = true
  try {
    await categoryPageConfigApi.update(categoryId.value, {
      bannerMediaId: form.bannerMediaId || null,
      promotionalDescription: form.promotionalDescription || null,
      ymalCollectionId: form.ymalCollectionId || null,
      pinnedProductIds: pinned.value.map((p) => p.productId)
    })
    hasConfig.value = true
    notify.success('Category page saved')
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    saving.value = false
  }
}

async function onRemoveConfig () {
  if (!(await deleteConfirmation('this category page configuration', {
    title: 'Reset page config',
    okLabel: 'Reset',
    message: 'Remove the banner, promo copy, YMAL collection and pinned products for this category?'
  }))) return
  removing.value = true
  try {
    await categoryPageConfigApi.remove(categoryId.value)
    resetForm()
    notify.success('Category page configuration removed')
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    removing.value = false
  }
}

onMounted(() => {
  loadCategories()
  loadCollections()
})
</script>
