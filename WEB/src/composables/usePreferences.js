/*
 * usePreferences(pageKey)
 *
 * Reads/writes per-page UI preferences to a cookie (`pref_<pageKey>`), JSON
 * encoded, 365-day expiry. Returns reactive refs plus a debounced `save()`.
 *
 * Stored shape:
 *   { pageSize, sortBy, descending, columnWidths: {}, columnOrder: [], hiddenColumns: [], filters: {} }
 *
 * Changes to any ref auto-persist (debounced 300ms); `save()` can also be
 * called manually.
 */
import { ref, watch } from 'vue'
import { useQuasar } from 'quasar'

const DEFAULTS = {
  pageSize: 10,
  sortBy: null,
  descending: false,
  columnWidths: {},
  columnOrder: null,   // array of column names in display order (null = table's declared order)
  hiddenColumns: [],   // column names the user has hidden
  filters: {}
}

export function usePreferences (pageKey) {
  const $q = useQuasar()
  const cookieName = `pref_${pageKey}`

  const stored = $q.cookies.has(cookieName) ? $q.cookies.get(cookieName) || {} : {}
  const initial = { ...DEFAULTS, ...stored }

  const pageSize = ref(initial.pageSize)
  const sortBy = ref(initial.sortBy)
  const descending = ref(initial.descending)
  const columnWidths = ref(initial.columnWidths || {})
  const columnOrder = ref(initial.columnOrder || null)
  const hiddenColumns = ref(initial.hiddenColumns || [])
  const filters = ref(initial.filters || {})

  let timer = null

  function writeCookie () {
    $q.cookies.set(
      cookieName,
      {
        pageSize: pageSize.value,
        sortBy: sortBy.value,
        descending: descending.value,
        columnWidths: columnWidths.value,
        columnOrder: columnOrder.value,
        hiddenColumns: hiddenColumns.value,
        filters: filters.value
      },
      { expires: 365, path: '/', sameSite: 'Lax' }
    )
  }

  // Debounced save (300ms).
  function save () {
    if (timer) clearTimeout(timer)
    timer = setTimeout(writeCookie, 300)
  }

  function reset () {
    pageSize.value = DEFAULTS.pageSize
    sortBy.value = DEFAULTS.sortBy
    descending.value = DEFAULTS.descending
    columnWidths.value = {}
    columnOrder.value = null
    hiddenColumns.value = []
    filters.value = {}
    $q.cookies.remove(cookieName, { path: '/' })
  }

  // Auto-persist on any change.
  watch([pageSize, sortBy, descending, columnWidths, columnOrder, hiddenColumns, filters], save, { deep: true })

  return {
    pageSize,
    sortBy,
    descending,
    columnWidths,
    columnOrder,
    hiddenColumns,
    filters,
    save,
    reset
  }
}
