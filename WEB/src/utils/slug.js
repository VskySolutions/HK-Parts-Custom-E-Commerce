/**
 * URL slug helper shared by every detail page that auto-derives a slug from its name/title.
 * Mirrors the API's SlugGenerator (lowercase a–z/0–9, single hyphens) so what the admin sees is what
 * the server stores.
 */
export function slugify (value) {
  return (value || '').toString().trim().toLowerCase()
    .replace(/['’"]/g, '')
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
}
