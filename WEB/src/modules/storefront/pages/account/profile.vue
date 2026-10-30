<template>
  <div class="q-gutter-md">
    <q-inner-loading :showing="loading" />

    <!-- Personal details -->
    <q-card flat bordered>
      <q-card-section class="row items-center justify-between">
        <div class="text-subtitle1 text-weight-medium">Personal details</div>
        <q-badge v-if="profile && !profile.emailVerified" color="orange" label="Email not verified" />
        <q-badge v-else-if="profile" color="green" label="Verified" />
      </q-card-section>
      <q-separator />
      <q-form @submit.prevent="onSaveProfile">
        <q-card-section class="q-gutter-md">
          <div>
            <div class="row q-col-gutter-md">
              <div class="col-12 col-sm-6">
                <AppTextField v-model="form.firstName" label="First name" required :rules="[(v) => !!v || 'Required']" />
              </div>
              <div class="col-12 col-sm-6">
                <AppTextField v-model="form.lastName" label="Last name" required :rules="[(v) => !!v || 'Required']" />
              </div>
            </div>
          </div>
          <AppPhoneInput
            label="Phone (optional)"
            :model-value="form.phoneNumber"
            @update:model-value="form.phoneNumber = $event"
          />
          <AppSelect
            v-model="form.preferredTimeZone"
            label="Timezone"
            :options="tzOptions"
            use-input
            hide-selected
            fill-input
            input-debounce="0"
            @filter="filterTz"
            hint="Dates & times across the store show in this zone."
          />
        </q-card-section>
        <q-card-actions align="right" class="q-px-md q-pb-md">
          <q-btn type="submit" color="primary" no-caps unelevated label="Save changes" :loading="savingProfile" />
        </q-card-actions>
      </q-form>
    </q-card>

    <!-- Email -->
    <q-card flat bordered>
      <q-card-section class="text-subtitle1 text-weight-medium">Email address</q-card-section>
      <q-separator />
      <q-form @submit.prevent="onChangeEmail">
        <q-card-section class="q-gutter-md">
          <AppTextField :model-value="profile ? profile.email : ''" label="Current email" readonly />
          <div>
            <AppTextField
              v-model="newEmail"
              label="New email"
              type="email"
              :rules="[(v) => !!v || 'Enter a new email']"
            />
            <div class="text-caption text-grey-6 q-mt-xs">
              You'll need to verify the new address before it takes effect.
            </div>
          </div>
        </q-card-section>
        <q-card-actions align="right" class="q-px-md q-pb-md">
          <q-btn type="submit" color="primary" outline no-caps label="Update email" :loading="savingEmail" />
        </q-card-actions>
      </q-form>
    </q-card>

    <!-- Password -->
    <q-card flat bordered>
      <q-card-section class="text-subtitle1 text-weight-medium">Password</q-card-section>
      <q-separator />
      <q-form @submit.prevent="onChangePassword">
        <q-card-section class="q-gutter-md">
          <AppPasswordField v-model="pw.currentPassword" label="Current password" autocomplete="current-password" :rules="[(v) => !!v || 'Required']" />
          <div>
            <div class="row q-col-gutter-md">
              <div class="col-12 col-sm-6">
                <AppPasswordField v-model="pw.newPassword" label="New password" strength :rules="passwordRules()" />
              </div>
              <div class="col-12 col-sm-6">
                <AppPasswordField v-model="pw.confirm" label="Confirm new password" :rules="[matchRule(() => pw.newPassword)]" />
              </div>
            </div>
          </div>
        </q-card-section>
        <q-card-actions align="right" class="q-px-md q-pb-md">
          <q-btn type="submit" color="primary" outline no-caps label="Change password" :loading="savingPassword" />
        </q-card-actions>
      </q-form>
    </q-card>
  </div>
</template>

<script setup>
import { ref, reactive, onMounted } from 'vue'
import { useQuasar } from 'quasar'
import { accountApi } from 'modules/storefront/account-api'
import { useCustomerAuthStore } from 'stores/customerAuth'
import { useStorefront } from 'modules/storefront/composables/useStorefront'
import { getApiErrorMessage } from 'services/api'
import { passwordRules, matchRule } from 'validators'
import { timeZoneOptions, setActiveTimeZone } from 'src/utils/datetime'
import AppPhoneInput from 'components/common/AppPhoneInput.vue'
import AppSelect from 'components/common/AppSelect.vue'

const $q = useQuasar()
const auth = useCustomerAuthStore()
const { branding } = useStorefront()

const loading = ref(false)
const savingProfile = ref(false)
const savingEmail = ref(false)
const savingPassword = ref(false)

const profile = ref(null)
const form = reactive({ firstName: '', lastName: '', phoneNumber: '', preferredTimeZone: '' })
const newEmail = ref('')
const pw = reactive({ currentPassword: '', newPassword: '', confirm: '' })

// Timezone picker: "Store default" plus all IANA zones (searchable).
const TZ_BASE = [{ label: 'Store default', value: '' }, ...timeZoneOptions()]
const tzOptions = ref(TZ_BASE)
function filterTz (needle, update) {
  const q = (needle || '').toLowerCase().trim()
  update(() => {
    tzOptions.value = !q ? TZ_BASE : TZ_BASE.filter((o) => o.label.toLowerCase().includes(q))
  })
}

// Apply the effective zone app-wide: the customer's choice, else the tenant default.
function applyTimeZone (pref) {
  setActiveTimeZone(pref || branding.value.displayTimeZone || 'UTC')
}

async function load () {
  loading.value = true
  try {
    const p = await accountApi.getProfile()
    profile.value = p
    form.firstName = p.firstName || ''
    form.lastName = p.lastName || ''
    form.phoneNumber = p.phoneNumber || ''
    form.preferredTimeZone = p.preferredTimeZone || ''
    auth.setProfile({ firstName: p.firstName, lastName: p.lastName, email: p.email })
  } catch (e) {
    $q.notify({ type: 'negative', message: getApiErrorMessage(e) })
  } finally {
    loading.value = false
  }
}

async function onSaveProfile () {
  savingProfile.value = true
  try {
    const p = await accountApi.updateProfile({
      firstName: form.firstName.trim(),
      lastName: form.lastName.trim(),
      phoneNumber: form.phoneNumber.trim() || null,
      preferredTimeZone: form.preferredTimeZone || null
    })
    profile.value = p
    auth.setProfile({ firstName: p.firstName, lastName: p.lastName })
    applyTimeZone(p.preferredTimeZone) // reflect the new zone immediately, app-wide
    $q.notify({ type: 'positive', message: 'Profile updated.' })
  } catch (e) {
    $q.notify({ type: 'negative', message: getApiErrorMessage(e) })
  } finally {
    savingProfile.value = false
  }
}

async function onChangeEmail () {
  savingEmail.value = true
  try {
    await accountApi.changeEmail(newEmail.value.trim())
    newEmail.value = ''
    $q.notify({ type: 'positive', message: 'Verification email sent to your new address.' })
    await load()
  } catch (e) {
    $q.notify({ type: 'negative', message: getApiErrorMessage(e) })
  } finally {
    savingEmail.value = false
  }
}

async function onChangePassword () {
  savingPassword.value = true
  try {
    await accountApi.changePassword({ currentPassword: pw.currentPassword, newPassword: pw.newPassword })
    pw.currentPassword = ''
    pw.newPassword = ''
    pw.confirm = ''
    $q.notify({ type: 'positive', message: 'Password changed.' })
  } catch (e) {
    $q.notify({ type: 'negative', message: getApiErrorMessage(e) })
  } finally {
    savingPassword.value = false
  }
}

onMounted(load)
</script>
