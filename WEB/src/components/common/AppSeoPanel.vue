<template>
  <div class="app-seo-panel q-gutter-y-sm">
    <q-inner-loading :showing="loading" />

    <!-- Search-result preview -->
    <div class="text-caption text-grey-7 q-mb-xs">Search engine preview</div>
    <q-card flat bordered class="q-pa-md q-mb-md app-seo-panel__preview">
      <div class="app-seo-panel__title ellipsis">{{ preview.title }}</div>
      <div class="app-seo-panel__url ellipsis">{{ preview.url }}</div>
      <div class="app-seo-panel__desc">{{ preview.description }}</div>
    </q-card>

    <AppTextField v-model="form.metaTitle" label="Meta title" :placeholder="`Defaults to the ${fallbackLabel}`" :v="v$.metaTitle" :disable="disable" maxlength="300">
      <template #hint>{{ titleHint }}</template>
    </AppTextField>

    <AppTextField v-model="form.metaDescription" label="Meta description" type="textarea" autogrow placeholder="Plain-text summary for search engines" :v="v$.metaDescription" :disable="disable" maxlength="500">
      <template #hint>{{ descriptionHint }}</template>
    </AppTextField>

    <AppTextField v-model="form.metaKeywords" label="Meta keywords" placeholder="Comma-separated keywords" hint="Minor impact on modern search engines" :v="v$.metaKeywords" :disable="disable" maxlength="500" />

    <AppTextField v-model="form.h1" label="H1 tag" :placeholder="`Defaults to the ${fallbackLabel}`" :v="v$.h1" :disable="disable" maxlength="300" />

    <AppTextField v-model="form.canonicalUrl" label="Canonical URL" placeholder="https://…" hint="Optional — the preferred URL when this content is reachable at several addresses" :v="v$.canonicalUrl" :disable="disable" maxlength="500" />

    <MediaImageField
      v-model="form.metaImageMediaId"
      v-model:url="metaImageUrl"
      label="Meta image"
      hint="Social-share (og:image) picture — 1200×630 recommended"
      :alt="fallbackTitle"
      :disable="disable"
    />

    <q-toggle v-model="form.noIndex" label="Hide from search engines (noindex, left out of sitemap.xml)" :disable="disable" />

    <q-expansion-item dense icon="o_code" label="Advanced: raw markup" header-class="text-grey-8 q-px-none">
      <div class="q-gutter-y-sm q-pt-sm">
        <AppTextField v-model="form.metaOpenGraph" label="Open Graph override" type="textarea" autogrow placeholder='&lt;meta property="og:title" content="…" /&gt;' hint="Raw og: tags — replaces the generated Open Graph markup" :disable="disable" />
        <AppTextField v-model="form.metaCustomTags" label="Custom head tags / scripts" type="textarea" autogrow hint="Injected into the page &lt;head&gt;" :disable="disable" />
        <AppTextField v-model="form.metaFooterTags" label="Custom footer tags / scripts" type="textarea" autogrow hint="Injected before &lt;/body&gt;" :disable="disable" />
      </div>
    </q-expansion-item>
  </div>
</template>

<script setup>
/*
 * AppSeoPanel — the one SEO editor every module uses (products, categories, manufacturers, CMS pages,
 * blog posts, collections…). Loads and auto-saves the record's row in the central SEO table
 * (GET/PUT /api/admin/seo/entities/{entityType}/{entityId}), independent of the host page's own form.
 * Pass the host's `runSave` so saves drive the page's status chip.
 */
import { reactive, ref, computed, watch, nextTick } from 'vue'
import { debounce } from 'quasar'
import useVuelidate from '@vuelidate/core'
import { maxLength, url } from 'validators'
import { seoMetadataApi, getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'
import MediaImageField from 'modules/catalog/components/MediaImageField.vue'

// Storefront route prefix per entity type (mirrors modules/storefront/routes.js and the API's
// SeoEntities.StorefrontPathFor). Types without a storefront page show no URL.
const STOREFRONT_PATHS = {
  Product: '/shop/product/',
  Category: '/shop/category/',
  CmsPage: '/shop/page/',
  BlogPost: '/shop/blog/'
}

const props = defineProps({
  entityType: { type: String, required: true },
  entityId: { type: String, default: null },
  slug: { type: String, default: '' },
  fallbackTitle: { type: String, default: '' },
  fallbackDescription: { type: String, default: '' },
  fallbackLabel: { type: String, default: 'name' },
  disable: { type: Boolean, default: false },
  runSave: { type: Function, default: null }
})

const notify = useNotify()

const EMPTY = {
  metaTitle: '', metaDescription: '', metaKeywords: '', canonicalUrl: '', h1: '',
  metaImageMediaId: null, metaOpenGraph: '', metaCustomTags: '', metaFooterTags: '', noIndex: false
}
const form = reactive({ ...EMPTY })
const metaImageUrl = ref(null)
const loading = ref(false)

const rules = {
  metaTitle: { maxLength: maxLength(300) },
  metaDescription: { maxLength: maxLength(500) },
  metaKeywords: { maxLength: maxLength(500) },
  h1: { maxLength: maxLength(300) },
  canonicalUrl: { maxLength: maxLength(500), url }
}
const v$ = useVuelidate(rules, form)

let hydrating = false
let lastSaved = ''

function payload () {
  const p = {}
  for (const k of Object.keys(EMPTY)) {
    const v = form[k]
    p[k] = typeof v === 'string' ? (v.trim() || null) : v
  }
  return p
}

async function load () {
  if (!props.entityId) return
  loading.value = true
  hydrating = true
  try {
    const dto = await seoMetadataApi.get(props.entityType, props.entityId)
    for (const k of Object.keys(EMPTY)) form[k] = dto[k] ?? EMPTY[k]
    metaImageUrl.value = dto.metaImageUrl || null
    lastSaved = JSON.stringify(payload())
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    loading.value = false
    nextTick(() => { hydrating = false })
  }
}

const save = debounce(async () => {
  if (!props.entityId || props.disable) return
  if (!(await v$.value.$validate())) return
  const body = payload()
  const snap = JSON.stringify(body)
  if (snap === lastSaved) return
  const doSave = async () => {
    await seoMetadataApi.save(props.entityType, props.entityId, body)
    lastSaved = snap
  }
  if (props.runSave) {
    await props.runSave(doSave)
  } else {
    try { await doSave() } catch (err) { notify.error(getApiErrorMessage(err)) }
  }
}, 800)

watch(form, () => { if (!hydrating) save() }, { deep: true })
watch(() => [props.entityType, props.entityId], load, { immediate: true })

// ---- preview + length hints ----------------------------------------------------
function stripHtml (s) { return (s || '').toString().replace(/<[^>]+>/g, ' ').replace(/\s+/g, ' ').trim() }

const preview = computed(() => {
  const raw = form.metaDescription || stripHtml(props.fallbackDescription) || 'A description will appear here in search results.'
  const path = STOREFRONT_PATHS[props.entityType]
  const key = props.slug || props.entityId || ''
  const host = typeof window !== 'undefined' ? window.location.origin : ''
  return {
    title: form.metaTitle || props.fallbackTitle || 'Page title',
    url: path ? `${host}${path}${key}` : '(no storefront page for this item yet)',
    description: raw.length > 160 ? `${raw.slice(0, 157)}…` : raw
  }
})

function lengthHint (value, ideal) {
  const n = (value || '').length
  if (!n) return `Falls back to the ${props.fallbackLabel}. Aim for ~${ideal} characters.`
  const note = n > ideal ? ' — may be truncated in results' : (n < ideal * 0.5 ? ' — consider adding detail' : ' — good length')
  return `${n} / ${ideal} recommended${note}`
}
const titleHint = computed(() => lengthHint(form.metaTitle, 60))
const descriptionHint = computed(() => lengthHint(form.metaDescription, 160))
</script>

<style lang="scss" scoped>
.app-seo-panel {
  position: relative;
  &__preview { max-width: 600px; }
  &__title { color: #1a0dab; font-size: 18px; line-height: 1.3; }
  &__url { color: #006621; font-size: 13px; margin: 2px 0 4px; }
  &__desc { color: #4d5156; font-size: 13px; line-height: 1.4; }
}
</style>
