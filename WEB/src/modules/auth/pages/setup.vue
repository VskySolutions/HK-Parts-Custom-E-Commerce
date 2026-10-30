<template>
  <q-card flat bordered class="setup-card q-pa-md">
    <q-card-section class="text-center q-pb-none">
      <q-icon name="o_settings_suggest" size="42px" color="primary" />
      <div class="text-h5 q-mt-sm">First-time Setup</div>
      <div class="text-subtitle2 text-grey-7">
        Create the super administrator before the first sign-in.
      </div>
    </q-card-section>

    <q-card-section>
      <q-banner v-if="statusLoading" class="bg-grey-2">
        <template #avatar><q-spinner color="primary" /></template>
        Checking setup status…
      </q-banner>

      <q-banner v-else-if="status.setupCompleted" class="bg-green-1 text-green-9 rounded-borders">
        Setup already completed. You can sign in.
      </q-banner>

      <q-form v-else @submit.prevent="onComplete" class="q-gutter-md q-mt-sm">
        <q-input v-model="form.fullName" label="Full name" outlined dense
          :rules="[(v) => !!v || 'Required']" />
        <q-input v-model="form.email" type="email" label="Email" outlined dense
          :rules="[(v) => !!v || 'Required']" />
        <AppPasswordField v-model="form.password" label="Password" strength
          :rules="passwordRules()" />
        <q-btn type="submit" color="primary" unelevated class="full-width"
          label="Complete setup" :loading="submitting" />
      </q-form>
    </q-card-section>

    <q-card-actions class="q-px-md q-pb-sm">
      <q-btn flat color="grey-8" label="Go to login" to="/auth/login" />
      <q-space />
      <q-btn flat color="primary" icon="o_refresh" label="Refresh"
        :loading="statusLoading" @click="loadStatus" />
    </q-card-actions>
  </q-card>
</template>

<script setup>
/*
 * First-time setup (WO-94 Step 12 / backend contract).
 * GET /api/setup/status -> { setupCompleted, superAdminExists }
 * POST /api/setup/complete -> creates the super admin.
 */
import { ref, reactive, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { anonApi, getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'
import { passwordRules } from 'validators'

const router = useRouter()
const notify = useNotify()

const statusLoading = ref(false)
const submitting = ref(false)
const status = ref({ setupCompleted: false, superAdminExists: false })
const form = reactive({ fullName: '', email: '', password: '' })

async function loadStatus () {
  statusLoading.value = true
  try {
    const { data } = await anonApi.get('/api/setup/status')
    status.value = data || status.value
  } catch (err) {
    notify.error('Unable to load setup status')
  } finally {
    statusLoading.value = false
  }
}

async function onComplete () {
  submitting.value = true
  try {
    await anonApi.post('/api/setup/complete', { ...form })
    notify.success('Setup completed. Please sign in.')
    router.push('/auth/login')
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    submitting.value = false
  }
}

onMounted(loadStatus)
</script>

<style scoped>
.setup-card {
  width: 460px;
  max-width: 92vw;
}
</style>
