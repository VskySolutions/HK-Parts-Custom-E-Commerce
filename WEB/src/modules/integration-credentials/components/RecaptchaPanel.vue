<template>
  <div>
    <PanelHeader :item="item">
      <template #actions>
        <q-btn flat color="primary" icon="o_refresh" no-caps :loading="loading" @click="load" />
        <q-btn v-if="canWrite" unelevated color="primary" icon="o_save" label="Save changes" no-caps :loading="saving" @click="onSave" />
      </template>
    </PanelHeader>

    <div class="row q-col-gutter-md">
      <!-- Keys + behaviour -->
      <div class="col-12 col-md-7">
        <q-card flat bordered>
          <q-card-section class="q-gutter-y-sm">
            <div class="text-subtitle1 q-mb-xs">Keys</div>

            <AppTextField v-model="form.siteKey" label="Site Key" :v="v$.siteKey" maxlength="200" placeholder="6Lc…" autocomplete="off">
              <template #hint><span class="text-caption text-grey-7">Public key used by the storefront widget.</span></template>
            </AppTextField>

            <AppTextField
              v-model="form.secretKey"
              label="Secret Key"
              :type="showSecret ? 'text' : 'password'"
              autocomplete="new-password"
              :placeholder="hasSecretKey ? 'Leave blank to keep the stored value' : '6Lc…'"
            >
              <template #hint>
                <span v-if="hasSecretKey" class="text-caption text-grey-7">Stored: {{ secretKeyMasked || '••••' }} — encrypted at rest. Re-enter to replace it.</span>
                <span v-else class="text-caption text-grey-7">Encrypted at rest and shown masked after saving.</span>
              </template>
              <template #append>
                <q-icon :name="showSecret ? 'o_visibility_off' : 'o_visibility'" class="cursor-pointer" @click="showSecret = !showSecret" />
              </template>
            </AppTextField>

            <q-separator class="q-my-sm" />
            <div class="text-subtitle1 q-mb-xs">Behaviour</div>

            <div class="row q-col-gutter-sm">
              <div class="col-12 col-sm-6"><AppSelect v-model="form.version" label="Version" :options="versionOptions" /></div>
              <div v-if="form.version === 'V3'" class="col-12 col-sm-6">
                <AppTextField v-model.number="form.scoreThreshold" label="Score threshold" type="number" :v="v$.scoreThreshold" step="0.1" min="0" max="1">
                  <template #hint><span class="text-caption text-grey-7">v3 only — minimum passing score (0.0–1.0).</span></template>
                </AppTextField>
              </div>
            </div>

            <AppSelect v-model="form.failBehaviour" label="If Google is unreachable" :options="failOptions" />
          </q-card-section>

          <q-separator />
          <q-card-actions class="q-pa-md items-center">
            <span class="text-caption text-grey-7 q-mr-sm">Status</span>
            <q-badge :color="isConfigured ? 'positive' : 'grey-5'" :label="isConfigured ? 'Configured' : 'Not configured'" />
          </q-card-actions>
        </q-card>
      </div>

      <!-- Per-form protection -->
      <div class="col-12 col-md-5">
        <q-card flat bordered>
          <q-card-section>
            <div class="text-subtitle1 q-mb-sm">Protected forms</div>
            <div class="text-caption text-grey-7 q-mb-sm">Enable a reCAPTCHA challenge on each storefront form.</div>
            <div v-for="flag in formFlags" :key="flag.key" class="row items-center justify-between q-py-xs">
              <div class="text-body2">
                {{ flag.label }}
                <q-badge v-if="flag.planned" color="orange-6" label="not yet wired" class="q-ml-xs" outline />
              </div>
              <q-toggle v-model="form.perFormSettings[flag.key]" color="primary" :disable="!canWrite" />
            </div>
            <div class="text-caption text-grey-6 q-mt-sm">
              Contact, Newsletter, Review and Q&A forms are configurable here but not yet implemented in the storefront.
            </div>
          </q-card-section>
        </q-card>
      </div>
    </div>
  </div>
</template>

<script setup>
/*
 * RecaptchaPanel: the singleton Google reCAPTCHA config (WO-106) — Site/Secret keys (secret write-only,
 * masked), version + v3 score threshold, unreachable-API fail behaviour, and per-form protection flags.
 * Backed by GET/PUT /api/tenant/recaptcha (Settings module).
 */
import { reactive, ref, computed, onMounted } from 'vue'
import useVuelidate from '@vuelidate/core'
import { maxLength, minValue, maxValue } from 'validators'
import { recaptchaApi } from 'modules/integration-credentials/api'
import { getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'
import { usePermissions, Permissions } from 'composables/usePermissions'
import PanelHeader from 'modules/integration-credentials/components/PanelHeader.vue'

defineProps({ item: { type: Object, required: true } })

const notify = useNotify()
const { has } = usePermissions()
const canWrite = computed(() => has(Permissions.SettingsWrite))

const versionOptions = [
  { label: 'v3 (score-based)', value: 'V3' },
  { label: 'v2 Checkbox', value: 'V2Checkbox' },
  { label: 'v2 Invisible', value: 'V2Invisible' }
]
const failOptions = [
  { label: 'Fail open — allow the submission', value: 'FailOpen' },
  { label: 'Fail closed — block the submission', value: 'FailClosed' }
]
const formFlags = [
  { key: 'register', label: 'Register' },
  { key: 'login', label: 'Login' },
  { key: 'passwordReset', label: 'Password reset' },
  { key: 'guestCheckout', label: 'Guest checkout' },
  { key: 'contact', label: 'Contact form', planned: true },
  { key: 'newsletter', label: 'Newsletter signup', planned: true },
  { key: 'reviewSubmit', label: 'Product review', planned: true },
  { key: 'qaSubmit', label: 'Q&A submission', planned: true },
  { key: 'inquiry', label: 'Inquiry form' }
]

function emptyForms () {
  return { register: false, login: false, passwordReset: false, guestCheckout: false, contact: false, newsletter: false, reviewSubmit: false, qaSubmit: false, inquiry: false }
}

const form = reactive({ siteKey: '', secretKey: '', version: 'V3', scoreThreshold: 0.5, failBehaviour: 'FailOpen', perFormSettings: emptyForms() })
const hasSecretKey = ref(false)
const secretKeyMasked = ref('')
const showSecret = ref(false)
const loading = ref(false)
const saving = ref(false)

const isConfigured = computed(() => !!form.siteKey && (hasSecretKey.value || !!form.secretKey))

const rules = {
  siteKey: { maxLength: maxLength(200) },
  scoreThreshold: { minValue: minValue(0), maxValue: maxValue(1) }
}
const v$ = useVuelidate(rules, form)

function applyConfig (dto) {
  form.siteKey = dto.siteKey || ''
  form.version = dto.version || 'V3'
  form.scoreThreshold = dto.scoreThreshold != null ? dto.scoreThreshold : 0.5
  form.failBehaviour = dto.failBehaviour || 'FailOpen'
  const pf = dto.perFormSettings || {}
  form.perFormSettings = { ...emptyForms(), ...Object.fromEntries(Object.keys(emptyForms()).map((k) => [k, !!pf[k]])) }
  form.secretKey = ''
  hasSecretKey.value = !!dto.hasSecretKey
  secretKeyMasked.value = dto.secretKeyMasked || ''
  v$.value.$reset()
}

async function load () {
  loading.value = true
  try {
    applyConfig(await recaptchaApi.get())
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    loading.value = false
  }
}

async function onSave () {
  const ok = await v$.value.$validate()
  if (!ok) return
  saving.value = true
  try {
    const dto = await recaptchaApi.update({
      siteKey: form.siteKey.trim() || null,
      secretKey: form.secretKey ? form.secretKey.trim() : null,
      version: form.version,
      scoreThreshold: Number(form.scoreThreshold),
      failBehaviour: form.failBehaviour,
      perFormSettings: { ...form.perFormSettings }
    })
    applyConfig(dto)
    notify.success('reCAPTCHA settings saved')
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    saving.value = false
  }
}

onMounted(load)
</script>
