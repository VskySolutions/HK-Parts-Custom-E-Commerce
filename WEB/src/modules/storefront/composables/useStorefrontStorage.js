/*
 * Storefront client-side state (WO-19).
 *
 * Recently-viewed and compare lists are kept in localStorage and mirrored into
 * module-level refs so the public layout's compare badge and any open page stay
 * in sync. No authentication or store dependency — this works for anonymous
 * shoppers.
 *
 *   storefront.recentlyViewed : ordered product ids (most-recent first, cap 10)
 *   storefront.compare         : product ids selected for comparison (cap 4)
 */
import { ref } from 'vue'

const RECENT_KEY = 'storefront.recentlyViewed'
const COMPARE_KEY = 'storefront.compare'
const RECENT_CAP = 10
const COMPARE_MAX = 4

function readIds (key) {
  try {
    const raw = localStorage.getItem(key)
    const arr = raw ? JSON.parse(raw) : []
    return Array.isArray(arr) ? arr.filter((x) => typeof x === 'string' && x) : []
  } catch (e) {
    return []
  }
}

function writeIds (key, ids) {
  try {
    localStorage.setItem(key, JSON.stringify(ids))
  } catch (e) {
    /* ignore quota / serialization errors — the list is best-effort */
  }
}

// Shared reactive state (module singletons).
const recentlyViewedIds = ref(readIds(RECENT_KEY))
const compareIds = ref(readIds(COMPARE_KEY))

export function useRecentlyViewed () {
  function record (id) {
    if (!id) return
    const next = [id, ...recentlyViewedIds.value.filter((x) => x !== id)].slice(0, RECENT_CAP)
    recentlyViewedIds.value = next
    writeIds(RECENT_KEY, next)
  }
  function clear () {
    recentlyViewedIds.value = []
    writeIds(RECENT_KEY, [])
  }
  return { recentlyViewedIds, record, clear }
}

export function useCompare () {
  function has (id) {
    return compareIds.value.includes(id)
  }
  function add (id) {
    if (!id || has(id)) return { ok: has(id), full: false }
    if (compareIds.value.length >= COMPARE_MAX) return { ok: false, full: true }
    const next = [...compareIds.value, id]
    compareIds.value = next
    writeIds(COMPARE_KEY, next)
    return { ok: true, full: false }
  }
  function remove (id) {
    const next = compareIds.value.filter((x) => x !== id)
    compareIds.value = next
    writeIds(COMPARE_KEY, next)
  }
  // Returns { ok, full, removed } describing the resulting state.
  function toggle (id) {
    if (has(id)) {
      remove(id)
      return { ok: true, full: false, removed: true }
    }
    return { ...add(id), removed: false }
  }
  function clear () {
    compareIds.value = []
    writeIds(COMPARE_KEY, [])
  }
  return { compareIds, has, add, remove, toggle, clear, max: COMPARE_MAX }
}
