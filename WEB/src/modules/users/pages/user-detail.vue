<template>
  <q-page class="app-page">
    <AppDetailHeader
      :title="isCreate ? 'New user' : (entity?.fullName || entity?.email || 'User')"
      :breadcrumbs="[
        { label: 'Home', icon: 'o_home', to: '/dashboard' },
        { label: 'Users', to: { name: 'users' } },
        { label: isCreate ? 'New user' : (entity?.fullName || entity?.email || 'User') }
      ]"
      :status="!isCreate && entity ? (form.isActive ? 'Active' : 'Inactive') : ''"
      :status-color="form.isActive ? 'positive' : 'grey'"
      show-back
      @back="router.push({ name: 'users' })"
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

        <q-btn v-if="!isCreate && entity" no-caps unelevated color="primary" icon="o_manage_accounts" label="Manage access">
          <q-menu anchor="bottom right" self="top right">
            <q-list style="min-width: 220px">
              <q-item v-close-popup clickable :disable="sendingReset" @click="onSendReset">
                <q-item-section avatar><q-icon name="o_lock_reset" color="primary" /></q-item-section>
                <q-item-section>
                  <q-item-label>Send reset link</q-item-label>
                  <q-item-label caption>Email the user a password-reset link</q-item-label>
                </q-item-section>
              </q-item>
              <q-item v-close-popup clickable @click="openSetPassword">
                <q-item-section avatar><q-icon name="o_password" color="primary" /></q-item-section>
                <q-item-section>
                  <q-item-label>Set password</q-item-label>
                  <q-item-label caption>Set a new password directly</q-item-label>
                </q-item-section>
              </q-item>
            </q-list>
          </q-menu>
        </q-btn>
      </template>
    </AppDetailHeader>

    <q-dialog v-model="pwdDialog">
      <q-card style="min-width: 380px; max-width: 92vw">
        <q-card-section class="row items-center q-gutter-sm">
          <q-icon name="o_password" color="primary" size="24px" />
          <div class="text-subtitle1 text-weight-medium">Set password</div>
        </q-card-section>
        <q-separator />
        <q-form @submit.prevent="onSetPassword">
          <q-card-section class="q-gutter-md">
            <div class="text-caption text-grey-7">
              Set a new password for <strong>{{ entity?.email }}</strong>. They can change it later.
            </div>
            <AppPasswordField v-model="newPwd" label="New password" strength :rules="passwordRules()" />
            <AppPasswordField v-model="confirmPwd" label="Confirm new password" :rules="[matchRule(() => newPwd)]" />
          </q-card-section>
          <q-card-actions align="right" class="q-pa-md">
            <q-btn v-close-popup flat no-caps color="grey-8" label="Cancel" />
            <q-btn type="submit" unelevated no-caps color="primary" label="Set password" :loading="settingPwd" />
          </q-card-actions>
        </q-form>
      </q-card>
    </q-dialog>

    <q-inner-loading :showing="loading" color="primary" />

    <q-banner v-if="!loading && !isCreate && !entity" class="bg-grey-2 rounded-borders">
      User not found.
    </q-banner>

    <template v-if="isCreate || entity">
      <div v-if="!isCreate" class="row items-center text-caption text-grey-7 q-mb-sm q-px-xs">
        <q-icon name="o_cloud_sync" size="16px" class="q-mr-xs" />
        Changes are saved automatically as you edit — no need to press save.
      </div>

      <q-card flat bordered class="app-section">
        <q-tabs
          v-model="tab"
          align="left"
          active-color="primary"
          indicator-color="primary"
          class="text-grey-7 app-detail-tabs"
          no-caps
          inline-label
        >
          <q-tab name="general" icon="o_person" label="General" />
        </q-tabs>

        <q-separator />

        <q-tab-panels v-model="tab" animated keep-alive>
          <q-tab-panel name="general" class="q-gutter-y-sm">
            <div class="row q-col-gutter-sm">
              <div class="col-12 col-md-6">
                <AppTextField v-model="form.email" label="Email" type="email" required :v="v$.email" :disable="!isCreate" hint="Cannot be changed after creation" />
              </div>
              <div v-if="isCreate" class="col-12 col-md-6">
                <AppPasswordField v-model="form.password" label="Password" required strength :v="v$.password">
                  <template #hint>8–16 characters, incl. a letter &amp; a number</template>
                </AppPasswordField>
              </div>
            </div>
            <div class="row q-col-gutter-sm">
              <div class="col-12 col-md-6"><AppTextField v-model="form.firstName" label="First name" required :v="v$.firstName" /></div>
              <div class="col-12 col-md-6"><AppTextField v-model="form.lastName" label="Last name" :v="v$.lastName" /></div>
            </div>
            <AppSelect v-model="form.roleId" label="Role" required :v="v$.roleId" :options="roleOptions" :loading="rolesLoading" />
            <q-toggle v-if="!isCreate" v-model="form.isActive" label="Active" color="primary" />
          </q-tab-panel>
        </q-tab-panels>

        <template v-if="isCreate">
          <q-separator />
          <q-card-actions class="q-pa-md">
            <div class="text-caption text-grey-7">Create the user — profile and role changes auto-save from then on.</div>
            <q-space />
            <q-btn unelevated color="primary" no-caps icon="o_check" label="Create user" :loading="creating" @click="create" />
          </q-card-actions>
        </template>
      </q-card>
    </template>

    <AppRecordMeta entity-type="user" :record-id="entity?.id" />
  </q-page>
</template>

<script setup>
/*
 * User create + manage page (full-page auto-save via useDetailForm). Create collects email + password
 * (email/password fixed afterwards). Manage auto-saves name / active / role. Role uses a separate
 * assignRoles call, so a thin api adapter unifies create vs update payloads for the composable.
 */
import { ref, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { getApiErrorMessage } from 'services/api'
import { userApi } from 'modules/users/api'
import { useNotify } from 'composables/useNotify'
import { useDetailForm } from 'composables/useDetailForm'
import { required, email, maxLength, strongPassword, passwordRules, matchRule } from 'validators'
import AppDetailHeader from 'components/common/AppDetailHeader.vue'
import AppTextField from 'components/common/AppTextField.vue'
import AppSelect from 'components/common/AppSelect.vue'

const route = useRoute()
const router = useRouter()
const notify = useNotify()

const tab = ref('general')
const isNew = computed(() => route.name === 'user-new')

const roleOptions = ref([])
const rolesLoading = ref(false)
async function loadRoles () {
  rolesLoading.value = true
  try {
    const list = await userApi.roles()
    const items = Array.isArray(list) ? list : list?.items || []
    roleOptions.value = items.map((r) => ({ label: r.name, value: r.id }))
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    rolesLoading.value = false
  }
}
loadRoles()

function buildPayload (f) {
  return {
    email: f.email,
    password: f.password,
    firstName: f.firstName,
    lastName: f.lastName,
    roleId: f.roleId,
    isActive: f.isActive
  }
}

// Adapter: create takes email/password/roleIds; update takes profile/status + a separate role assignment.
const detailApi = {
  get: (id) => userApi.get(id),
  create: (p) => userApi.create({
    email: p.email, password: p.password, firstName: p.firstName, lastName: p.lastName,
    roleIds: p.roleId ? [p.roleId] : []
  }),
  update: async (id, p) => {
    await userApi.update(id, { firstName: p.firstName, lastName: p.lastName, isActive: p.isActive })
    await userApi.assignRoles(id, p.roleId ? [p.roleId] : [])
    return userApi.get(id)
  }
}

const rules = computed(() => ({
  email: { required, email, maxLength: maxLength(256) },
  firstName: { required, maxLength: maxLength(200) },
  lastName: { maxLength: maxLength(200) },
  password: isNew.value ? { required, strongPassword } : {},
  roleId: { required }
}))

const {
  form, v$, entity, loading, creating, isCreate, saveStatus, create
} = useDetailForm({
  createRouteName: 'user-new',
  detailRouteName: 'user-detail',
  entityLabel: 'user',
  api: detailApi,
  buildPayload,
  empty: { email: '', firstName: '', lastName: '', password: '', roleId: null, isActive: true },
  rules,
  hydrateForm: (f, e) => {
    f.email = e.email || ''
    f.password = ''
    f.roleId = e.roles && e.roles.length ? e.roles[0].id : null
    f.isActive = e.isActive ?? true
    const parts = (e.fullName || '').trim().split(/\s+/).filter(Boolean)
    f.firstName = parts.shift() || ''
    f.lastName = parts.join(' ')
  }
})

// ---- Password management (manage mode only) --------------------------------
const sendingReset = ref(false)
const pwdDialog = ref(false)
const settingPwd = ref(false)
const newPwd = ref('')
const confirmPwd = ref('')

async function onSendReset () {
  if (!entity.value) return
  sendingReset.value = true
  try {
    await userApi.sendPasswordReset(entity.value.id)
    notify.success(`Password-reset link sent to ${entity.value.email}`)
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    sendingReset.value = false
  }
}

function openSetPassword () {
  newPwd.value = ''
  confirmPwd.value = ''
  pwdDialog.value = true
}

async function onSetPassword () {
  if (!entity.value) return
  if (newPwd.value !== confirmPwd.value) {
    notify.error('Passwords do not match')
    return
  }
  settingPwd.value = true
  try {
    await userApi.setPassword(entity.value.id, newPwd.value)
    notify.success('Password updated')
    pwdDialog.value = false
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    settingPwd.value = false
  }
}
</script>

<style scoped lang="scss">
.app-detail-tabs {
  :deep(.q-tab) { min-height: 44px; }
}
</style>
