<template>
  <q-page class="app-page">
    <AppListHeader
      title="Search page content"
      :breadcrumbs="[{ label: 'Home', icon: 'o_home', to: '/dashboard' }, { label: 'Search page content' }]"
      :show-add="false"
      show-back
      @back="router.push('/dashboard')"
    >
      <template #actions>
        <q-btn v-if="canWrite" color="primary" unelevated no-caps icon="o_save" label="Save" :loading="saving" :disable="loading" @click="save" />
      </template>
    </AppListHeader>

    <div class="row q-col-gutter-md">
      <div class="col-12 col-md-6">
        <AppSection title="Search bar">
          <q-inner-loading :showing="loading" />
          <AppTextField v-model="form.heading" label="Heading" placeholder="e.g. Search our catalog" :disable="!canWrite" />
          <AppTextField v-model="form.placeholderText" label="Search box placeholder" placeholder="e.g. Search for products…" :disable="!canWrite" />
          <AppTextField v-model="form.resultsCountLabel" label="Results-count label" placeholder="e.g. {count} results found" hint="Shown above the results grid" :disable="!canWrite" />
        </AppSection>
      </div>

      <div class="col-12 col-md-6">
        <AppSection title="No results">
          <AppRichText v-model="form.noResultsMessage" label="No-results message" placeholder="Message shown when a search returns nothing…" :disable="!canWrite" />
          <AppSelect v-model="form.noResultsBannerId" label="No-results banner" :options="bannerOptions" hint="Optional promotional banner to show when there are no results" :disable="!canWrite" />
          <AppSelect v-model="form.noResultsCollectionId" label="No-results collection" :options="collectionOptions" hint="Optional product collection to suggest instead" :disable="!canWrite" />
        </AppSection>
      </div>
    </div>
  </q-page>
</template>

<script setup>
/*
 * Search page content (WO-105): a singleton settings page (no list, no id). Loads the single content
 * record on mount and saves it in place with an explicit Save button — the settings pattern (cf. tax).
 * Banner / collection selectors are populated from their admin list endpoints, each with a "None" option.
 */
import { ref, reactive, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { searchContentApi, bannerApi, productCollectionApi } from 'modules/cms/api'
import { getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'
import { usePermissions } from 'composables/usePermissions'
import AppListHeader from 'components/common/AppListHeader.vue'
import AppSection from 'components/common/AppSection.vue'
import AppTextField from 'components/common/AppTextField.vue'
import AppSelect from 'components/common/AppSelect.vue'
import AppRichText from 'components/common/AppRichText.vue'

const router = useRouter()
const notify = useNotify()
const { has } = usePermissions()
const canWrite = computed(() => has('Cms.Write'))

const loading = ref(false)
const saving = ref(false)
const bannerOptions = ref([{ label: 'None', value: null }])
const collectionOptions = ref([{ label: 'None', value: null }])

const form = reactive({
  heading: '',
  placeholderText: '',
  resultsCountLabel: '',
  noResultsMessage: '',
  noResultsBannerId: null,
  noResultsCollectionId: null
})

function unwrapList (result) {
  return Array.isArray(result) ? result : result?.items || result?.data || []
}

async function loadContent () {
  loading.value = true
  try {
    const c = await searchContentApi.get()
    if (c) {
      form.heading = c.heading || ''
      form.placeholderText = c.placeholderText || ''
      form.resultsCountLabel = c.resultsCountLabel || ''
      form.noResultsMessage = c.noResultsMessage || ''
      form.noResultsBannerId = c.noResultsBannerId || null
      form.noResultsCollectionId = c.noResultsCollectionId || null
    }
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    loading.value = false
  }
}

async function loadBanners () {
  try {
    const r = await bannerApi.list({ page: 1, pageSize: 500 })
    bannerOptions.value = [{ label: 'None', value: null }, ...unwrapList(r).map((b) => ({ label: b.title || '(untitled)', value: b.id }))]
  } catch (e) { bannerOptions.value = [{ label: 'None', value: null }] }
}

async function loadCollections () {
  try {
    const r = await productCollectionApi.list({ page: 1, pageSize: 500 })
    collectionOptions.value = [{ label: 'None', value: null }, ...unwrapList(r).map((c) => ({ label: c.name, value: c.id }))]
  } catch (e) { collectionOptions.value = [{ label: 'None', value: null }] }
}

async function save () {
  saving.value = true
  try {
    await searchContentApi.update({
      heading: form.heading || null,
      placeholderText: form.placeholderText || null,
      resultsCountLabel: form.resultsCountLabel || null,
      noResultsMessage: form.noResultsMessage || null,
      noResultsBannerId: form.noResultsBannerId || null,
      noResultsCollectionId: form.noResultsCollectionId || null
    })
    notify.success('Search page content saved')
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    saving.value = false
  }
}

onMounted(() => { loadContent(); loadBanners(); loadCollections() })
</script>
