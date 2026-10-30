<template>
  <q-page class="app-page">
    <AppDetailHeader
      :title="template ? template.name : 'Email template'"
      :subtitle="subtitle"
      :status="template ? (template.enabled ? 'Enabled' : 'Disabled') : ''"
      :status-color="template && template.enabled ? 'positive' : 'grey'"
      @back="goBack"
    >
      <template #actions>
        <q-toggle
          v-if="template && canWrite"
          :model-value="template.enabled"
          :label="template.enabled ? 'Enabled' : 'Disabled'"
          color="positive"
          :disable="togglingEnabled"
          @update:model-value="onToggleEnabled"
        />
        <q-btn
          v-if="canWrite"
          flat
          color="primary"
          icon="o_send"
          label="Test send"
          no-caps
          :disable="!template"
          @click="testSendOpen = true"
        />
        <q-btn
          v-if="canWrite"
          unelevated
          color="primary"
          icon="o_save"
          label="Save"
          no-caps
          :loading="saving"
          :disable="!template"
          @click="onSave"
        />
      </template>
    </AppDetailHeader>

    <q-inner-loading :showing="loading" color="primary" />

    <q-banner v-if="!loading && !template" class="bg-grey-2 rounded-borders">
      Template not found.
    </q-banner>

    <template v-if="template">
      <q-banner v-if="!canWrite" class="bg-grey-2 rounded-borders q-mb-md text-grey-8">
        You have read-only access to email templates.
      </q-banner>

      <q-banner
        v-if="smtpInfo && !smtpInfo.hasSmtpConfigured"
        class="bg-orange-1 text-orange-9 rounded-borders q-mb-md"
      >
        <template #avatar><q-icon name="o_warning" color="orange-9" /></template>
        No enabled SMTP account is assigned to the {{ template.category }} category. Test-sends and
        live delivery for this template will fail until one is configured.
      </q-banner>

      <div class="row q-col-gutter-md">
        <!-- Editor column -->
        <div class="col-12 col-md-8">
          <q-card flat bordered>
            <q-card-section class="q-gutter-md">
              <AppTextField
                v-model="form.subjectLine"
                label="Subject line"
                required
                :readonly="!canWrite"
                :v="v$.subjectLine"
                maxlength="512"
              />

              <div class="app-field">
                <div class="row items-center no-wrap q-mb-xs">
                  <AppFieldLabel label="HTML body" class="col" />
                  <q-btn-toggle
                    v-model="bodyMode"
                    dense
                    unelevated
                    no-caps
                    toggle-color="primary"
                    color="grey-3"
                    text-color="grey-8"
                    :options="bodyModeOptions"
                  />
                </div>

                <q-editor
                  v-show="bodyMode === 'wysiwyg'"
                  v-model="form.htmlBody"
                  :readonly="!canWrite"
                  content-style="min-height: 320px"
                  :toolbar="editorToolbar"
                />
                <q-input
                  v-show="bodyMode === 'html'"
                  v-model="form.htmlBody"
                  type="textarea"
                  outlined
                  :readonly="!canWrite"
                  input-style="min-height: 320px; font-family: 'Roboto Mono', ui-monospace, monospace; font-size: 13px"
                />
                <div class="text-caption text-grey-6 q-mt-xs">
                  Visual mode is best for body content; full HTML documents are best edited in HTML mode.
                  Both modes edit the same content.
                </div>
              </div>

              <AppTextField
                v-model="form.plainTextBody"
                label="Plain-text body (fallback)"
                type="textarea"
                autogrow
                :readonly="!canWrite"
                input-style="min-height: 96px"
              />
            </q-card-section>
          </q-card>
        </div>

        <!-- Dynamic variables column -->
        <div class="col-12 col-md-4">
          <q-card flat bordered>
            <q-card-section>
              <div class="text-subtitle1">Dynamic variables</div>
              <div class="text-caption text-grey-6 q-mb-sm">
                Placeholders used by this template. Click to copy.
              </div>
              <q-list v-if="usedVariables.length" separator>
                <q-item
                  v-for="v in usedVariables"
                  :key="v.token"
                  clickable
                  @click="copyToken(v.token)"
                >
                  <q-item-section>
                    <q-item-label><code>{{ tokenText(v.token) }}</code></q-item-label>
                    <q-item-label caption>{{ v.description }}</q-item-label>
                    <q-item-label v-if="v.sample" caption class="text-grey-5">
                      e.g. {{ v.sample }}
                    </q-item-label>
                  </q-item-section>
                  <q-item-section side>
                    <q-icon name="o_content_copy" size="16px" color="grey-6" />
                  </q-item-section>
                </q-item>
              </q-list>
              <div v-else class="text-body2 text-grey-6">No dynamic variables detected.</div>
            </q-card-section>
          </q-card>
        </div>
      </div>

      <!-- Preview pane -->
      <q-card flat bordered class="q-mt-md">
        <q-card-section class="row items-center">
          <div class="col">
            <div class="text-subtitle1">Preview</div>
            <div class="text-caption text-grey-6">
              Rendered with sample data — reflects your unsaved edits.
            </div>
          </div>
          <q-btn
            flat
            color="primary"
            icon="o_refresh"
            label="Refresh"
            no-caps
            :loading="previewing"
            @click="refreshPreview"
          />
        </q-card-section>
        <q-separator />

        <q-card-section v-if="preview">
          <div class="row q-col-gutter-md q-mb-sm">
            <div class="col-12 col-sm">
              <div class="text-caption text-grey-6">Subject</div>
              <div class="text-body1 text-weight-medium">{{ preview.subject || '—' }}</div>
            </div>
            <div class="col-12 col-sm-auto">
              <div class="text-caption text-grey-6">From</div>
              <div class="text-body1">{{ preview.fromName || '—' }}</div>
            </div>
          </div>

          <q-tabs
            v-model="previewTab"
            dense
            align="left"
            active-color="primary"
            indicator-color="primary"
            class="text-grey-7"
          >
            <q-tab name="html" icon="o_html" label="HTML" no-caps />
            <q-tab name="text" icon="o_notes" label="Plain text" no-caps />
          </q-tabs>
          <q-separator />
          <q-tab-panels v-model="previewTab" animated>
            <q-tab-panel name="html" class="q-pa-none">
              <iframe class="preview-frame" sandbox="" :srcdoc="preview.htmlBody" title="HTML preview" />
            </q-tab-panel>
            <q-tab-panel name="text" class="q-pa-none">
              <pre class="preview-text">{{ preview.textBody || '(no plain-text body)' }}</pre>
            </q-tab-panel>
          </q-tab-panels>
        </q-card-section>

        <q-card-section v-else class="text-grey-6">
          <q-spinner v-if="previewing" color="primary" size="20px" class="q-mr-sm" />
          <span>{{ previewing ? 'Rendering preview…' : 'No preview available.' }}</span>
        </q-card-section>
      </q-card>
    </template>

    <TestSendDialog
      v-model="testSendOpen"
      :sending="testSending"
      :smtp-configured="!smtpInfo || smtpInfo.hasSmtpConfigured"
      :category="template ? template.category : ''"
      @submit="onTestSend"
    />

    <AppRecordMeta entity-type="email-template" :record-id="template?.id" />
  </q-page>
</template>

<script setup>
/*
 * Email template editor (WO-80, REQ-ENT-002 / REQ-ENT-003 / REQ-ENT-004).
 *
 * Keyed by ?key= (see routes.js). Provides:
 *  - subject + a dual-mode body editor (Quasar q-editor WYSIWYG and a raw-HTML
 *    textarea, both bound to the same html string so switching never loses work);
 *  - a Dynamic Variables panel listing the {{tokens}} this template uses;
 *  - a live-ish preview (debounced POST .../preview) with rendered subject,
 *    from-name, and HTML + plain-text in visually distinct tabs;
 *  - a test-send dialog (POST .../test-send); and
 *  - an enable/disable toggle that confirms before disabling a critical template.
 */
import { ref, reactive, computed, watch, onMounted, onBeforeUnmount } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import useVuelidate from '@vuelidate/core'
import { required, maxLength } from 'validators'
import { emailTemplatesApi } from 'modules/email-templates/api'
import { getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'
import { useConfirm } from 'composables/useConfirm'
import { usePermissions, Permissions } from 'composables/usePermissions'
import { extractTokens, describeTokens } from 'modules/email-templates/variables'
import TestSendDialog from 'modules/email-templates/components/TestSendDialog.vue'

const route = useRoute()
const router = useRouter()
const notify = useNotify()
const confirm = useConfirm()
const { has } = usePermissions()

const canWrite = computed(() => has(Permissions.EmailTemplatesWrite))
const templateKey = computed(() => route.query.key || '')

const template = ref(null)
const smtpInfo = ref(null)
const loading = ref(false)
const saving = ref(false)
const togglingEnabled = ref(false)
const ready = ref(false)

const bodyMode = ref('html')
const bodyModeOptions = [
  { label: 'Visual', value: 'wysiwyg', icon: 'o_visibility' },
  { label: 'HTML', value: 'html', icon: 'o_code' }
]
const editorToolbar = [
  ['bold', 'italic', 'underline', 'strike'],
  ['link'],
  [{ label: 'Format', icon: 'o_format_size', options: ['p', 'h1', 'h2', 'h3'] }],
  ['unordered', 'ordered'],
  ['undo', 'redo', 'removeFormat']
]

const form = reactive({ subjectLine: '', htmlBody: '', plainTextBody: '' })
const rules = { subjectLine: { required, maxLength: maxLength(512) } }
const v$ = useVuelidate(rules, form)

const subtitle = computed(() =>
  template.value ? `${template.value.templateKey} · ${template.value.category}` : ''
)

const usedVariables = computed(() =>
  describeTokens(extractTokens(form.subjectLine, form.htmlBody, form.plainTextBody))
)

function tokenText (token) {
  return `{{${token}}}`
}

// ---- Preview (debounced) --------------------------------------------------
const preview = ref(null)
const previewing = ref(false)
const previewTab = ref('html')
let previewTimer = null

async function refreshPreview () {
  if (!template.value) return
  previewing.value = true
  try {
    preview.value = await emailTemplatesApi.preview({
      key: template.value.templateKey,
      body: {
        subject: form.subjectLine,
        htmlBody: form.htmlBody,
        plainTextBody: form.plainTextBody
      }
    })
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    previewing.value = false
  }
}

function schedulePreview () {
  if (previewTimer) clearTimeout(previewTimer)
  previewTimer = setTimeout(refreshPreview, 700)
}

watch(
  () => [form.subjectLine, form.htmlBody, form.plainTextBody],
  () => {
    if (ready.value) schedulePreview()
  }
)

// ---- Load -----------------------------------------------------------------
async function load () {
  ready.value = false
  preview.value = null
  const key = templateKey.value
  if (!key) {
    template.value = null
    return
  }
  loading.value = true
  try {
    const dto = await emailTemplatesApi.get(key)
    template.value = dto
    form.subjectLine = dto.subjectLine || ''
    form.htmlBody = dto.htmlBody || ''
    form.plainTextBody = dto.plainTextBody || ''
    v$.value.$reset()
    await loadSmtpInfo(key)
    await refreshPreview()
    ready.value = true
  } catch (err) {
    template.value = null
    notify.error(getApiErrorMessage(err))
  } finally {
    loading.value = false
  }
}

// The detail DTO carries no SMTP info; pull it from the list summary so the
// "no SMTP account" warning is accurate even on a direct/deep-linked load.
async function loadSmtpInfo (key) {
  try {
    const summaries = await emailTemplatesApi.list({ search: key })
    const match = (Array.isArray(summaries) ? summaries : []).find((s) => s.templateKey === key)
    smtpInfo.value = match
      ? { hasSmtpConfigured: match.hasSmtpConfigured, assignedSmtpAccountName: match.assignedSmtpAccountName }
      : null
  } catch (err) {
    smtpInfo.value = null
  }
}

// ---- Save -----------------------------------------------------------------
async function onSave () {
  const ok = await v$.value.$validate()
  if (!ok) return
  saving.value = true
  try {
    const dto = await emailTemplatesApi.update(template.value.templateKey, {
      name: template.value.name,
      subjectLine: form.subjectLine,
      htmlBody: form.htmlBody,
      plainTextBody: form.plainTextBody || null,
      description: template.value.description ?? null
    })
    template.value = dto
    notify.success('Template saved')
    await refreshPreview()
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    saving.value = false
  }
}

// ---- Enable / disable -----------------------------------------------------
async function onToggleEnabled (value) {
  if (!template.value) return
  let confirmFlag = false
  // Disabling a critical template (Password Reset, Email Verification) requires
  // explicit confirmation — the backend also enforces this (409 without it).
  if (!value && template.value.isCritical) {
    const ok = await confirm({
      title: 'Disable critical template?',
      message:
        `"${template.value.name}" is a critical template. Disabling it means customers will no ` +
        'longer receive this essential email. Are you sure you want to continue?',
      okLabel: 'Disable',
      cancelLabel: 'Keep enabled',
      color: 'negative'
    })
    if (!ok) return
    confirmFlag = true
  }
  togglingEnabled.value = true
  try {
    const dto = await emailTemplatesApi.setEnabled(template.value.templateKey, value, confirmFlag)
    template.value = dto
    notify.success(value ? 'Template enabled' : 'Template disabled')
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    togglingEnabled.value = false
  }
}

// ---- Test send ------------------------------------------------------------
const testSendOpen = ref(false)
const testSending = ref(false)

async function onTestSend (recipientEmail) {
  if (!template.value) return
  testSending.value = true
  try {
    const result = await emailTemplatesApi.testSend({
      key: template.value.templateKey,
      recipientEmail
    })
    if (result && result.dispatched) {
      notify.success(result.message || `Test email queued for ${result.recipientEmail}.`)
      testSendOpen.value = false
    } else {
      notify.warning((result && result.message) || 'The test email could not be sent.')
    }
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    testSending.value = false
  }
}

// ---- Misc -----------------------------------------------------------------
async function copyToken (token) {
  const text = tokenText(token)
  try {
    await navigator.clipboard.writeText(text)
    notify.info(`Copied ${text}`)
  } catch (err) {
    notify.warning('Could not copy to clipboard.')
  }
}

function goBack () {
  router.push({ name: 'email-templates' })
}

watch(templateKey, load)
onMounted(load)
onBeforeUnmount(() => {
  if (previewTimer) clearTimeout(previewTimer)
})
</script>

<style scoped>
.preview-frame {
  display: block;
  width: 100%;
  min-height: 520px;
  border: 0;
  background: #ffffff;
}
.preview-text {
  margin: 0;
  padding: 16px;
  min-height: 200px;
  white-space: pre-wrap;
  word-break: break-word;
  font-family: 'Roboto Mono', ui-monospace, 'SF Mono', Menlo, Consolas, monospace;
  font-size: 13px;
  line-height: 1.5;
  color: #24292f;
  background: #f6f8fa;
}
</style>
