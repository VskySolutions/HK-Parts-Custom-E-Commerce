<template>
  <q-page class="app-page">
    <AppDetailHeader
      :title="isCreate ? 'New collection' : (entity?.name || 'Collection')"
      :breadcrumbs="[
        { label: 'Home', icon: 'o_home', to: '/dashboard' },
        { label: 'Product Collections', to: { name: 'cms-collections' } },
        { label: isCreate ? 'New collection' : (entity?.name || 'Collection') }
      ]"
      :status="!isCreate && entity ? (form.isEnabled ? 'Enabled' : 'Disabled') : ''"
      :status-color="form.isEnabled ? 'positive' : 'grey'"
      show-back
      @back="router.push({ name: 'cms-collections' })"
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
      Collection not found.
    </q-banner>

    <template v-if="isCreate || entity">
      <div v-if="!isCreate" class="row items-center text-caption text-grey-7 q-mb-sm q-px-xs">
        <q-icon name="o_cloud_sync" size="16px" class="q-mr-xs" />
        Changes to the details below are saved automatically — no need to press save.
      </div>

      <!-- ============ CORE FIELDS (auto-saved) ============ -->
      <q-card flat bordered class="app-section q-pa-md q-gutter-y-sm">
        <AppTextField v-model="form.name" label="Name" required :v="v$.name" placeholder="e.g. Summer essentials" :disable="!canWrite" />
        <AppTextField v-model="form.slug" label="Slug" :v="v$.slug" placeholder="e.g. summer-essentials" hint="Storefront URL — auto-filled from the name until you edit it" :disable="!canWrite" />
        <AppRichText v-model="form.description" label="Description" placeholder="Describe this collection…" :disable="!canWrite" />

        <q-separator class="q-my-sm" />
        <q-toggle v-model="form.isEnabled" label="Enabled" color="primary" :disable="!canWrite" />

        <template v-if="isCreate">
          <q-separator />
          <div class="row items-center q-pt-sm">
            <div class="text-caption text-grey-7">
              Create the collection to start adding products — everything auto-saves from then on.
            </div>
            <q-space />
            <q-btn v-if="canWrite" unelevated color="primary" no-caps icon="o_check" label="Create collection" :loading="creating" @click="create" />
          </div>
        </template>
      </q-card>

      <!-- ============ PRODUCTS (sub-resource; add / remove / reorder saved explicitly) ============ -->
      <q-card v-if="!isCreate" flat bordered class="app-section q-pa-md q-mt-md">
        <div class="row items-center q-mb-sm">
          <div class="col">
            <div class="app-section__title">Products</div>
            <div class="text-caption text-grey-7">Ordered as shown on the storefront. Use the arrows to reorder.</div>
          </div>
          <q-badge color="blue-1" text-color="primary" :label="`${items.length} products`" />
        </div>

        <div v-if="canWrite" class="q-mb-md" style="max-width: 460px">
          <ProductPicker :exclude-ids="itemIds" :disable="savingItems" @select="addItem" />
        </div>

        <div v-if="!items.length" class="text-grey-6 text-caption q-py-md text-center">
          No products yet — search above to add the first one.
        </div>

        <q-list v-else separator>
          <q-item v-for="(it, i) in items" :key="it.productId" class="q-py-sm">
            <q-item-section side>
              <div class="column">
                <q-btn flat dense round size="sm" icon="o_arrow_upward" :disable="i === 0 || !canWrite || savingItems" @click="moveItem(i, -1)">
                  <q-tooltip>Move up</q-tooltip>
                </q-btn>
                <q-btn flat dense round size="sm" icon="o_arrow_downward" :disable="i === items.length - 1 || !canWrite || savingItems" @click="moveItem(i, 1)">
                  <q-tooltip>Move down</q-tooltip>
                </q-btn>
              </div>
            </q-item-section>

            <q-item-section side>
              <q-avatar rounded size="44px" color="grey-2" text-color="grey-6">
                <img v-if="it.imageUrl" :src="$media(it.imageUrl)" :alt="it.productName">
                <q-icon v-else name="o_image" size="20px" />
              </q-avatar>
            </q-item-section>

            <q-item-section>
              <q-item-label lines="1">{{ it.productName }}</q-item-label>
              <q-item-label v-if="it.sku" caption>SKU: {{ it.sku }}</q-item-label>
            </q-item-section>

            <q-item-section side>
              <q-btn flat round dense icon="o_delete" color="negative" :disable="!canWrite || savingItems" @click="removeItem(it)">
                <q-tooltip>Remove</q-tooltip>
              </q-btn>
            </q-item-section>
          </q-item>
        </q-list>
      </q-card>
    </template>

    <AppRecordMeta entity-type="product-collection" :record-id="entity?.id" />
  </q-page>
</template>

<script setup>
/*
 * Product Collection create + manage page (WO-97; full-page auto-save via useDetailForm). The core
 * scalars (name / slug / description / enabled) auto-save on change in manage mode with the live
 * status chip. The ordered product list is a separate sub-resource saved explicitly through the
 * item endpoints: add (POST items), remove (DELETE items/{productId}) and reorder (PUT
 * items/reorder with the whole ordered productId list — no drag-drop lib). Products only appear
 * once the collection exists, mirroring the customer-group fixed-price editor.
 */
import { ref, computed } from 'vue'
import { useRouter } from 'vue-router'
import { collectionApi } from '../api'
import { moveInArray } from '../reorder'
import ProductPicker from '../components/ProductPicker.vue'
import { getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'
import { usePermissions } from 'composables/usePermissions'
import { useDetailForm } from 'composables/useDetailForm'
import { deleteConfirmation } from 'dialogs/delete_confirmation'
import { required, maxLength } from 'validators'

const router = useRouter()
const notify = useNotify()
const { has } = usePermissions()
const canWrite = computed(() => has('Cms.Write'))

function buildPayload (form) {
  return {
    name: form.name,
    slug: form.slug || null,
    description: form.description || null,
    isEnabled: form.isEnabled
  }
}

// ---- Items sub-resource (NOT part of the auto-saved core form) -------------
const items = ref([])
const savingItems = ref(false)
const itemIds = computed(() => items.value.map((it) => it.productId))

function seedItems (e) {
  items.value = (e.items || [])
    .slice()
    .sort((a, b) => (a.displayOrder || 0) - (b.displayOrder || 0))
}

const {
  form, v$, entity, loading, creating, isCreate, id, saveStatus, create, markSaved
} = useDetailForm({
  createRouteName: 'cms-collection-new',
  detailRouteName: 'cms-collection-detail',
  entityLabel: 'collection',
  deriveSlug: true,
  api: collectionApi,
  buildPayload,
  empty: { name: '', slug: '', description: '', isEnabled: true },
  rules: {
    name: { required, maxLength: maxLength(200) },
    slug: { maxLength: maxLength(220) }
  },
  afterLoad: (e) => seedItems(e),
  resetExtra: () => { items.value = [] }
})

// Re-fetch the collection to refresh the item list (fresh displayOrder + thumbnails) after a change.
async function reloadItems () {
  const fresh = await collectionApi.get(id.value)
  entity.value = fresh
  seedItems(fresh)
}

async function addItem (product) {
  if (items.value.some((it) => it.productId === product.id)) return
  savingItems.value = true
  try {
    await collectionApi.addItem(id.value, product.id)
    await reloadItems()
    markSaved()
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    savingItems.value = false
  }
}

async function removeItem (it) {
  if (!(await deleteConfirmation(`“${it.productName}” from this collection`, {
    title: 'Remove product',
    okLabel: 'Remove',
    message: `Remove “${it.productName}” from this collection?`
  }))) return
  savingItems.value = true
  try {
    await collectionApi.removeItem(id.value, it.productId)
    await reloadItems()
    markSaved()
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    savingItems.value = false
  }
}

async function moveItem (i, dir) {
  const target = i + dir
  if (target < 0 || target >= items.value.length) return
  const prev = items.value
  items.value = moveInArray(items.value, i, dir)
  savingItems.value = true
  try {
    await collectionApi.reorderItems(id.value, items.value.map((it) => it.productId))
    markSaved()
  } catch (err) {
    items.value = prev
    notify.error(getApiErrorMessage(err))
  } finally {
    savingItems.value = false
  }
}
</script>
