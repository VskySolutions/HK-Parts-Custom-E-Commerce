<template>
  <q-page class="app-page">
    <AppDetailHeader
      title="Branding"
      :breadcrumbs="[{ label: 'Home', icon: 'o_home', to: '/dashboard' }, { label: 'Branding' }]"
      :show-back="false"
    >
      <template #actions>
        <q-chip
          v-if="saveStatus"
          :icon="saveStatus.icon"
          :color="saveStatus.chip"
          :text-color="saveStatus.text"
          square
          dense
          class="q-mr-sm text-caption"
        >
          <q-spinner v-if="saveStatus.spin" size="14px" class="q-mr-xs" />
          {{ saveStatus.label }}
        </q-chip>
        <q-btn flat color="primary" icon="o_refresh" label="Reload" no-caps :loading="loading" @click="load" />
      </template>
    </AppDetailHeader>

    <q-inner-loading :showing="loading" color="primary" />

    <q-card flat bordered class="app-section">
      <q-tabs v-model="tab" align="left" active-color="primary" indicator-color="primary" class="text-grey-7 app-detail-tabs" no-caps inline-label>
        <q-tab name="identity" icon="o_badge" label="Identity" />
        <q-tab name="colours" icon="o_palette" label="Colours" />
        <q-tab name="media" icon="o_image" label="Logo & favicon" />
        <q-tab name="contact" icon="o_contact_mail" label="Contact & social" />
      </q-tabs>
      <q-separator />

      <q-tab-panels v-model="tab" animated keep-alive>
        <q-tab-panel name="identity" class="q-gutter-y-sm">
          <AppTextField v-model="form.brandName" label="Brand name" required :v="v$.brandName" maxlength="200" :disable="!canWrite" />
          <AppTextField v-model="form.domain" label="Domain" :v="v$.domain" placeholder="store.example.com" maxlength="255" :disable="!canWrite" />
          <AppTextField v-model="form.fontFamily" label="Font family" placeholder="Poppins, Roboto, sans-serif" :disable="!canWrite" />
          <AppSelect v-model="form.defaultLanguage" label="Default language" :options="languageOptions" clearable :disable="!canWrite" />
          <AppSelect
            v-model="form.displayTimeZone"
            label="Display timezone"
            :options="tzOptions"
            use-input
            hide-selected
            fill-input
            input-debounce="0"
            :disable="!canWrite"
            hint="Timezone used to display all UTC dates & times across the admin app and storefront."
            @filter="filterTz"
          />
        </q-tab-panel>

        <q-tab-panel name="colours">
          <div class="row q-col-gutter-xl">
            <!-- Grouped colour fields -->
            <div class="col-12 col-md-7">
              <div class="text-caption text-grey-6 q-mb-md">
                These colours theme the public storefront. Leave any field blank to keep the default.
              </div>

              <div class="clr-group text-subtitle2 text-grey-8">Brand</div>
              <div class="clr-hint">Buttons, links, badges and calls-to-action.</div>
              <ColorField v-model="form.primaryColor" label="Primary colour" :v="v$.primaryColor" :disable="!canWrite" />
              <ColorField v-model="form.secondaryColor" label="Secondary colour" :v="v$.secondaryColor" :disable="!canWrite" />
              <ColorField v-model="form.accentColor" label="Accent colour" :v="v$.accentColor" :disable="!canWrite" />

              <q-separator class="q-my-md" />
              <div class="clr-group text-subtitle2 text-grey-8">Page</div>
              <div class="clr-hint">The storefront canvas (&lt;body&gt;) and default text colour.</div>
              <ColorField v-model="form.bodyBackgroundColor" label="Body background" :v="v$.bodyBackgroundColor" :disable="!canWrite" />
              <ColorField v-model="form.textColor" label="Text colour" :v="v$.textColor" :disable="!canWrite" />

              <q-separator class="q-my-md" />
              <div class="clr-group text-subtitle2 text-grey-8">Headings</div>
              <div class="clr-hint">Set one colour for all headings, then override individual levels — H1–H6 fall back to the general colour.</div>
              <ColorField v-model="form.headingColor" label="All headings" :v="v$.headingColor" :disable="!canWrite" />
              <div class="row q-col-gutter-sm q-mt-xs">
                <div class="col-6"><ColorField v-model="form.heading1Color" label="H1" :v="v$.heading1Color" :disable="!canWrite" /></div>
                <div class="col-6"><ColorField v-model="form.heading2Color" label="H2" :v="v$.heading2Color" :disable="!canWrite" /></div>
                <div class="col-6"><ColorField v-model="form.heading3Color" label="H3" :v="v$.heading3Color" :disable="!canWrite" /></div>
                <div class="col-6"><ColorField v-model="form.heading4Color" label="H4" :v="v$.heading4Color" :disable="!canWrite" /></div>
                <div class="col-6"><ColorField v-model="form.heading5Color" label="H5" :v="v$.heading5Color" :disable="!canWrite" /></div>
                <div class="col-6"><ColorField v-model="form.heading6Color" label="H6" :v="v$.heading6Color" :disable="!canWrite" /></div>
              </div>

              <q-separator class="q-my-md" />
              <div class="clr-group text-subtitle2 text-grey-8">Text &amp; links</div>
              <div class="clr-hint">Applies to storefront content/prose (e.g. product descriptions). Component text keeps its own styling.</div>
              <ColorField v-model="form.paragraphColor" label="Paragraph (p)" :v="v$.paragraphColor" :disable="!canWrite" />
              <ColorField v-model="form.spanColor" label="Span" :v="v$.spanColor" :disable="!canWrite" />
              <ColorField v-model="form.linkColor" label="Link (a)" :v="v$.linkColor" :disable="!canWrite" />
            </div>

            <!-- Live preview -->
            <div class="col-12 col-md-5">
              <div class="clr-preview-wrap">
                <div class="text-caption text-grey-6 q-mb-xs row items-center">
                  <q-icon name="o_visibility" size="16px" class="q-mr-xs" /> Live preview
                </div>
                <div class="clr-preview" :style="{ background: pv.bodyBg, color: pv.text }">
                  <div class="clr-preview__bar">Storefront preview</div>
                  <div class="clr-preview__body">
                    <h1 :style="{ color: pv.h1 }">Heading 1</h1>
                    <h2 :style="{ color: pv.h2 }">Heading 2</h2>
                    <h3 :style="{ color: pv.h3 }">Heading 3</h3>
                    <h4 :style="{ color: pv.h4 }">Heading 4</h4>
                    <h5 :style="{ color: pv.h5 }">Heading 5</h5>
                    <h6 :style="{ color: pv.h6 }">Heading 6</h6>
                    <p :style="{ color: pv.paragraph }">
                      A sample paragraph in your body text colour, with a
                      <span :style="{ color: pv.span }">highlighted span</span> and an
                      <a :style="{ color: pv.link }">inline link</a>.
                    </p>
                    <div class="clr-preview__btns">
                      <span class="clr-preview__btn" :style="{ background: pv.accent }">Add to Cart</span>
                      <span class="clr-preview__btn" :style="{ background: pv.primary }">Buy Now</span>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </q-tab-panel>

        <q-tab-panel name="media" class="q-gutter-y-sm">
          <AppFileUpload media v-model="form.logoMediaId" v-model:preview-url="form.logoUrl" label="Logo" accept="image/*" extensions-label="PNG, JPG, SVG" :disable="!canWrite" />
          <AppFileUpload media v-model="form.faviconMediaId" v-model:preview-url="form.faviconUrl" label="Favicon" accept="image/*,.ico" extensions-label="PNG, ICO, SVG" :disable="!canWrite" />
        </q-tab-panel>

        <q-tab-panel name="contact" class="q-gutter-y-sm">
          <div class="text-subtitle2 text-grey-8 q-mb-sm">Contact</div>
          <AppTextField v-model="form.supportEmail" label="Support email" :v="v$.supportEmail" type="email" :disable="!canWrite" />
          <AppTextField v-model="form.supportPhone" label="Support phone" :v="v$.supportPhone" :disable="!canWrite" />

          <q-separator class="q-my-sm" />
          <div class="text-subtitle2 text-grey-8 q-mb-sm">Social links</div>
          <AppTextField v-model="form.facebook" label="Facebook" :v="v$.facebook" placeholder="https://…" :disable="!canWrite" />
          <AppTextField v-model="form.instagram" label="Instagram" :v="v$.instagram" placeholder="https://…" :disable="!canWrite" />
          <AppTextField v-model="form.twitter" label="X (Twitter)" :v="v$.twitter" placeholder="https://…" :disable="!canWrite" />
          <AppTextField v-model="form.linkedin" label="LinkedIn" :v="v$.linkedin" placeholder="https://…" :disable="!canWrite" />
          <AppTextField v-model="form.youtube" label="YouTube" :v="v$.youtube" placeholder="https://…" :disable="!canWrite" />
        </q-tab-panel>
      </q-tab-panels>

    </q-card>

    <q-banner v-if="!canWrite" class="bg-grey-2 rounded-borders q-mt-md text-grey-8">
      You have read-only access to branding.
    </q-banner>
  </q-page>
</template>

<script setup>
/*
 * Branding page (WO-9 REQ-TEN-001): the deployment's singleton branding as a full-page editor that
 * AUTO-SAVES every change (debounced) — no explicit Save button, matching the admin detail-page standard.
 * Persists via PUT /api/tenant/branding, then refreshes the global tenant branding (CSS vars + shell brand
 * name/logo). A live status chip in the header reflects saving / saved / blocked-by-validation / error.
 */
import { reactive, ref, computed, watch, nextTick, onMounted } from 'vue'
import { debounce } from 'quasar'
import useVuelidate from '@vuelidate/core'
import { required, maxLength, url, email, hexColor } from 'validators'
import { brandingApi } from 'modules/branding/api'
import { timeZoneOptions } from 'src/utils/datetime'
import { getApiErrorMessage } from 'services/api'
import { useTenantStore } from 'stores/tenant'
import { useNotify } from 'composables/useNotify'
import { usePermissions, Permissions } from 'composables/usePermissions'
import AppDetailHeader from 'components/common/AppDetailHeader.vue'
import AppTextField from 'components/common/AppTextField.vue'
import AppSelect from 'components/common/AppSelect.vue'
import AppFileUpload from 'components/common/AppFileUpload.vue'
import ColorField from 'modules/branding/components/ColorField.vue'

const tenant = useTenantStore()
const notify = useNotify()
const { has } = usePermissions()
const canWrite = computed(() => has(Permissions.BrandingWrite))

const tab = ref('identity')
const loading = ref(false)

// Auto-save status (mirrors the detail-page pattern): a counter of in-flight saves + error/blocked flags.
const saving = ref(0)
const saveError = ref(false)
const savedOnce = ref(false)
const coreBlocked = ref(false)
let hydrating = false
let lastSnap = ''

const saveStatus = computed(() => {
  if (!canWrite.value) return null
  if (saving.value > 0) return { label: 'Saving…', icon: 'o_sync', chip: 'blue-1', text: 'primary', spin: true }
  if (coreBlocked.value) return { label: 'Fix errors to save', icon: 'o_error_outline', chip: 'red-1', text: 'negative' }
  if (saveError.value) return { label: 'Couldn’t save — retry', icon: 'o_cloud_off', chip: 'red-1', text: 'negative' }
  if (savedOnce.value) return { label: 'All changes saved', icon: 'o_cloud_done', chip: 'green-1', text: 'positive' }
  return { label: 'Auto-save on', icon: 'o_cloud_queue', chip: 'grey-3', text: 'grey-8' }
})

const SOCIAL_KEYS = ['facebook', 'instagram', 'twitter', 'linkedin', 'youtube']
const EMPTY = {
  brandName: '', domain: '', fontFamily: '', defaultLanguage: null, displayTimeZone: 'UTC',
  primaryColor: '', secondaryColor: '', accentColor: '',
  bodyBackgroundColor: '', textColor: '', headingColor: '',
  heading1Color: '', heading2Color: '', heading3Color: '', heading4Color: '', heading5Color: '', heading6Color: '',
  paragraphColor: '', spanColor: '', linkColor: '',
  logoMediaId: null, logoUrl: '', faviconMediaId: null, faviconUrl: '',
  supportEmail: '', supportPhone: '',
  facebook: '', instagram: '', twitter: '', linkedin: '', youtube: ''
}
const form = reactive({ ...EMPTY })
const extraSocial = ref({})
const layoutOptionsJson = ref(null)

const rules = {
  brandName: { required, maxLength: maxLength(200) },
  domain: { maxLength: maxLength(255) },
  primaryColor: { hexColor, maxLength: maxLength(32) },
  secondaryColor: { hexColor, maxLength: maxLength(32) },
  accentColor: { hexColor, maxLength: maxLength(32) },
  bodyBackgroundColor: { hexColor, maxLength: maxLength(32) },
  textColor: { hexColor, maxLength: maxLength(32) },
  headingColor: { hexColor, maxLength: maxLength(32) },
  heading1Color: { hexColor, maxLength: maxLength(32) },
  heading2Color: { hexColor, maxLength: maxLength(32) },
  heading3Color: { hexColor, maxLength: maxLength(32) },
  heading4Color: { hexColor, maxLength: maxLength(32) },
  heading5Color: { hexColor, maxLength: maxLength(32) },
  heading6Color: { hexColor, maxLength: maxLength(32) },
  paragraphColor: { hexColor, maxLength: maxLength(32) },
  spanColor: { hexColor, maxLength: maxLength(32) },
  linkColor: { hexColor, maxLength: maxLength(32) },
  supportEmail: { email },
  supportPhone: { maxLength: maxLength(50) },
  facebook: { url }, instagram: { url }, twitter: { url }, linkedin: { url }, youtube: { url }
}
const v$ = useVuelidate(rules, form)

// Resolved colours for the live preview — mirrors the storefront fallback chain (per-level heading →
// general heading; paragraph/span → text; link → accent), so the preview matches the real storefront.
const pv = computed(() => {
  const heading = form.headingColor || '#1f1f2b'
  const text = form.textColor || '#2b2b2d'
  const accent = form.accentColor || '#e31e24'
  return {
    bodyBg: form.bodyBackgroundColor || '#ffffff',
    text,
    accent,
    primary: form.primaryColor || '#2b2b3a',
    h1: form.heading1Color || heading,
    h2: form.heading2Color || heading,
    h3: form.heading3Color || heading,
    h4: form.heading4Color || heading,
    h5: form.heading5Color || heading,
    h6: form.heading6Color || heading,
    paragraph: form.paragraphColor || text,
    span: form.spanColor || text,
    link: form.linkColor || accent
  }
})

const TZ_BASE = timeZoneOptions()
const tzOptions = ref(TZ_BASE)
function filterTz (needle, update) {
  const q = (needle || '').toLowerCase().trim()
  update(() => { tzOptions.value = !q ? TZ_BASE : TZ_BASE.filter((o) => o.label.toLowerCase().includes(q)) })
}

const languageOptions = [
  { label: 'English', value: 'en' }, { label: 'Spanish', value: 'es' }, { label: 'French', value: 'fr' },
  { label: 'German', value: 'de' }, { label: 'Portuguese', value: 'pt' }, { label: 'Italian', value: 'it' },
  { label: 'Dutch', value: 'nl' }, { label: 'Arabic', value: 'ar' }, { label: 'Hindi', value: 'hi' },
  { label: 'Chinese (Simplified)', value: 'zh' }, { label: 'Japanese', value: 'ja' }
]

function parseSocial (json) {
  const known = {}; const extra = {}
  if (!json) return { known, extra }
  let obj = null
  try { obj = JSON.parse(json) } catch (e) { return { known, extra } }
  if (!obj || typeof obj !== 'object') return { known, extra }
  for (const [key, value] of Object.entries(obj)) {
    if (SOCIAL_KEYS.includes(key)) known[key] = value
    else extra[key] = value
  }
  return { known, extra }
}

function hydrate (b) {
  hydrating = true
  Object.assign(form, EMPTY, {
    brandName: b.brandName || '', domain: b.domain || '', fontFamily: b.fontFamily || '',
    defaultLanguage: b.defaultLanguage || null,
    displayTimeZone: b.displayTimeZone || 'UTC',
    primaryColor: b.primaryColor || '', secondaryColor: b.secondaryColor || '', accentColor: b.accentColor || '',
    bodyBackgroundColor: b.bodyBackgroundColor || '', textColor: b.textColor || '', headingColor: b.headingColor || '',
    heading1Color: b.heading1Color || '', heading2Color: b.heading2Color || '', heading3Color: b.heading3Color || '',
    heading4Color: b.heading4Color || '', heading5Color: b.heading5Color || '', heading6Color: b.heading6Color || '',
    paragraphColor: b.paragraphColor || '', spanColor: b.spanColor || '', linkColor: b.linkColor || '',
    logoMediaId: b.logoMediaId || null, logoUrl: b.logoUrl || '',
    faviconMediaId: b.faviconMediaId || null, faviconUrl: b.faviconUrl || '',
    supportEmail: b.supportEmail || '', supportPhone: b.supportPhone || ''
  })
  const { known, extra } = parseSocial(b.socialLinksJson)
  for (const key of SOCIAL_KEYS) form[key] = known[key] || ''
  extraSocial.value = extra
  layoutOptionsJson.value = b.layoutOptionsJson || null
  v$.value.$reset()
  // Baseline the snapshot so the initial load never triggers an auto-save; re-enable the watch next tick.
  lastSnap = snapshot()
  nextTick(() => { hydrating = false })
}

function toNull (value) { const s = (value ?? '').toString().trim(); return s || null }
function buildSocialJson () {
  const out = { ...extraSocial.value }
  for (const key of SOCIAL_KEYS) { const val = (form[key] || '').trim(); if (val) out[key] = val; else delete out[key] }
  return Object.keys(out).length ? JSON.stringify(out) : null
}

async function load () {
  loading.value = true
  try {
    hydrate((await brandingApi.get()) || {})
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    loading.value = false
  }
}

function buildPayload () {
  return {
    brandName: form.brandName.trim(),
    domain: toNull(form.domain),
    logoMediaId: form.logoMediaId || null,
    faviconMediaId: form.faviconMediaId || null,
    logoUrl: form.logoMediaId ? null : toNull(form.logoUrl),
    faviconUrl: form.faviconMediaId ? null : toNull(form.faviconUrl),
    primaryColor: toNull(form.primaryColor),
    secondaryColor: toNull(form.secondaryColor),
    accentColor: toNull(form.accentColor),
    bodyBackgroundColor: toNull(form.bodyBackgroundColor),
    textColor: toNull(form.textColor),
    headingColor: toNull(form.headingColor),
    heading1Color: toNull(form.heading1Color),
    heading2Color: toNull(form.heading2Color),
    heading3Color: toNull(form.heading3Color),
    heading4Color: toNull(form.heading4Color),
    heading5Color: toNull(form.heading5Color),
    heading6Color: toNull(form.heading6Color),
    paragraphColor: toNull(form.paragraphColor),
    spanColor: toNull(form.spanColor),
    linkColor: toNull(form.linkColor),
    fontFamily: toNull(form.fontFamily),
    supportEmail: toNull(form.supportEmail),
    supportPhone: toNull(form.supportPhone),
    socialLinksJson: buildSocialJson(),
    layoutOptionsJson: layoutOptionsJson.value || null,
    defaultLanguage: form.defaultLanguage || null,
    displayTimeZone: form.displayTimeZone || 'UTC'
  }
}

function snapshot () { return JSON.stringify(buildPayload()) }

// Debounced auto-save: validate, skip if unchanged, PUT, then refresh the admin-shell branding.
const saveCore = debounce(async () => {
  if (!canWrite.value) return
  const ok = await v$.value.$validate()
  if (!ok) { coreBlocked.value = true; return }
  coreBlocked.value = false
  const payload = buildPayload()
  const snap = JSON.stringify(payload)
  if (snap === lastSnap) return
  saving.value++
  saveError.value = false
  try {
    await brandingApi.update(payload)
    lastSnap = snap
    savedOnce.value = true
    await tenant.loadBranding().catch(() => {})
  } catch (err) {
    saveError.value = true
    notify.error(getApiErrorMessage(err))
  } finally {
    saving.value--
  }
}, 800)

// Auto-save on any field change (suppressed while hydrating server data).
watch(form, () => { if (!hydrating) saveCore() }, { deep: true })

onMounted(load)
</script>

<style scoped lang="scss">
.app-detail-tabs {
  :deep(.q-tab) { min-height: 44px; }
}

.clr-group {
  font-weight: 600;
  margin-bottom: 2px;
}
.clr-hint {
  font-size: 12px;
  color: #9a9aa2;
  margin-bottom: 8px;
}

.clr-preview-wrap {
  position: sticky;
  top: 12px;
}
.clr-preview {
  border: 1px solid rgba(0, 0, 0, 0.12);
  border-radius: 8px;
  overflow: hidden;
  box-shadow: 0 2px 12px rgba(0, 0, 0, 0.07);
}
.clr-preview__bar {
  background: rgba(0, 0, 0, 0.04);
  border-bottom: 1px solid rgba(0, 0, 0, 0.08);
  padding: 6px 12px;
  font-size: 11px;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  color: #8a8a92;
}
.clr-preview__body {
  padding: 16px;

  h1, h2, h3, h4, h5, h6 { font-weight: 600; margin: 0 0 6px; line-height: 1.2; }
  h1 { font-size: 22px; }
  h2 { font-size: 19px; }
  h3 { font-size: 17px; }
  h4 { font-size: 15px; }
  h5 { font-size: 13.5px; }
  h6 { font-size: 12.5px; }
  p { margin: 12px 0 0; font-size: 13.5px; line-height: 1.5; }
  a { text-decoration: underline; cursor: pointer; }
}
.clr-preview__btns {
  display: flex;
  gap: 8px;
  margin-top: 16px;
}
.clr-preview__btn {
  color: #fff;
  font-size: 12px;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.4px;
  padding: 8px 14px;
  border-radius: 3px;
}
</style>
