<template>
  <q-page class="app-page">
    <AppListHeader
      title="SEO"
      :breadcrumbs="[{ label: 'Home', icon: 'o_home', to: '/dashboard' }, { label: 'SEO' }]"
      :show-add="false"
    />

    <q-inner-loading :showing="loading" />

    <AppSection title="robots.txt">
      <template #actions>
        <q-badge v-if="settings.isCustomRobotsTxt" color="primary" label="Custom" />
        <q-badge v-else color="grey-6" label="Default" />
      </template>

      <AppTextField
        v-model="robots"
        type="textarea"
        label="robots.txt content"
        hint="Served at /robots.txt. Clear the field and save to revert to the built-in default (allow all + sitemap)."
        input-style="min-height: 220px; font-family: monospace"
        :disable="!canWrite"
      />

      <div class="row items-center q-gutter-sm q-mt-sm">
        <q-btn color="primary" unelevated no-caps icon="o_save" label="Save" :loading="saving" :disable="!canWrite" @click="save" />
        <q-btn flat no-caps icon="o_restart_alt" label="Reset to default" :disable="!canWrite || saving" @click="resetDefault" />
        <q-space />
        <q-btn flat no-caps icon="o_open_in_new" label="View robots.txt" type="a" :href="robotsUrl" target="_blank" />
      </div>
    </AppSection>

    <AppSection title="Sitemap">
      <div class="row items-center q-gutter-lg">
        <div>
          <div class="text-caption text-grey-7">Last generated</div>
          <div>{{ status.generatedOnUtc ? $datetime(status.generatedOnUtc) : 'Not cached yet' }}</div>
        </div>
        <div>
          <div class="text-caption text-grey-7">URLs</div>
          <div>{{ status.entryCount }}</div>
        </div>
        <q-space />
        <q-btn color="primary" outline no-caps icon="o_refresh" label="Regenerate" :loading="refreshing" @click="refresh" />
        <q-btn flat no-caps icon="o_open_in_new" label="View sitemap.xml" type="a" :href="sitemapUrl" target="_blank" />
      </div>
      <div class="text-caption text-grey-7 q-mt-sm">
        Lists all published products, categories, CMS pages, and blog posts; cached (default 15 minutes) and
        regenerated automatically as content changes. Use Regenerate to refresh immediately.
      </div>
    </AppSection>

    <AppSection title="Schema markup">
      <div class="text-body2 text-grey-8">
        Product structured data (schema.org JSON-LD) is generated automatically on every product detail page —
        name, SKU, image, brand, price, and availability. No configuration is required.
      </div>
    </AppSection>
  </q-page>
</template>

<script setup>
import { ref, reactive, computed, onMounted } from 'vue'
import { seoApi } from 'modules/cms-seo/api'
import { useNotify } from 'composables/useNotify'
import { usePermissions } from 'composables/usePermissions'
import { getApiErrorMessage } from 'services/api'

const notify = useNotify()
const { has } = usePermissions()
const canWrite = computed(() => has('Cms.Write'))

const loading = ref(false)
const saving = ref(false)
const refreshing = ref(false)
const robots = ref('')
const settings = reactive({ robotsTxt: '', isCustomRobotsTxt: false })
const status = reactive({ generatedOnUtc: null, entryCount: 0, isCached: false })

const apiBase = (process.env.API_BASE_URL || '').replace(/\/$/, '')
const robotsUrl = computed(() => `${apiBase}/robots.txt`)
const sitemapUrl = computed(() => `${apiBase}/sitemap.xml`)

async function load () {
  loading.value = true
  try {
    const [s, st] = await Promise.all([seoApi.getSettings(), seoApi.sitemapStatus()])
    Object.assign(settings, s)
    robots.value = s.robotsTxt || ''
    Object.assign(status, st)
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    loading.value = false
  }
}

async function save () {
  saving.value = true
  try {
    const s = await seoApi.updateRobots(robots.value)
    Object.assign(settings, s)
    robots.value = s.robotsTxt || ''
    notify.success('robots.txt saved')
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    saving.value = false
  }
}

async function resetDefault () {
  robots.value = ''
  await save()
}

async function refresh () {
  refreshing.value = true
  try {
    const st = await seoApi.refreshSitemap()
    Object.assign(status, st)
    notify.success('Sitemap regenerated')
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    refreshing.value = false
  }
}

onMounted(load)
</script>
