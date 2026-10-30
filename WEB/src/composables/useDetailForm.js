/*
 * useDetailForm — the shared engine behind the full-page "create + manage" detail pages
 * (product/category/manufacturer/…). Encapsulates the pattern:
 *   - two modes off one route pair: create (`createRouteName`) vs manage (`detailRouteName`)
 *   - create is a single explicit action; on success it redirects to the manage route
 *   - manage auto-saves every core-field change (debounced) with a live status chip
 *   - optional slug auto-derivation from `form.name`
 *
 * The page supplies `empty`, `rules`, `buildPayload(form)` and an `api` with { get, create, update }.
 * Sub-resources (media, zones, values…) stay in the page via `afterLoad` / `resetExtra` + `markSaved()`.
 */
import { ref, reactive, computed, watch, nextTick, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { debounce } from 'quasar'
import useVuelidate from '@vuelidate/core'
import { getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'

function slugify (value) {
  return (value || '').toString().trim().toLowerCase()
    .replace(/['’"]/g, '')
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
}

function capitalize (s) {
  return (s || '').charAt(0).toUpperCase() + (s || '').slice(1)
}

export function useDetailForm (opts) {
  const {
    createRouteName,
    detailRouteName,
    empty,
    rules = {},
    api,                 // { get(id), create(payload), update(id, payload) }
    buildPayload,        // (form) => payload
    entityLabel = 'record',
    deriveSlug = false,  // auto-fill form.slug from form.name until customized
    autoSave = true,     // false → no debounced auto-save; expose an explicit save() instead
    idField = 'id',      // entity key field used for the create→detail redirect (e.g. 'currencyCode')
    coreKeys,            // keys to hydrate; defaults to Object.keys(empty)
    hydrateForm,         // optional (form, entity) => void — custom field mapping when form ≠ entity 1:1
    afterLoad,           // optional async (entity) => void — load sub-resources in manage mode
    resetExtra           // optional () => void — reset sub-resource state entering create mode
  } = opts

  const route = useRoute()
  const router = useRouter()
  const notify = useNotify()

  const isCreate = computed(() => route.name === createRouteName)
  const id = computed(() => route.params.id)

  const entity = ref(null)
  const loading = ref(false)
  const creating = ref(false)

  const form = reactive({ ...empty })
  const v$ = useVuelidate(rules, form)
  const KEYS = coreKeys || Object.keys(empty)

  const saving = ref(0)
  const saveError = ref(false)
  const savedOnce = ref(false)
  const coreBlocked = ref(false)
  let hydrating = false
  let lastCore = ''
  let lastAutoSlug = ''

  const saveStatus = computed(() => {
    if (isCreate.value) return null
    if (saving.value > 0) return { label: 'Saving…', icon: 'o_sync', chip: 'blue-1', text: 'primary', spin: true }
    if (coreBlocked.value) return { label: 'Fix errors to save', icon: 'o_error_outline', chip: 'red-1', text: 'negative' }
    if (saveError.value) return { label: 'Couldn’t save — retry', icon: 'o_cloud_off', chip: 'red-1', text: 'negative' }
    if (savedOnce.value) return { label: 'All changes saved', icon: 'o_cloud_done', chip: 'green-1', text: 'positive' }
    return { label: 'Auto-save on', icon: 'o_cloud_queue', chip: 'grey-3', text: 'grey-8' }
  })

  // Wrap an async save (core or sub-resource) so the status chip and error state stay in sync.
  async function runSave (fn) {
    saving.value++
    saveError.value = false
    try {
      await fn()
      savedOnce.value = true
    } catch (err) {
      saveError.value = true
      notify.error(getApiErrorMessage(err))
    } finally {
      saving.value--
    }
  }

  // Sub-resource operations (media add/remove, etc.) call this so the header reflects "saved".
  function markSaved () { savedOnce.value = true }

  function snapshot () { return JSON.stringify(buildPayload(form)) }

  function hydrate (e) {
    hydrating = true
    lastAutoSlug = ''
    for (const k of KEYS) form[k] = e[k] ?? empty[k]
    if (hydrateForm) hydrateForm(form, e)
    lastCore = snapshot()
    nextTick(() => { hydrating = false })
  }

  const saveCore = debounce(async () => {
    if (isCreate.value) return
    const ok = await v$.value.$validate()
    if (!ok) { coreBlocked.value = true; return }
    coreBlocked.value = false
    const snap = snapshot()
    if (snap === lastCore) return
    await runSave(async () => {
      entity.value = await api.update(id.value, buildPayload(form))
      lastCore = snap
    })
  }, 800)

  if (autoSave) {
    watch(form, () => { if (!hydrating && !isCreate.value) saveCore() }, { deep: true })
  }

  // Explicit save (autoSave:false pages): create → redirect; edit → update in place.
  async function save () {
    if (isCreate.value) return create()
    const ok = await v$.value.$validate()
    if (!ok) { notify.warning('Fix the errors first'); return }
    await runSave(async () => {
      entity.value = await api.update(id.value, buildPayload(form))
      lastCore = snapshot()
      notify.success(`${capitalize(entityLabel)} saved`)
    })
  }

  if (deriveSlug) {
    watch(() => form.name, (name) => {
      if (!form.slug || form.slug === lastAutoSlug) {
        lastAutoSlug = slugify(name)
        form.slug = lastAutoSlug
      }
    })
  }

  async function create () {
    const ok = await v$.value.$validate()
    if (!ok) { notify.warning('Fill in the required fields'); return }
    creating.value = true
    try {
      const created = await api.create(buildPayload(form))
      notify.success(`${capitalize(entityLabel)} created`)
      router.replace({ name: detailRouteName, params: { id: created[idField] } })
    } catch (err) {
      notify.error(getApiErrorMessage(err))
    } finally {
      creating.value = false
    }
  }

  async function load () {
    loading.value = true
    try {
      entity.value = await api.get(id.value)
      hydrate(entity.value)
      if (afterLoad) await afterLoad(entity.value)
    } catch (err) {
      entity.value = null
      notify.error(getApiErrorMessage(err))
    } finally {
      loading.value = false
    }
  }

  function init () {
    saveError.value = false
    savedOnce.value = false
    coreBlocked.value = false
    if (isCreate.value) {
      entity.value = null
      Object.assign(form, empty)
      if (resetExtra) resetExtra()
      hydrating = true
      v$.value.$reset()
      nextTick(() => { hydrating = false })
    } else {
      load()
    }
  }

  onMounted(init)
  watch(() => route.fullPath, init)

  return {
    // state
    form, v$, entity, loading, creating, saving, isCreate, id, saveStatus,
    // actions
    create, save, runSave, markSaved, reload: init
  }
}
