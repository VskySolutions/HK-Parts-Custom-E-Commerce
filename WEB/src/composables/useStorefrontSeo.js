/*
 * useStorefrontSeo — one place that turns a record's central SEO payload (`seo` on every storefront detail
 * response) into <head> tags: title, description, keywords, robots, canonical, Open Graph / Twitter cards
 * and optional JSON-LD. Every storefront detail page (product, category, CMS page, blog post) uses it so
 * the fallbacks behave the same everywhere.
 *
 *   useStorefrontSeo(() => record.value && {
 *     seo: record.value.seo,             // StorefrontSeoDto (may be null)
 *     title: record.value.name,          // fallback title
 *     description: record.value.summary, // fallback description (HTML is stripped)
 *     image: firstImageUrl,              // fallback og:image
 *     type: 'product',                   // og:type (default 'website')
 *     jsonLd: schemaObjectOrString       // optional structured data
 *   })
 *
 * Returns `{ heading }` — the H1 to render (seo.h1 ?? fallback title).
 * Raw admin markup (Open Graph override, custom head/footer tags) is intentionally not rendered here.
 */
import { computed } from 'vue'
import { useMeta } from 'quasar'
import { mediaUrl } from 'services/api'

function plain (value, max = 300) {
  const text = (value || '').toString().replace(/<[^>]+>/g, ' ').replace(/\s+/g, ' ').trim()
  return text.length > max ? `${text.slice(0, max - 1)}…` : text
}

function absolute (url) {
  if (!url) return null
  const resolved = mediaUrl(url)
  if (/^https?:\/\//i.test(resolved)) return resolved
  return typeof window !== 'undefined' ? new URL(resolved, window.location.origin).href : resolved
}

export function useStorefrontSeo (source) {
  const state = computed(() => {
    const src = source() || null
    if (!src) return null
    const seo = src.seo || {}
    const title = seo.metaTitle || src.title || ''
    const description = seo.metaDescription || plain(src.description, 160)
    const canonical = seo.canonicalUrl ||
      (typeof window !== 'undefined' ? window.location.origin + window.location.pathname : null)
    return {
      title,
      description,
      keywords: seo.metaKeywords || '',
      noIndex: !!seo.noIndex,
      canonical,
      image: absolute(seo.metaImageUrl || src.image),
      type: src.type || 'website',
      heading: seo.h1 || src.title || '',
      jsonLd: src.jsonLd || null
    }
  })

  useMeta(() => {
    const s = state.value
    if (!s) return {}
    const meta = {
      ogTitle: { property: 'og:title', content: s.title },
      ogType: { property: 'og:type', content: s.type },
      twitterCard: { name: 'twitter:card', content: s.image ? 'summary_large_image' : 'summary' }
    }
    if (s.description) {
      meta.description = { name: 'description', content: s.description }
      meta.ogDescription = { property: 'og:description', content: s.description }
    }
    if (s.keywords) meta.keywords = { name: 'keywords', content: s.keywords }
    if (s.noIndex) meta.robots = { name: 'robots', content: 'noindex, nofollow' }
    if (s.canonical) meta.ogUrl = { property: 'og:url', content: s.canonical }
    if (s.image) meta.ogImage = { property: 'og:image', content: s.image }

    const result = { title: s.title, meta }
    if (s.canonical) result.link = { canonical: { rel: 'canonical', href: s.canonical } }
    if (s.jsonLd) {
      result.script = {
        ldJson: {
          type: 'application/ld+json',
          // Escape "<" so record text such as "</script>" cannot break out of the script element
          // ("<" only ever appears inside JSON strings, where < is equivalent).
          innerHTML: (typeof s.jsonLd === 'string' ? s.jsonLd : JSON.stringify(s.jsonLd)).replace(/</g, '\\u003c')
        }
      }
    }
    return result
  })

  return { heading: computed(() => (state.value ? state.value.heading : '')) }
}
