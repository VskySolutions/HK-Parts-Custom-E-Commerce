<template>
  <q-page class="storefront-root">
    <div class="sf-container q-py-lg">
      <nav class="sf-crumbs" aria-label="Breadcrumb">
        <router-link class="sf-crumbs__link" :to="{ name: 'shop-home' }">
          <q-icon name="o_home" size="15px" /> Home
        </router-link>
        <q-icon name="o_chevron_right" size="16px" class="sf-crumbs__sep" />
        <h1 class="sf-crumbs__current">Blog</h1>
      </nav>

      <!-- Loading -->
      <div v-if="loading" class="row justify-center q-py-xl">
        <q-spinner color="primary" size="42px" />
      </div>

      <!-- Empty -->
      <div v-else-if="!items.length" class="text-grey-6 q-py-xl text-center">
        No blog posts have been published yet.
      </div>

      <!-- List -->
      <template v-else>
        <div class="row q-col-gutter-lg">
          <div v-for="post in items" :key="post.slug" class="col-12 col-sm-6 col-md-4">
            <q-card flat bordered class="sf-blog-card">
              <router-link :to="postTo(post)" class="sf-blog-card__media">
                <img v-if="post.featuredImageUrl" :src="$media(post.featuredImageUrl)" :alt="post.title">
                <div v-else class="sf-blog-card__ph"><q-icon name="o_article" size="42px" /></div>
              </router-link>
              <q-card-section>
                <div class="sf-blog-card__meta">
                  <span v-if="post.publishedOnUtc"><q-icon name="o_event" size="14px" /> {{ $date(post.publishedOnUtc) }}</span>
                  <span v-if="post.author"><q-icon name="o_person" size="14px" /> {{ post.author }}</span>
                </div>
                <router-link :to="postTo(post)" class="sf-blog-card__title">{{ post.title }}</router-link>
                <p v-if="post.summary" class="sf-blog-card__summary">{{ post.summary }}</p>
                <router-link :to="postTo(post)" class="sf-blog-card__more">
                  Read more <q-icon name="o_arrow_forward" size="14px" />
                </router-link>
              </q-card-section>
            </q-card>
          </div>
        </div>

        <div v-if="totalPages > 1" class="row justify-center q-mt-xl">
          <q-pagination
            v-model="page"
            :max="totalPages"
            :max-pages="7"
            boundary-numbers
            direction-links
            @update:model-value="onPageChange"
          />
        </div>
      </template>
    </div>
  </q-page>
</template>

<script setup>
/*
 * Storefront blog listing (WO-54). A paged grid of published posts — featured
 * image, published date, author and summary — each linking to the full post by
 * name. Degrades to an empty state when nothing is published or the fetch fails.
 */
import { ref, computed, onMounted } from 'vue'
import { cmsApi } from 'modules/storefront/cms-api'

const PAGE_SIZE = 9

const items = ref([])
const totalCount = ref(0)
const page = ref(1)
const loading = ref(true)

const totalPages = computed(() => Math.max(1, Math.ceil(totalCount.value / PAGE_SIZE)))

function postTo (post) {
  return { name: 'shop-blog-post', params: { slug: post.slug } }
}

async function load () {
  loading.value = true
  try {
    const res = await cmsApi.blog({ page: page.value, pageSize: PAGE_SIZE })
    items.value = Array.isArray(res && res.items) ? res.items : []
    totalCount.value = (res && res.totalCount) || 0
  } catch (e) {
    items.value = []
    totalCount.value = 0
  } finally {
    loading.value = false
  }
}

function onPageChange () {
  load()
  if (typeof window !== 'undefined') window.scrollTo({ top: 0, behavior: 'smooth' })
}

onMounted(load)
</script>

<style scoped lang="scss">
.sf-blog-card {
  height: 100%;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  transition: box-shadow var(--sf-transition);
}
.sf-blog-card:hover { box-shadow: var(--sf-shadow-hover); }

.sf-blog-card__media {
  display: block;
  aspect-ratio: 16 / 10;
  background: var(--sf-surface-alt);
  overflow: hidden;

  img { width: 100%; height: 100%; object-fit: cover; display: block; transition: transform var(--sf-transition); }
}
.sf-blog-card:hover .sf-blog-card__media img { transform: scale(1.05); }
.sf-blog-card__ph {
  width: 100%;
  height: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
  color: var(--sf-muted);
}

.sf-blog-card__meta {
  display: flex;
  flex-wrap: wrap;
  gap: 14px;
  font-size: 12px;
  color: var(--sf-muted);
  margin-bottom: 8px;

  span { display: inline-flex; align-items: center; gap: 4px; }
}
.sf-blog-card__title {
  display: block;
  font-size: 16px;
  font-weight: 600;
  line-height: 1.35;
  color: var(--sf-heading);
  text-decoration: none;
}
.sf-blog-card__title:hover { color: var(--sf-accent); }
.sf-blog-card__summary {
  margin: 8px 0 12px;
  font-size: 13.5px;
  line-height: 1.6;
  color: var(--sf-text);
  display: -webkit-box;
  -webkit-line-clamp: 3;
  -webkit-box-orient: vertical;
  overflow: hidden;
}
.sf-blog-card__more {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  font-size: 13px;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.3px;
  color: var(--sf-accent);
  text-decoration: none;
}
</style>
