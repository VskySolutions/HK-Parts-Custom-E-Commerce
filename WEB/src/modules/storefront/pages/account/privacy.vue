<template>
  <div class="q-gutter-md privacy">
    <!-- Download your data -->
    <q-card flat bordered>
      <q-card-section class="text-subtitle1 text-weight-medium">Download your data</q-card-section>
      <q-separator />
      <q-card-section class="text-body2 text-grey-8">
        Get a copy of the personal data we hold about you — your profile, saved addresses and order
        history — as a JSON file you can keep or take elsewhere.
      </q-card-section>
      <q-card-actions align="right" class="q-px-md q-pb-md">
        <q-btn
          color="primary"
          unelevated
          no-caps
          icon="o_download"
          label="Download my data"
          :loading="exporting"
          @click="onExport"
        />
      </q-card-actions>
    </q-card>

    <!-- Danger zone: delete account -->
    <q-card flat bordered class="danger-zone">
      <q-card-section class="row items-center q-gutter-sm">
        <q-icon name="o_warning" color="negative" size="22px" />
        <div class="text-subtitle1 text-weight-medium text-negative">Delete my account</div>
      </q-card-section>
      <q-separator />
      <q-card-section class="q-gutter-y-sm text-body2 text-grey-8">
        <div>
          Deleting your account is permanent and cannot be undone. Your personal details — name, email,
          phone number and saved addresses — will be anonymised, and you'll be signed out immediately.
        </div>
        <div>
          For legal and accounting reasons your past orders are kept, but they'll no longer be linked to
          your personal information.
        </div>
      </q-card-section>
      <q-card-actions align="right" class="q-px-md q-pb-md">
        <q-btn
          color="negative"
          outline
          no-caps
          icon="o_delete_forever"
          label="Delete my account"
          :loading="deleting"
          @click="confirmDelete"
        />
      </q-card-actions>
    </q-card>
  </div>
</template>

<script setup>
import { ref } from 'vue'
import { useQuasar } from 'quasar'
import { useRouter } from 'vue-router'
import { accountApi } from 'modules/storefront/account-api'
import { useCustomerAuthStore } from 'stores/customerAuth'
import { getApiErrorMessage } from 'services/api'

const $q = useQuasar()
const router = useRouter()
const auth = useCustomerAuthStore()

const exporting = ref(false)
const deleting = ref(false)

async function onExport () {
  exporting.value = true
  try {
    await accountApi.exportData()
    $q.notify({ type: 'positive', message: 'Your data export has been downloaded.' })
  } catch (e) {
    $q.notify({ type: 'negative', message: getApiErrorMessage(e) })
  } finally {
    exporting.value = false
  }
}

// Type-to-confirm — the OK button stays disabled until the customer types DELETE.
function confirmDelete () {
  $q.dialog({
    title: 'Delete account',
    message: 'This permanently anonymises your personal data and signs you out. Type DELETE to confirm.',
    prompt: { model: '', type: 'text', isValid: (v) => (v || '').trim().toUpperCase() === 'DELETE' },
    cancel: true,
    persistent: true,
    ok: { label: 'Delete my account', color: 'negative', unelevated: true, noCaps: true }
  }).onOk(async () => {
    deleting.value = true
    try {
      await accountApi.deleteAccount()
      auth.clearSession() // wipe the local session; the account no longer exists server-side
      $q.notify({ type: 'positive', message: 'Your account has been deleted.' })
      router.push({ name: 'shop-home' })
    } catch (e) {
      $q.notify({ type: 'negative', message: getApiErrorMessage(e) })
    } finally {
      deleting.value = false
    }
  })
}
</script>

<style scoped lang="scss">
.danger-zone {
  border-color: rgba(193, 0, 21, 0.35);
}
</style>
