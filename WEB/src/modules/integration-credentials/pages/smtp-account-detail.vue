<template>
  <q-page class="app-page">
    <AppDetailHeader
      :title="isCreate ? 'New SMTP account' : (entity?.displayName || 'SMTP account')"
      :breadcrumbs="[
        { label: 'Home', icon: 'o_home', to: '/dashboard' },
        { label: 'Integrations', to: { name: 'integrations', query: { section: 'smtp' } } },
        { label: isCreate ? 'New SMTP account' : (entity?.displayName || 'SMTP account') }
      ]"
      :status="!isCreate && entity ? (form.enabled ? 'Enabled' : 'Disabled') : ''"
      :status-color="form.enabled ? 'positive' : 'grey'"
      show-back
      @back="router.push({ name: 'integrations', query: { section: 'smtp' } })"
    />

    <q-inner-loading :showing="loading" color="primary" />

    <q-banner v-if="!loading && !isCreate && !entity" class="bg-grey-2 rounded-borders">
      SMTP account not found.
    </q-banner>

    <template v-if="isCreate || entity">
      <q-card flat bordered class="app-section">
        <q-tabs v-model="tab" align="left" active-color="primary" indicator-color="primary" class="text-grey-7 app-detail-tabs" no-caps inline-label>
          <q-tab name="general" icon="o_mail" label="General" />
        </q-tabs>
        <q-separator />

        <q-tab-panels v-model="tab" animated keep-alive>
          <q-tab-panel name="general" class="q-gutter-y-sm">
            <AppTextField v-model="form.displayName" label="Display name" required :v="v$.displayName" maxlength="200" placeholder="e.g. Primary transactional" />

            <div class="row q-col-gutter-sm">
              <div class="col-8"><AppTextField v-model="form.host" label="Host" required :v="v$.host" maxlength="255" placeholder="smtp.example.com" /></div>
              <div class="col-4"><AppTextField v-model="form.port" label="Port" required :v="v$.port" type="number" placeholder="587" /></div>
            </div>

            <div class="row q-col-gutter-sm">
              <div class="col-6"><AppSelect v-model="form.encryptionMode" label="Encryption" :options="encryptionOptions" /></div>
              <div class="col-6"><AppSelect v-model="form.authMethod" label="Auth method" :options="authMethodOptions" /></div>
            </div>

            <AppTextField v-model="form.username" label="Username" :v="v$.username" maxlength="255" autocomplete="off" />
            <AppTextField v-model="form.password" label="Password" :type="showPassword ? 'text' : 'password'" autocomplete="new-password">
              <template #hint>
                <span v-if="!isCreate && entity?.hasPassword" class="text-caption text-grey-7">A password is stored — leave blank to keep it.</span>
                <span v-else class="text-caption text-grey-7">Optional; required only if your server needs authentication.</span>
              </template>
              <template #append><q-icon :name="showPassword ? 'o_visibility_off' : 'o_visibility'" class="cursor-pointer" @click="showPassword = !showPassword" /></template>
            </AppTextField>

            <div class="row q-col-gutter-sm">
              <div class="col-6"><AppTextField v-model="form.fromName" label="From name" required :v="v$.fromName" maxlength="200" placeholder="Acme Store" /></div>
              <div class="col-6"><AppTextField v-model="form.fromEmail" label="From email" required :v="v$.fromEmail" maxlength="255" placeholder="noreply@example.com" /></div>
            </div>

            <AppSelect v-model="form.category" label="Notification category" :options="categoryOptions" />
            <div class="text-caption text-grey-7 q-mb-sm">Only one enabled account is used per category. Enabling this account disables any other enabled account in the same category.</div>
            <q-toggle v-model="form.enabled" label="Enabled" color="primary" />
          </q-tab-panel>
        </q-tab-panels>

        <q-separator />
        <q-card-actions class="q-pa-md">
          <div class="text-caption text-grey-7">{{ isCreate ? 'Create this SMTP account.' : 'Save your changes.' }}</div>
          <q-space />
          <q-btn v-if="canWrite" unelevated color="primary" no-caps :icon="isCreate ? 'o_check' : 'o_save'" :label="isCreate ? 'Create account' : 'Save'" :loading="saving > 0 || creating" @click="save" />
        </q-card-actions>
      </q-card>
    </template>

    <AppRecordMeta entity-type="smtp-account" :record-id="entity?.id" />
  </q-page>
</template>

<script setup>
/* SMTP account create + edit page (full-page, explicit Save), reachable from the Integrations hub's SMTP
 * section. The password is write-only — leave blank on edit to keep the stored one. */
import { ref, computed } from 'vue'
import { useRouter } from 'vue-router'
import { usePermissions } from 'composables/usePermissions'
import { useDetailForm } from 'composables/useDetailForm'
import { required, maxLength, minValue, maxValue, integer, email } from 'validators'
import { smtpAccountApi } from 'modules/integration-credentials/api'
import AppDetailHeader from 'components/common/AppDetailHeader.vue'
import AppTextField from 'components/common/AppTextField.vue'
import AppSelect from 'components/common/AppSelect.vue'

const router = useRouter()
const { has } = usePermissions()
const canWrite = computed(() => has('SmtpAccounts.Write'))

const tab = ref('general')
const showPassword = ref(false)

const encryptionOptions = [
  { label: 'None', value: 'None' }, { label: 'SSL', value: 'Ssl' }, { label: 'TLS', value: 'Tls' }, { label: 'STARTTLS', value: 'StartTls' }
]
const authMethodOptions = [
  { label: 'Auto', value: 'Auto' }, { label: 'LOGIN', value: 'Login' }, { label: 'PLAIN', value: 'Plain' }, { label: 'CRAM-MD5', value: 'CramMd5' }, { label: 'OAuth2', value: 'OAuth2' }
]
const categoryOptions = [
  { label: 'Unassigned', value: null }, { label: 'Transactional', value: 'Transactional' }, { label: 'Marketing', value: 'Marketing' }
]

function buildPayload (f) {
  return {
    displayName: (f.displayName || '').trim(),
    host: (f.host || '').trim(),
    port: Number(f.port),
    username: (f.username || '').trim() || null,
    // Blank password => null: on update the API keeps the stored one; on create it means "no auth".
    password: f.password ? f.password : null,
    fromName: (f.fromName || '').trim(),
    fromEmail: (f.fromEmail || '').trim(),
    encryptionMode: f.encryptionMode,
    authMethod: f.authMethod,
    category: f.category || null,
    enabled: !!f.enabled
  }
}

const {
  form, v$, entity, loading, creating, saving, isCreate, save
} = useDetailForm({
  createRouteName: 'smtp-account-new',
  detailRouteName: 'smtp-account-detail',
  entityLabel: 'SMTP account',
  autoSave: false,
  api: smtpAccountApi,
  buildPayload,
  empty: {
    displayName: '', host: '', port: 587, username: '', password: '',
    fromName: '', fromEmail: '', encryptionMode: 'StartTls', authMethod: 'Auto', category: null, enabled: true
  },
  rules: {
    displayName: { required, maxLength: maxLength(200) },
    host: { required, maxLength: maxLength(255) },
    port: { required, integer, minValue: minValue(1), maxValue: maxValue(65535) },
    username: { maxLength: maxLength(255) },
    fromName: { required, maxLength: maxLength(200) },
    fromEmail: { required, email, maxLength: maxLength(255) }
  },
  hydrateForm: (f) => { f.password = '' }
})
</script>

<style scoped lang="scss">
.app-detail-tabs {
  :deep(.q-tab) { min-height: 44px; }
}
</style>
