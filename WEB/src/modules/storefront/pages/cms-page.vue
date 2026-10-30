<template>
  <q-page class="storefront-root">
    <div class="sf-container q-py-lg">
      <!-- Loading -->
      <div v-if="loading" class="row justify-center q-py-xl">
        <q-spinner color="primary" size="42px" />
      </div>

      <!-- 404 / unpublished -->
      <q-banner v-else-if="notFound" class="bg-grey-2 rounded-borders q-my-md">
        This page was not found.
        <template #action>
          <q-btn flat no-caps color="primary" label="Back to shop" :to="{ name: 'shop-home' }" />
        </template>
      </q-banner>

      <!-- Page -->
      <article v-else-if="page">
        <nav class="sf-crumbs" aria-label="Breadcrumb">
          <router-link class="sf-crumbs__link" :to="{ name: 'shop-home' }">
            <q-icon name="o_home" size="15px" /> Home
          </router-link>
          <q-icon name="o_chevron_right" size="16px" class="sf-crumbs__sep" />
          <h1 class="sf-crumbs__current">{{ page.title }}</h1>
        </nav>

        <!-- eslint-disable-next-line vue/no-v-html -->
        <div class="sf-cms-body" v-html="safeBody" />
      </article>
    </div>
  </q-page>
</template>

<script setup>
/*
 * Storefront CMS page viewer (WO-54). Fetches a single published page by slug and
 * renders its title + rich `body` HTML. The body is passed through the minimal
 * client-side sanitiser before v-html; a missing/unpublished page (404) shows a
 * friendly not-found state. metaTitle/metaDescription drive the document head.
 */
import { ref, computed, watch, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { useMeta } from 'quasar'
import { cmsApi, sanitizeCmsHtml } from 'modules/storefront/cms-api'

const route = useRoute()

const page = ref(null)
const loading = ref(true)
const notFound = ref(false)

const safeBody = computed(() => sanitizeCmsHtml(page.value && page.value.body))

useMeta(() => ({
  title: (page.value && (page.value.metaTitle || page.value.title)) || 'Page',
  meta: page.value && page.value.metaDescription
    ? { description: { name: 'description', content: page.value.metaDescription } }
    : {}
}))

async function load () {
  loading.value = true
  notFound.value = false
  page.value = null
  try {
    const data = await cmsApi.page(route.params.slug)
    if (data) page.value = data
    else notFound.value = true
  } catch (e) {
    notFound.value = true
  } finally {
    loading.value = false
  }
}

watch(() => route.params.slug, load)
onMounted(load)
</script>

<style scoped lang="scss">
.sf-cms-body {
  font-size: 15px;
  line-height: 1.75;
  color: var(--sf-text);

  // v-html content is not scoped — reach it with :deep().
  :deep(img) { max-width: 100%; height: auto; border-radius: var(--sf-radius); }
  :deep(h2), :deep(h3), :deep(h4) { margin-top: 1.4em; }
  :deep(p) { margin: 0 0 1em; }
  :deep(ul), :deep(ol) { margin: 0 0 1em; padding-left: 1.4em; }
  :deep(table) { width: 100%; border-collapse: collapse; margin: 0 0 1em; }
  :deep(th), :deep(td) { border: 1px solid var(--sf-border); padding: 8px 10px; text-align: left; }
  :deep(a) { color: var(--sf-accent); }
  :deep(blockquote) {
    margin: 0 0 1em;
    padding: 8px 16px;
    border-left: 3px solid var(--sf-accent);
    color: var(--sf-muted);
    background: var(--sf-surface-alt);
  }
}
</style>
