/*
 * useCategories (WO-109) — the public storefront category tree.
 *
 * Loads the enabled category tree once (module-singleton) from
 * GET /api/storefront/catalog/categories and shares it across the header
 * mega-menu and the home-page category grid.
 */
import { ref, computed } from 'vue'
import { storefrontApi } from 'modules/storefront/api'

const categories = ref([])
const loaded = ref(false)
const loading = ref(false)

// Navigation view: drops categories flagged "Display in menu = off" (and their subtrees).
function pruneMenu (nodes) {
  return (nodes || [])
    .filter((n) => n.showInMenu !== false)
    .map((n) => ({ ...n, children: pruneMenu(n.children) }))
}
const menuCategories = computed(() => pruneMenu(categories.value))

export function useCategories () {
  async function loadCategories (force = false) {
    if (loaded.value && !force) return categories.value
    if (loading.value) return categories.value
    loading.value = true
    try {
      const tree = await storefrontApi.categories()
      categories.value = Array.isArray(tree) ? tree : []
      // Only cache as loaded on success, so a transient failure retries next time
      // instead of permanently pinning an empty list for the session.
      loaded.value = true
    } catch (e) {
      categories.value = []
    } finally {
      loading.value = false
    }
    return categories.value
  }

  return { categories, menuCategories, loaded, loading, loadCategories }
}
