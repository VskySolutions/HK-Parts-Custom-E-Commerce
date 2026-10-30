<template>
  <div v-if="posts.length" class="sf-section">
    <div class="sf-container">
      <div class="sf-section__head">
        <h2 class="sf-section__title">{{ heading }}</h2>
      </div>

      <div class="sf-blograil">
        <component
          :is="postHref(post) ? 'a' : 'div'"
          v-for="(post, i) in posts"
          :key="post.id || post.slug || i"
          :href="postHref(post)"
          class="sf-blogcard"
        >
          <div class="sf-blogcard__media">
            <img
              v-if="post.featuredImageUrl"
              :src="$media(post.featuredImageUrl)"
              :alt="post.title"
              class="sf-blogcard__img"
              loading="lazy"
            >
            <div v-else class="sf-blogcard__img sf-blogcard__img--empty row items-center justify-center">
              <q-icon name="o_article" size="36px" />
            </div>
          </div>
          <div class="sf-blogcard__body">
            <div v-if="post.publishedOnUtc" class="sf-blogcard__date">{{ formatDate(post.publishedOnUtc) }}</div>
            <div class="sf-blogcard__title">{{ post.title }}</div>
            <div v-if="post.summary" class="sf-blogcard__summary">{{ post.summary }}</div>
            <span v-if="postHref(post)" class="sf-blogcard__more">Read more <q-icon name="o_arrow_forward" size="14px" /></span>
          </div>
        </component>
      </div>
    </div>
  </div>
</template>

<script setup>
/*
 * Blog posts row (WO-100) — a horizontal scroll-snap rail of blog cards for the
 * BlogPostsRow home-page section. Mirrors the ProductCarousel rail sizing.
 *
 * A public blog route does not exist in the storefront router yet, so cards link
 * defensively to the intended `/shop/blog/{slug}` path via a plain <a> (a full
 * navigation the SPA can pick up once a blog route lands); a post with neither a
 * slug nor id renders as a non-linked card rather than a dead link.
 */
import { formatDate } from 'src/utils/datetime'

defineProps({
  heading: { type: String, default: 'From the Blog' },
  posts: { type: Array, default: () => [] }
})

function postHref (post) {
  const key = post && (post.slug || post.id)
  return key ? `/shop/blog/${key}` : undefined
}
</script>

<style scoped lang="scss">
.sf-blograil {
  display: flex;
  gap: 20px;
  overflow-x: auto;
  scroll-snap-type: x mandatory;
  scroll-behavior: smooth;
  padding-bottom: 8px;
  -ms-overflow-style: none;
  scrollbar-width: none;
}
.sf-blograil::-webkit-scrollbar { display: none; }

.sf-blogcard {
  flex: 0 0 calc((100% - 60px) / 4);
  scroll-snap-align: start;
  display: flex;
  flex-direction: column;
  background: var(--sf-surface);
  border: 1px solid var(--sf-border);
  border-radius: var(--sf-radius);
  overflow: hidden;
  text-decoration: none;
  color: var(--sf-text);
  transition: box-shadow var(--sf-transition), border-color var(--sf-transition), transform var(--sf-transition);
}
a.sf-blogcard:hover { box-shadow: var(--sf-shadow-hover); border-color: transparent; transform: translateY(-3px); }

.sf-blogcard__media { position: relative; aspect-ratio: 16 / 10; background: #fafafa; overflow: hidden; }
.sf-blogcard__img { position: absolute; inset: 0; width: 100%; height: 100%; object-fit: cover; }
.sf-blogcard__img--empty { color: var(--sf-muted); background: var(--sf-surface-alt); }

.sf-blogcard__body { padding: 16px; display: flex; flex-direction: column; gap: 6px; flex: 1; }
.sf-blogcard__date { font-size: 12px; color: var(--sf-muted); text-transform: uppercase; letter-spacing: 0.4px; }
.sf-blogcard__title {
  font-size: 15px;
  font-weight: 700;
  line-height: 1.35;
  color: var(--sf-heading);
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}
a.sf-blogcard:hover .sf-blogcard__title { color: var(--sf-accent); }
.sf-blogcard__summary {
  font-size: 13px;
  color: var(--sf-muted);
  line-height: 1.5;
  display: -webkit-box;
  -webkit-line-clamp: 3;
  -webkit-box-orient: vertical;
  overflow: hidden;
}
.sf-blogcard__more {
  margin-top: auto;
  padding-top: 6px;
  font-size: 12px;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  color: var(--sf-accent);
  display: inline-flex;
  align-items: center;
  gap: 4px;
}

@media (max-width: 1023px) {
  .sf-blogcard { flex-basis: calc((100% - 20px) / 2); }
}
@media (max-width: 599px) {
  .sf-blogcard { flex-basis: 82%; }
}
</style>
