<template>
  <q-page class="app-page">
    <AppDetailHeader
      :title="isCreate ? 'New blog post' : (entity?.title || 'Blog post')"
      :breadcrumbs="[
        { label: 'Home', icon: 'o_home', to: '/dashboard' },
        { label: 'CMS' },
        { label: 'Blog', to: { name: 'cms-blog' } },
        { label: isCreate ? 'New blog post' : (entity?.title || 'Blog post') }
      ]"
      :status="!isCreate && entity ? form.status : ''"
      :status-color="statusColor(form.status)"
      show-back
      @back="router.push({ name: 'cms-blog' })"
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
      Blog post not found.
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
          <q-tab name="general" icon="o_article" label="Content" />
          <q-tab name="seo" icon="o_search" label="SEO" :disable="isCreate" />
        </q-tabs>

        <q-separator />

        <q-tab-panels v-model="tab" animated keep-alive>
          <!-- ============ CONTENT ============ -->
          <q-tab-panel name="general" class="q-gutter-y-sm">
            <AppTextField v-model="form.title" label="Title" required :v="v$.title" placeholder="e.g. Our summer sale is live" :disable="!canWrite" />
            <AppTextField v-model="form.slug" label="Slug" :v="v$.slug" placeholder="e.g. summer-sale" hint="Storefront URL — auto-filled from the title until you edit it" :disable="!canWrite" />

            <AppTextField v-model="form.summary" label="Summary" type="textarea" autogrow :v="v$.summary" placeholder="Short teaser shown in blog listings" :disable="!canWrite" />

            <AppFileUpload media v-model="form.featuredImageMediaId" v-model:preview-url="form.featuredImageUrl" label="Featured image" accept="image/*" extensions-label="PNG, JPG, WEBP" :disable="!canWrite" />

            <AppRichText v-model="form.body" label="Body" placeholder="Write the post…" min-height="18rem" :disable="!canWrite" />

            <q-separator class="q-my-sm" />
            <div class="row q-col-gutter-sm items-start">
              <div class="col-12 col-md-4">
                <AppTextField v-model="form.author" label="Author" :v="v$.author" placeholder="e.g. Jane Doe" :disable="!canWrite" />
              </div>
              <div class="col-12 col-md-5">
                <AppTextField v-model="form.tags" label="Tags" placeholder="news, releases, tips" hint="Comma-separated" :disable="!canWrite" />
              </div>
              <div class="col-12 col-md-3">
                <AppSelect v-model="form.status" label="Status" :options="statusOptions" :disable="!canWrite" />
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

            <AppTextField v-model="form.metaTitle" label="Meta title" placeholder="Defaults to the post title" :disable="!canWrite" maxlength="300">
              <template #hint>{{ metaTitleHint }}</template>
            </AppTextField>
            <AppTextField v-model="form.metaDescription" label="Meta description" type="textarea" autogrow placeholder="Plain-text summary for search engines" :disable="!canWrite" maxlength="500">
              <template #hint>{{ metaDescriptionHint }}</template>
            </AppTextField>
          </q-tab-panel>
        </q-tab-panels>

        <template v-if="isCreate">
          <q-separator />
          <q-card-actions class="q-pa-md">
            <div class="text-caption text-grey-7">
              Create the post to unlock SEO — all auto-saved from then on.
            </div>
            <q-space />
            <q-btn v-if="canWrite" unelevated color="primary" no-caps icon="o_check" label="Create post" :loading="creating" @click="create" />
          </q-card-actions>
        </template>
      </q-card>
    </template>

    <AppRecordMeta entity-type="cms-blog-post" :record-id="entity?.id" />
  </q-page>
</template>

<script setup>
/*
 * Blog Post create + manage page (WO-54; full-page auto-save via useDetailForm). Content (title / slug /
 * summary / featured image / body / author / tags / status) and SEO (meta title/description) auto-save on
 * change in manage mode, with the live status chip in the header. Slug is derived from the title locally
 * (useDetailForm's built-in deriveSlug watches `form.name`, but the primary field here is `title`). The
 * featured image is a central Media asset (AppFileUpload media mode): the model holds the Media id and the
 * display URL rides on `featuredImageUrl`; only the id is persisted. Tags are a comma-separated string.
 */
import { ref, computed, watch } from 'vue'
import { useRouter } from 'vue-router'
import { blogPostApi, statusOptions, statusColor } from '../api'
import { usePermissions } from 'composables/usePermissions'
import { useDetailForm } from 'composables/useDetailForm'
import { required, maxLength } from 'validators'

const router = useRouter()
const { has } = usePermissions()
const canWrite = computed(() => has('Cms.Write'))

const tab = ref('general')

function buildPayload (form) {
  return {
    title: form.title,
    slug: form.slug || null,
    summary: form.summary || null,
    body: form.body || null,
    author: form.author || null,
    tags: form.tags || null,
    featuredImageMediaId: form.featuredImageMediaId || null,
    status: form.status,
    metaTitle: form.metaTitle || null,
    metaDescription: form.metaDescription || null
  }
}

const {
  form, v$, entity, loading, creating, isCreate, saveStatus, create
} = useDetailForm({
  createRouteName: 'cms-blog-new',
  detailRouteName: 'cms-blog-detail',
  entityLabel: 'blog post',
  api: blogPostApi,
  buildPayload,
  empty: {
    title: '', slug: '', summary: '', body: '', author: '', tags: '',
    featuredImageMediaId: null, featuredImageUrl: '', status: 'Draft',
    metaTitle: '', metaDescription: ''
  },
  rules: {
    title: { required, maxLength: maxLength(300) },
    slug: { maxLength: maxLength(320) },
    summary: { maxLength: maxLength(1000) },
    author: { maxLength: maxLength(200) }
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
  const raw = form.metaDescription || form.summary || (form.body ? String(form.body).replace(/<[^>]+>/g, '').slice(0, 160) : 'A description of this post will appear here in search results.')
  return {
    title: form.metaTitle || form.title || 'Post title',
    url: `yourstore.com › blog › ${form.slug || 'post-slug'}`,
    description: raw.length > 160 ? `${raw.slice(0, 157)}…` : raw
  }
})
function lengthHint (value, ideal, fallbackLabel) {
  const n = (value || '').length
  if (!n) return `Falls back to the ${fallbackLabel}. Aim for ~${ideal} characters.`
  const note = n > ideal ? ' — may be truncated in results' : (n < ideal * 0.5 ? ' — consider adding detail' : ' — good length')
  return `${n} / ${ideal} recommended${note}`
}
const metaTitleHint = computed(() => lengthHint(form.metaTitle, 60, 'post title'))
const metaDescriptionHint = computed(() => lengthHint(form.metaDescription, 160, 'summary'))
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
