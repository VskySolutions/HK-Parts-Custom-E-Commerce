<template>
  <q-page class="app-page">
    <AppDetailHeader
      :title="isCreate ? 'New role' : (entity?.name || 'Role')"
      :breadcrumbs="[
        { label: 'Home', icon: 'o_home', to: '/dashboard' },
        { label: 'Roles', to: { name: 'roles' } },
        { label: isCreate ? 'New role' : (entity?.name || 'Role') }
      ]"
      :status="readOnly ? 'System role' : ''"
      status-color="grey"
      show-back
      @back="router.push({ name: 'roles' })"
    />

    <q-inner-loading :showing="loading" color="primary" />

    <q-banner v-if="!loading && !isCreate && !entity" class="bg-grey-2 rounded-borders">
      Role not found.
    </q-banner>

    <template v-if="isCreate || entity">
      <q-card flat bordered class="app-section">
        <q-tabs v-model="tab" align="left" active-color="primary" indicator-color="primary" class="text-grey-7 app-detail-tabs" no-caps inline-label>
          <q-tab name="general" icon="o_admin_panel_settings" label="General" />
        </q-tabs>
        <q-separator />

        <q-tab-panels v-model="tab" animated keep-alive>
          <q-tab-panel name="general" class="q-gutter-y-sm">
            <q-banner v-if="readOnly" dense rounded class="bg-grey-2 text-grey-8 q-mb-sm">
              <template #avatar><q-icon name="o_lock" color="grey-7" /></template>
              System roles are managed by the platform and cannot be edited.
            </q-banner>

            <AppTextField v-model="form.name" label="Name" required :v="v$.name" :disable="readOnly" placeholder="e.g. Catalog Manager" />
            <AppTextField v-model="form.description" label="Description" type="textarea" autogrow :disable="readOnly" placeholder="A short note describing what this role is for" />

            <div class="app-field q-mt-sm">
              <AppFieldLabel label="Modules" />
              <div class="text-body2 text-grey-7 q-mb-sm">Modules this role can access.</div>
              <q-option-group v-if="moduleOptions.length" v-model="form.accessibleModules" :options="moduleOptions" type="checkbox" color="primary" :disable="readOnly" />
              <div v-else class="text-body2 text-grey-6 q-py-sm">{{ modulesLoading ? 'Loading modules…' : 'No modules available.' }}</div>
            </div>
          </q-tab-panel>
        </q-tab-panels>

        <template v-if="!readOnly">
          <q-separator />
          <q-card-actions class="q-pa-md">
            <div class="text-caption text-grey-7">{{ isCreate ? 'Create this role.' : 'Save your changes.' }}</div>
            <q-space />
            <q-btn unelevated color="primary" no-caps :icon="isCreate ? 'o_check' : 'o_save'" :label="isCreate ? 'Create role' : 'Save'" :loading="saving > 0 || creating" @click="save" />
          </q-card-actions>
        </template>
      </q-card>
    </template>

    <AppRecordMeta entity-type="role" :record-id="entity?.id" />
  </q-page>
</template>

<script setup>
/* Role create + edit page (full-page, explicit Save). System roles are shown read-only. */
import { ref, computed } from 'vue'
import { useRouter } from 'vue-router'
import { getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'
import { useDetailForm } from 'composables/useDetailForm'
import { required, maxLength } from 'validators'
import { roleApi } from 'modules/roles/api'
import AppDetailHeader from 'components/common/AppDetailHeader.vue'
import AppTextField from 'components/common/AppTextField.vue'
import AppFieldLabel from 'components/common/AppFieldLabel.vue'

const router = useRouter()
const notify = useNotify()

const tab = ref('general')

const moduleOptions = ref([])
const modulesLoading = ref(false)
async function loadModules () {
  modulesLoading.value = true
  try {
    const list = await roleApi.modules()
    const items = Array.isArray(list) ? list : list?.items || []
    moduleOptions.value = items.map((m) => ({ label: m.displayName, value: m.key }))
  } catch (err) { notify.error(getApiErrorMessage(err)) } finally { modulesLoading.value = false }
}
loadModules()

function buildPayload (f) {
  return {
    name: (f.name || '').trim(),
    description: f.description || null,
    accessibleModules: [...(f.accessibleModules || [])]
  }
}

const {
  form, v$, entity, loading, creating, saving, isCreate, save
} = useDetailForm({
  createRouteName: 'role-new',
  detailRouteName: 'role-detail',
  entityLabel: 'role',
  autoSave: false,
  api: roleApi,
  buildPayload,
  empty: { name: '', description: '', accessibleModules: [] },
  rules: { name: { required, maxLength: maxLength(100) } },
  hydrateForm: (f, e) => { f.accessibleModules = Array.isArray(e.accessibleModules) ? [...e.accessibleModules] : [] }
})

const readOnly = computed(() => !!entity.value?.isSystemRole)
</script>

<style scoped lang="scss">
.app-detail-tabs {
  :deep(.q-tab) { min-height: 44px; }
}
</style>
