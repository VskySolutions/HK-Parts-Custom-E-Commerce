<template>
  <q-page class="app-page">
    <AppListHeader
      title="Home Page Sections"
      subtitle="Compose the storefront home page from ordered, toggleable content sections."
      :breadcrumbs="[{ label: 'Home', icon: 'o_home', to: '/dashboard' }, { label: 'Home Page Sections' }]"
    >
      <template #actions>
        <q-btn v-if="canWrite" unelevated color="primary" no-caps icon="o_add" label="Add section">
          <q-menu auto-close anchor="bottom right" self="top right">
            <q-list style="min-width: 230px">
              <q-item-label header class="text-caption">Choose a section type</q-item-label>
              <q-item v-for="t in sectionTypeOptions" :key="t.value" clickable @click="openCreate(t.value)">
                <q-item-section avatar><q-icon :name="t.icon" color="primary" /></q-item-section>
                <q-item-section>{{ t.label }}</q-item-section>
              </q-item>
            </q-list>
          </q-menu>
        </q-btn>
      </template>
    </AppListHeader>

    <AppSection title="Sections" subtitle="Shown top-to-bottom on the home page. Use the arrows to reorder.">
      <q-inner-loading :showing="loading" color="primary" />

      <div v-if="!loading && !sections.length" class="text-grey-6 text-body2 q-py-lg text-center">
        <q-icon name="o_dashboard_customize" size="28px" class="q-mb-sm block" />
        No sections yet. Use “Add section” to build the home page.
      </div>

      <q-list v-if="sections.length" separator>
        <q-item v-for="(s, i) in sections" :key="s.id" class="q-py-sm">
          <!-- Reorder -->
          <q-item-section side>
            <div class="column">
              <q-btn flat dense round size="sm" icon="o_arrow_upward" :disable="i === 0 || !canWrite || reordering" @click="move(i, -1)">
                <q-tooltip>Move up</q-tooltip>
              </q-btn>
              <q-btn flat dense round size="sm" icon="o_arrow_downward" :disable="i === sections.length - 1 || !canWrite || reordering" @click="move(i, 1)">
                <q-tooltip>Move down</q-tooltip>
              </q-btn>
            </div>
          </q-item-section>

          <!-- Type icon -->
          <q-item-section side>
            <q-avatar rounded size="38px" color="blue-1" text-color="primary" :icon="sectionTypeMeta(s.sectionType).icon" />
          </q-item-section>

          <!-- Name + summary -->
          <q-item-section>
            <q-item-label class="row items-center q-gutter-xs">
              <span class="text-weight-medium">{{ s.displayName || sectionTypeMeta(s.sectionType).label }}</span>
              <q-badge outline color="primary" :label="sectionTypeMeta(s.sectionType).label" />
            </q-item-label>
            <q-item-label caption>{{ summary(s) }}</q-item-label>
          </q-item-section>

          <!-- Enabled toggle -->
          <q-item-section side>
            <q-toggle
              :model-value="s.isEnabled"
              color="primary"
              :label="s.isEnabled ? 'Enabled' : 'Disabled'"
              left-label
              :disable="!canWrite || togglingId === s.id"
              @update:model-value="(val) => toggleEnabled(s, val)"
            />
          </q-item-section>

          <!-- Actions -->
          <q-item-section side>
            <div class="row no-wrap">
              <q-btn flat round dense icon="o_edit" color="primary" :disable="!canWrite" @click="openEdit(s)">
                <q-tooltip>Edit</q-tooltip>
              </q-btn>
              <q-btn flat round dense icon="o_delete" color="negative" :disable="!canWrite" @click="onDelete(s)">
                <q-tooltip>Delete</q-tooltip>
              </q-btn>
            </div>
          </q-item-section>
        </q-item>
      </q-list>
    </AppSection>

    <!-- ============ CONFIG DRAWER (create + edit) ============ -->
    <AppFormDrawer
      v-model="drawerOpen"
      :title="editingId ? `Edit ${sectionTypeMeta(draft.sectionType).label}` : `Add ${sectionTypeMeta(draft.sectionType).label}`"
      :submit-label="editingId ? 'Save section' : 'Add section'"
      :saving="saving"
      @submit="save"
    >
      <div class="q-gutter-y-sm">
        <div class="row items-center q-gutter-xs q-mb-sm">
          <q-icon :name="sectionTypeMeta(draft.sectionType).icon" color="primary" />
          <span class="text-weight-medium">{{ sectionTypeMeta(draft.sectionType).label }}</span>
        </div>

        <AppTextField
          v-model="draft.displayName"
          label="Display name"
          required
          :rules="[requiredRule]"
          placeholder="e.g. Shop by category"
          hint="Internal label + optional heading for the section"
        />

        <!-- maxItems for the 'row' sections -->
        <AppTextField
          v-if="showMaxItems"
          v-model.number="draft.maxItems"
          label="Max items"
          type="number"
          min="1"
          hint="Most items to show in this section"
        />

        <!-- HeroBanner -->
        <AppTextField
          v-if="draft.sectionType === 'HeroBanner'"
          v-model="draft.bannerLocation"
          label="Banner location"
          placeholder="home-hero"
          hint="The banner placement key rendered in this slot"
        />

        <!-- ProductRow -->
        <template v-if="draft.sectionType === 'ProductRow'">
          <AppSelect
            v-model="draft.productRowSource"
            label="Products source"
            :options="productRowSourceOptions"
          />
          <AppSelect
            v-if="draft.productRowSource === 'Collection'"
            v-model="draft.collectionId"
            label="Collection"
            :options="collectionOptions"
            hint="Which product collection fills this row"
          >
            <template #no-option>
              <q-item><q-item-section class="text-grey-6">No collections yet — create one first</q-item-section></q-item>
            </template>
          </AppSelect>
          <AppSelect
            v-else
            v-model="draft.rule"
            label="Rule"
            :options="autoRuleOptions"
            hint="How products for this row are chosen automatically"
          />
        </template>

        <!-- CustomHtmlBlock -->
        <div v-if="draft.sectionType === 'CustomHtmlBlock'">
          <div class="row items-center q-mb-sm">
            <q-toggle v-model="draft.rawHtmlMode" color="primary" label="Raw HTML mode" />
            <q-space />
            <span class="text-caption text-grey-6">{{ draft.rawHtmlMode ? 'Edit raw HTML directly' : 'Use the visual editor' }}</span>
          </div>
          <AppRichText
            v-if="!draft.rawHtmlMode"
            v-model="draft.html"
            label="HTML content"
            placeholder="Write the custom block content…"
            :hide-viewsource="true"
          />
          <q-input
            v-else
            v-model="draft.html"
            type="textarea"
            input-style="font-family: 'Consolas', 'Monaco', 'Courier New', monospace; font-size: 13px; line-height: 1.5; tab-size: 2; min-height: 400px; white-space: pre; overflow-x: auto;"
            dense
            outlined
            placeholder="Paste or write raw HTML here…"
            hint="Raw HTML — rendered as-is on the storefront."
          />
        </div>

        <q-separator class="q-my-sm" />
        <q-toggle v-model="draft.isEnabled" color="primary" label="Enabled" />
        <div v-if="draft.sectionType === 'HeroBanner'" class="text-caption text-grey-7">
          Only one hero banner can be enabled at a time.
        </div>
      </div>
    </AppFormDrawer>
  </q-page>
</template>

<script setup>
/*
 * Home Page Sections manager (WO-96). A single page that lists the home sections in display order
 * with up/down reorder buttons (no drag-drop lib — the ordered id list is PUT to /reorder), a
 * per-section enabled toggle (PUT /{id}/enabled), a type badge, and edit/delete. "Add section"
 * offers the five section types; each opens the shared config drawer (AppFormDrawer) whose fields
 * vary by type. The single-enabled-HeroBanner rule is enforced client-side (and the backend 409s,
 * surfaced via notify).
 */
import { ref, reactive, computed, onMounted } from 'vue'
import {
  homeSectionApi, collectionApi, sectionTypeOptions, sectionTypeMeta,
  productRowSourceOptions, autoRuleOptions, autoRuleLabel, MAX_ITEMS_TYPES
} from '../api'
import { moveInArray } from '../reorder'
import { getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'
import { usePermissions } from 'composables/usePermissions'
import { deleteConfirmation } from 'dialogs/delete_confirmation'

const notify = useNotify()
const { has } = usePermissions()
const canWrite = computed(() => has('Cms.Write'))

const sections = ref([])
const loading = ref(false)
const reordering = ref(false)
const togglingId = ref(null)

const collectionOptions = ref([])

const drawerOpen = ref(false)
const saving = ref(false)
const editingId = ref(null)
const draft = reactive({
  sectionType: 'HeroBanner',
  displayName: '',
  isEnabled: true,
  maxItems: 8,
  bannerLocation: 'home-hero',
  productRowSource: 'Collection',
  collectionId: null,
  rule: 'NewArrivals',
  html: '',
  rawHtmlMode: false
})

const requiredRule = (v) => (!!v && String(v).trim().length > 0) || 'Display name is required'
const showMaxItems = computed(() => MAX_ITEMS_TYPES.includes(draft.sectionType))

// ---- Load ------------------------------------------------------------------
async function load () {
  loading.value = true
  try {
    const result = await homeSectionApi.list()
    const items = Array.isArray(result) ? result : result?.items || result?.data || []
    sections.value = items.slice().sort((a, b) => (a.displayOrder || 0) - (b.displayOrder || 0))
  } catch (err) {
    sections.value = []
    notify.error(getApiErrorMessage(err))
  } finally {
    loading.value = false
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

// ---- Summaries -------------------------------------------------------------
function collectionName (id) {
  const match = collectionOptions.value.find((o) => o.value === id)
  return match ? match.label : 'a collection'
}

function summary (s) {
  const c = s.config || {}
  switch (s.sectionType) {
    case 'HeroBanner':
      return `Banner location: ${c.bannerLocation || 'home-hero'}`
    case 'ProductRow':
      return c.productRowSource === 'Auto'
        ? `Auto · ${autoRuleLabel(c.rule)}${c.maxItems ? ` · up to ${c.maxItems}` : ''}`
        : `Collection · ${collectionName(c.collectionId)}${c.maxItems ? ` · up to ${c.maxItems}` : ''}`
    case 'CustomHtmlBlock':
      return 'Custom HTML block'
    default:
      return c.maxItems ? `Up to ${c.maxItems} items` : 'No limit set'
  }
}

// ---- Reorder (up/down → PUT the whole ordered id list) ---------------------
async function move (i, dir) {
  const target = i + dir
  if (target < 0 || target >= sections.value.length) return
  const prev = sections.value
  sections.value = moveInArray(sections.value, i, dir)
  reordering.value = true
  try {
    await homeSectionApi.reorder(sections.value.map((s) => s.id))
  } catch (err) {
    sections.value = prev
    notify.error(getApiErrorMessage(err))
  } finally {
    reordering.value = false
  }
}

// ---- Enabled toggle --------------------------------------------------------
function hasAnotherEnabledHero (exceptId) {
  return sections.value.some((s) => s.id !== exceptId && s.sectionType === 'HeroBanner' && s.isEnabled)
}

async function toggleEnabled (section, val) {
  if (val && section.sectionType === 'HeroBanner' && hasAnotherEnabledHero(section.id)) {
    notify.warning('Only one hero banner can be enabled at a time. Disable the other one first.')
    return
  }
  togglingId.value = section.id
  try {
    await homeSectionApi.setEnabled(section.id, val)
    section.isEnabled = val
    notify.success(val ? 'Section enabled' : 'Section disabled')
  } catch (err) {
    notify.error(getApiErrorMessage(err))
    await load() // resync from the server (e.g. a 409 from the single-hero rule)
  } finally {
    togglingId.value = null
  }
}

// ---- Create / edit ---------------------------------------------------------
function openCreate (type) {
  editingId.value = null
  Object.assign(draft, {
    sectionType: type,
    displayName: sectionTypeMeta(type).label,
    isEnabled: true,
    maxItems: 8,
    bannerLocation: 'home-hero',
    productRowSource: 'Collection',
    collectionId: null,
    rule: 'NewArrivals',
    html: '',
    rawHtmlMode: false
  })
  drawerOpen.value = true
}

function openEdit (s) {
  editingId.value = s.id
  const c = s.config || {}
  Object.assign(draft, {
    sectionType: s.sectionType,
    displayName: s.displayName || '',
    isEnabled: !!s.isEnabled,
    maxItems: c.maxItems ?? 8,
    bannerLocation: c.bannerLocation || 'home-hero',
    productRowSource: c.productRowSource || 'Collection',
    collectionId: c.collectionId || null,
    rule: c.rule || 'NewArrivals',
    html: c.html || '',
    rawHtmlMode: false
  })
  drawerOpen.value = true
}

// Serialize the type-specific fields into the `config` object the API expects.
function buildConfig () {
  const t = draft.sectionType
  const cfg = {}
  if (MAX_ITEMS_TYPES.includes(t)) cfg.maxItems = toIntOrNull(draft.maxItems)
  if (t === 'HeroBanner') cfg.bannerLocation = draft.bannerLocation || 'home-hero'
  if (t === 'ProductRow') {
    cfg.productRowSource = draft.productRowSource
    if (draft.productRowSource === 'Collection') cfg.collectionId = draft.collectionId || null
    else cfg.rule = draft.rule || null
  }
  if (t === 'CustomHtmlBlock') cfg.html = draft.html || null
  return cfg
}

function toIntOrNull (v) {
  if (v === '' || v === null || v === undefined) return null
  const n = Number(v)
  return Number.isFinite(n) ? Math.trunc(n) : null
}

async function save () {
  // Type-specific guards the drawer's q-form validation can't express on selects.
  if (draft.sectionType === 'ProductRow') {
    if (draft.productRowSource === 'Collection' && !draft.collectionId) {
      notify.warning('Pick a collection for this product row.')
      return
    }
    if (draft.productRowSource === 'Auto' && !draft.rule) {
      notify.warning('Pick a rule for this product row.')
      return
    }
  }
  // Client-side single-enabled-HeroBanner rule (backend also enforces it → 409).
  if (draft.sectionType === 'HeroBanner' && draft.isEnabled && hasAnotherEnabledHero(editingId.value)) {
    notify.warning('Another hero banner is already enabled. Disable it first, or save this one disabled.')
    return
  }

  const payload = {
    sectionType: draft.sectionType,
    displayName: draft.displayName,
    isEnabled: draft.isEnabled,
    config: buildConfig()
  }

  saving.value = true
  try {
    if (editingId.value) {
      await homeSectionApi.update(editingId.value, payload)
      notify.success('Section updated')
    } else {
      await homeSectionApi.create(payload)
      notify.success('Section added')
    }
    drawerOpen.value = false
    await load()
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    saving.value = false
  }
}

async function onDelete (s) {
  if (!(await deleteConfirmation(`the “${s.displayName || sectionTypeMeta(s.sectionType).label}” section`))) return
  try {
    await homeSectionApi.remove(s.id)
    notify.success('Section deleted')
    await load()
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  }
}

onMounted(() => {
  load()
  loadCollections()
})
</script>
