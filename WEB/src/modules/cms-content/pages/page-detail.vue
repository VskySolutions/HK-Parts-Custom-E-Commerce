<template>
  <q-page class="app-page">
    <AppDetailHeader
      :title="isCreate ? 'New page' : (entity?.title || 'Page')"
      :breadcrumbs="[
        { label: 'Home', icon: 'o_home', to: '/dashboard' },
        { label: 'CMS' },
        { label: 'Pages', to: { name: 'cms-pages' } },
        { label: isCreate ? 'New page' : (entity?.title || 'Page') }
      ]"
      :status="!isCreate && entity ? form.status : ''"
      :status-color="statusColor(form.status)"
      show-back
      @back="router.push({ name: 'cms-pages' })"
    >
      <template #actions>
        <q-chip
          v-if="!isCreate && entity?.isSystemPage"
          icon="o_lock"
          color="blue-grey-1"
          text-color="blue-grey-8"
          square
          dense
          class="q-mr-sm text-caption"
        >
          System page
        </q-chip>
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
      Page not found.
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
          <q-tab name="general" icon="o_description" label="Content" />
          <q-tab name="seo" icon="o_search" label="SEO" :disable="isCreate" />
        </q-tabs>

        <q-separator />

        <q-tab-panels v-model="tab" animated keep-alive>
          <!-- ============ CONTENT ============ -->
          <q-tab-panel name="general" class="q-gutter-y-sm">
            <AppTextField v-model="form.title" label="Title" required :v="v$.title" placeholder="e.g. About us" :disable="!canWrite" />
            <AppTextField v-model="form.slug" label="Slug" :v="v$.slug" placeholder="e.g. about-us" hint="Storefront URL — auto-filled from the title until you edit it" :disable="!canWrite" />

            <AppRichText v-model="form.body" label="Body" placeholder="Write the page content…" min-height="16rem" :disable="!canWrite" />

            <q-separator class="q-my-sm" />
            <div class="row q-col-gutter-sm items-start">
              <div class="col-12 col-md-6">
                <AppSelect v-model="form.pageGroupId" label="Page group" :options="groupOptions" hint="Groups related pages (e.g. footer links)" :disable="!canWrite" />
              </div>
              <div class="col-12 col-md-3">
                <AppSelect v-model="form.status" label="Status" :options="statusOptions" :disable="!canWrite" />
              </div>
              <div class="col-6 col-md-3">
                <AppTextField v-model="form.displayOrder" label="Display order" type="number" hint="Lower shows first" :disable="!canWrite" />
              </div>
            </div>

            <q-separator class="q-my-sm" />
            <!-- Storefront placement: quick-access link in the top bar and/or footer nav (bottom bar) -->
            <div class="row items-start q-col-gutter-md">
              <div class="col-12 col-md-6">
                <q-toggle v-model="form.showInTopBar" label="Show in top bar" left-label :disable="!canWrite" />
                <div class="text-caption text-grey-6 q-pl-sm">Quick-access link in the storefront navigation bar, after the category links</div>
              </div>
              <div class="col-12 col-md-6">
                <q-toggle v-model="form.showInFooter" label="Show in bottom bar" left-label :disable="!canWrite" />
                <div class="text-caption text-grey-6 q-pl-sm">Lists this page in the storefront footer links</div>
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

            <AppTextField v-model="form.metaTitle" label="Meta title" placeholder="Defaults to the page title" :disable="!canWrite" maxlength="300">
              <template #hint>{{ metaTitleHint }}</template>
            </AppTextField>
            <AppTextField v-model="form.metaDescription" label="Meta description" type="textarea" autogrow placeholder="Plain-text summary for search engines" :disable="!canWrite" maxlength="500">
              <template #hint>{{ metaDescriptionHint }}</template>
            </AppTextField>
            <AppTextField v-model="form.metaKeywords" label="Meta keywords" placeholder="Comma-separated keywords" :disable="!canWrite" maxlength="500" />
            <AppTextField v-model="form.canonicalUrl" label="Canonical URL" placeholder="https://…" hint="Optional — the preferred URL for this content" :disable="!canWrite" maxlength="500" />
          </q-tab-panel>
        </q-tab-panels>

        <template v-if="isCreate">
          <q-separator />
          <q-card-actions class="q-pa-md">
            <div class="text-caption text-grey-7">
              Create the page to unlock SEO — all auto-saved from then on.
            </div>
            <q-space />
            <q-btn v-if="canWrite" unelevated color="primary" no-caps icon="o_check" label="Create page" :loading="creating" @click="create" />
          </q-card-actions>
        </template>
      </q-card>
    </template>

    <AppRecordMeta entity-type="cms-page" :record-id="entity?.id" />
  </q-page>
</template>

<script setup>
/*
 * CMS Page create + manage page (WO-54; full-page auto-save via useDetailForm). Content (title / slug /
 * body / group / status / display order) and SEO (meta title/description/keywords + canonical URL) all
 * auto-save on change in manage mode, with the live status chip in the header. Slug is derived from the
 * title locally (useDetailForm's built-in deriveSlug watches `form.name`, but the primary field here is
 * `title`). Page groups are loaded once for the group select. System pages are flagged with a read-only
 * chip; the list hides their delete action.
 */
import { ref, computed, watch, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { cmsPageApi, cmsPageGroupApi, statusOptions, statusColor } from '../api'
import { usePermissions } from 'composables/usePermissions'
import { useDetailForm } from 'composables/useDetailForm'
import { required, maxLength } from 'validators'

const router = useRouter()
const { has } = usePermissions()
const canWrite = computed(() => has('Cms.Write'))

const tab = ref('general')

// Page groups for the group select ([— None —] + each group).
const groups = ref([])
const groupOptions = computed(() => [
  { label: '— None —', value: null },
  ...groups.value.map((g) => ({ label: g.name, value: g.id }))
])
async function loadGroups () {
  try {
    const result = await cmsPageGroupApi.list()
    groups.value = Array.isArray(result) ? result : result?.items || result?.data || []
  } catch {
    groups.value = []
  }
}
onMounted(loadGroups)

function buildPayload (form) {
  return {
    title: form.title,
    slug: form.slug || null,
    body: form.body || null,
    pageGroupId: form.pageGroupId || null,
    status: form.status,
    displayOrder: Number(form.displayOrder) || 0,
    showInTopBar: !!form.showInTopBar,
    showInFooter: !!form.showInFooter,
    metaTitle: form.metaTitle || null,
    metaDescription: form.metaDescription || null,
    metaKeywords: form.metaKeywords || null,
    canonicalUrl: form.canonicalUrl || null
  }
}

const {
  form, v$, entity, loading, creating, isCreate, saveStatus, create
} = useDetailForm({
  createRouteName: 'cms-page-new',
  detailRouteName: 'cms-page-detail',
  entityLabel: 'page',
  api: cmsPageApi,
  buildPayload,
  empty: {
    title: '', slug: '', body: '', pageGroupId: null, status: 'Draft', displayOrder: 0,
    showInTopBar: false, showInFooter: true,
    metaTitle: '', metaDescription: '', metaKeywords: '', canonicalUrl: ''
  },
  rules: {
    title: { required, maxLength: maxLength(300) },
    slug: { maxLength: maxLength(320) },
    canonicalUrl: { maxLength: maxLength(500) }
  }
})

// ---- Local slug derivation from the title (mirrors useDetailForm's deriveSlug, which is name-bound) ----
function slugify (value) {
  return (value || '').toString().trim().toLowerCase()
    .replace(/['’"]/g, '')
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
}
let lastAutoSlug = ''
watch(() => form.title, (title) => {
  if (!form.slug || form.slug === lastAutoSlug) {
    lastAutoSlug = slugify(title)
    form.slug = lastAutoSlug
  }
})

// ---- SEO preview + length hints ----
const seoPreview = computed(() => {
  const raw = form.metaDescription || (form.body ? String(form.body).replace(/<[^>]+>/g, '').slice(0, 160) : 'A description of this page will appear here in search results.')
  return {
    title: form.metaTitle || form.title || 'Page title',
    url: `yourstore.com › ${form.slug || 'page-slug'}`,
    description: raw.length > 160 ? `${raw.slice(0, 157)}…` : raw
  }
})
function lengthHint (value, ideal, fallbackLabel) {
  const n = (value || '').length
  if (!n) return `Falls back to the ${fallbackLabel}. Aim for ~${ideal} characters.`
  const note = n > ideal ? ' — may be truncated in results' : (n < ideal * 0.5 ? ' — consider adding detail' : ' — good length')
  return `${n} / ${ideal} recommended${note}`
}
const metaTitleHint = computed(() => lengthHint(form.metaTitle, 60, 'page title'))
const metaDescriptionHint = computed(() => lengthHint(form.metaDescription, 160, 'body'))
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
</style>
