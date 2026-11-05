<template>
  <q-page class="app-page">
    <AppDetailHeader
      title="Create Gift Certificate"
      :breadcrumbs="[
        { label: 'Home', icon: 'o_home', to: '/dashboard' },
        { label: 'Gift Certificates', to: { name: 'gift-certificates' } },
        { label: 'New' }
      ]"
      show-back
      @back="router.push({ name: 'gift-certificates' })"
    />

    <!-- After save: show the generated code -->
    <q-card v-if="created" flat bordered class="app-section q-pa-lg text-center">
      <q-icon name="o_redeem" size="48px" color="positive" />
      <div class="text-h6 q-mt-sm">Gift Certificate created successfully</div>
      <div class="text-caption text-grey-7 q-mt-md">Code</div>
      <div class="row justify-center items-center q-gutter-sm">
        <div class="text-h5 text-weight-bold" style="font-family: monospace; letter-spacing: 2px">{{ created.code }}</div>
        <q-btn flat round dense icon="o_content_copy" @click="copyText(created.code, notify)"><q-tooltip>Copy code</q-tooltip></q-btn>
      </div>
      <div class="q-mt-sm">{{ money(created.originalBalance) }} · {{ created.status }}</div>
      <div v-if="form.sendEmail && created.recipientEmail" class="text-caption text-grey-7 q-mt-xs">An email is on its way to {{ created.recipientEmail }}.</div>
      <div class="row justify-center q-gutter-sm q-mt-lg">
        <q-btn outline no-caps color="primary" label="Open certificate" @click="router.push({ name: 'gift-certificate-detail', params: { id: created.id } })" />
        <q-btn unelevated no-caps color="primary" label="Create another" @click="reset" />
      </div>
    </q-card>

    <q-form v-else @submit.prevent="save">
      <q-card flat bordered class="app-section">
        <q-card-section class="q-gutter-y-xs">
          <div class="row q-col-gutter-md">
            <div class="col-12 col-md-6">
              <AppTextField v-model.number="form.amount" label="Amount" required type="number" min="0.01" max="10000" step="0.01" :v="v$.amount">
                <template #prepend><span class="text-grey-7">$</span></template>
              </AppTextField>
            </div>
            <div class="col-12 col-md-6">
              <AppSelect v-model="form.status" label="Status" :options="statusOptions" emit-value map-options />
            </div>
          </div>
          <div class="row q-col-gutter-md">
            <div class="col-12 col-md-6"><AppTextField v-model="form.recipientName" label="Recipient Name" maxlength="75" :v="v$.recipientName" /></div>
            <div class="col-12 col-md-6"><AppTextField v-model="form.recipientEmail" label="Recipient Email" type="email" maxlength="75" :v="v$.recipientEmail" /></div>
          </div>
          <q-checkbox v-if="form.recipientEmail" v-model="form.sendEmail" label="Send email to recipient" />
          <AppTextField v-model="form.message" label="Message" type="textarea" autogrow maxlength="1000" counter :v="v$.message" />
          <div class="row q-col-gutter-md">
            <div class="col-12 col-md-6"><AppDateField v-model="form.expiresAt" label="Expires At" hint="Empty = never expires" /></div>
            <div class="col-12 col-md-6"><AppTextField v-model="form.comment" label="Comments" maxlength="255" counter hint="Saved on the ledger entry" :v="v$.comment" /></div>
          </div>
        </q-card-section>
        <q-separator />
        <q-card-actions class="q-pa-md">
          <q-btn flat no-caps label="Cancel" @click="router.push({ name: 'gift-certificates' })" />
          <q-space />
          <q-btn unelevated no-caps color="primary" icon="o_check" label="Create gift certificate" type="submit" :loading="saving" />
        </q-card-actions>
      </q-card>
    </q-form>
  </q-page>
</template>

<script setup>
/* Create an admin gift certificate. The server generates the code; it is shown (with copy) after save. */
import { ref, reactive } from 'vue'
import { useRouter } from 'vue-router'
import useVuelidate from '@vuelidate/core'
import { required, maxLength, helpers, email } from 'validators'
import { getApiErrorMessage } from 'services/api'
import { giftCertificateApi, money, copyText } from 'modules/gift-certificates/api'
import { useNotify } from 'composables/useNotify'
import AppDetailHeader from 'components/common/AppDetailHeader.vue'
import AppDateField from 'components/common/AppDateField.vue'

const router = useRouter()
const notify = useNotify()
const statusOptions = [{ label: 'Active', value: 'Active' }, { label: 'Inactive', value: 'Inactive' }]

const empty = () => ({ amount: null, status: 'Active', recipientName: '', recipientEmail: '', sendEmail: true, message: '', expiresAt: '', comment: '' })
const form = reactive(empty())
const saving = ref(false)
const created = ref(null)

const amountRule = helpers.withMessage('Enter an amount over 0, up to 10,000, with at most 2 decimals',
  (v) => v === null || v === '' || (Number(v) > 0 && Number(v) <= 10000 && Math.round(Number(v) * 100) === Number(v) * 100))
const $externalResults = reactive({})
const v$ = useVuelidate({
  amount: { required: helpers.withMessage('Amount is required', required), amountRule },
  recipientName: { maxLength: maxLength(75) },
  recipientEmail: { email, maxLength: maxLength(75) },
  message: { maxLength: maxLength(1000) },
  comment: { maxLength: maxLength(255) }
}, form, { $externalResults })

async function save () {
  v$.value.$clearExternalResults()
  if (!(await v$.value.$validate())) return
  saving.value = true
  try {
    created.value = await giftCertificateApi.create({
      amount: Number(form.amount),
      status: form.status,
      recipientName: form.recipientName || null,
      recipientEmail: form.recipientEmail || null,
      message: form.message || null,
      expiresAt: form.expiresAt ? `${form.expiresAt}T23:59:59Z` : null,
      sendEmail: !!form.recipientEmail && form.sendEmail,
      comment: form.comment || null
    })
    notify.success('Gift Certificate created successfully')
  } catch (err) {
    const errors = err?.response?.data?.errors
    if (err?.response?.status === 400 && errors && !Array.isArray(errors)) {
      for (const [k, m] of Object.entries(errors)) {
        const field = k.charAt(0).toLowerCase() + k.slice(1)
        if (field in form) $externalResults[field] = m
      }
    }
    notify.error(getApiErrorMessage(err))
  } finally {
    saving.value = false
  }
}

function reset () {
  Object.assign(form, empty())
  created.value = null
  v$.value.$reset()
}
</script>
