<template>
  <q-page class="storefront-root">
    <div class="sf-container q-py-lg">
      <!-- Loading -->
      <div v-if="loading" class="row justify-center q-py-xl">
        <q-spinner color="primary" size="42px" />
      </div>

      <!-- 404 / unpublished -->
      <q-banner v-else-if="notFound" class="bg-grey-2 rounded-borders q-my-md">
        This article was not found.
        <template #action>
          <q-btn flat no-caps color="primary" label="Back to blog" :to="{ name: 'shop-blog' }" />
        </template>
      </q-banner>

      <!-- Post -->
      <article v-else-if="post" class="sf-article">
        <nav class="sf-crumbs" aria-label="Breadcrumb">
          <router-link class="sf-crumbs__link" :to="{ name: 'shop-home' }">
            <q-icon name="o_home" size="15px" /> Home
          </router-link>
          <q-icon name="o_chevron_right" size="16px" class="sf-crumbs__sep" />
          <router-link class="sf-crumbs__link" :to="{ name: 'shop-blog' }">Blog</router-link>
          <q-icon name="o_chevron_right" size="16px" class="sf-crumbs__sep" />
          <span class="sf-crumbs__here">{{ post.title }}</span>
        </nav>

        <h1 class="sf-article__title">{{ post.title }}</h1>
        <div class="sf-article__meta">
          <span v-if="post.publishedOnUtc"><q-icon name="o_event" size="15px" /> {{ $date(post.publishedOnUtc) }}</span>
          <span v-if="post.author"><q-icon name="o_person" size="15px" /> {{ post.author }}</span>
        </div>

        <img
          v-if="post.featuredImageUrl"
          :src="$media(post.featuredImageUrl)"
          :alt="post.title"
          class="sf-article__hero"
        >

        <!-- eslint-disable-next-line vue/no-v-html -->
        <div class="sf-cms-body" v-html="safeBody" />

        <div v-if="tags.length" class="sf-article__tags">
          <q-icon name="o_sell" size="16px" />
          <router-link
            v-for="(tag, i) in tags"
            :key="tagKey(tag, i)"
            class="sf-article__tag"
            :to="{ name: 'shop-blog' }"
          >{{ tagLabel(tag) }}</router-link>
        </div>
      </article>
    </div>
  </q-page>
</template>

<script setup>
/*
 * Storefront blog post viewer (WO-54). Fetches a single published post by slug
 * and renders the featured image, title, published/author meta line, sanitised
 * rich `body` HTML, and its tags. A missing/unpublished post (404) shows a
 * friendly not-found state. Tags tolerate either strings or { name, slug }.
 */
import { ref, computed, watch, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { useMeta } from 'quasar'
import { cmsApi, sanitizeCmsHtml } from 'modules/storefront/cms-api'

const route = useRoute()

const post = ref(null)
const loading = ref(true)
const notFound = ref(false)

const safeBody = computed(() => sanitizeCmsHtml(post.value && post.value.body))
const tags = computed(() => (Array.isArray(post.value && post.value.tags) ? post.value.tags : []))

useMeta(() => ({ title: (post.value && post.value.title) || 'Article' }))

function tagLabel (tag) {
  return typeof tag === 'string' ? tag : (tag && (tag.name || tag.title)) || ''
}
function tagKey (tag, i) {
  return (typeof tag === 'string' ? tag : (tag && (tag.slug || tag.id || tag.name))) || i
}

async function load () {
  loading.value = true
  notFound.value = false
  post.value = null
  try {
    const data = await cmsApi.blogPost(route.params.slug)
    if (data) post.value = data
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
.sf-article { max-width: 860px; margin: 0 auto; }
.sf-article__title {
  font-size: 30px;
  font-weight: 700;
  line-height: 1.2;
  margin: 6px 0 12px;
  color: var(--sf-heading);
}
.sf-article__meta {
  display: flex;
  flex-wrap: wrap;
  gap: 16px;
  font-size: 13px;
  color: var(--sf-muted);
  margin-bottom: 20px;

  span { display: inline-flex; align-items: center; gap: 5px; }
}
.sf-article__hero {
  width: 100%;
  max-height: 460px;
  object-fit: cover;
  border-radius: var(--sf-radius-lg);
  margin-bottom: 24px;
  display: block;
}
.sf-article__tags {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 8px;
  margin-top: 28px;
  padding-top: 18px;
  border-top: 1px solid var(--sf-border);
  color: var(--sf-muted);
}
.sf-article__tag {
  display: inline-flex;
  padding: 4px 12px;
  font-size: 12px;
  background: var(--sf-surface-alt);
  color: var(--sf-text);
  border-radius: 999px;
  text-decoration: none;
  transition: background var(--sf-transition), color var(--sf-transition);
}
.sf-article__tag:hover { background: var(--sf-accent); color: #fff; }

.sf-cms-body {
  font-size: 15px;
  line-height: 1.8;
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
