<template>
  <div class="q-gutter-md tax-exemption">
    <q-inner-loading :showing="loading" />

    <template v-if="data">
      <!-- Status -->
      <q-card flat bordered>
        <q-card-section class="row items-center justify-between">
          <div class="text-subtitle1 text-weight-medium">Tax exemption</div>
          <q-chip :color="statusMeta.color" text-color="white" :icon="statusMeta.icon" :label="statusMeta.label" />
        </q-card-section>
        <q-separator />
        <q-card-section class="q-gutter-y-sm">
          <div class="text-body2 text-grey-8">{{ statusMeta.description }}</div>

          <div v-if="status === 'PendingReview' && latest" class="text-caption text-grey-6">
            Submitted {{ formatDate(latest.submittedOnUtc) }}
          </div>

          <q-banner v-if="status === 'Rejected' && latest && latest.adminNote" class="bg-red-1 text-red-9" rounded dense>
            <template #avatar><q-icon name="o_info" color="negative" /></template>
            {{ latest.adminNote }}
          </q-banner>

          <!-- Latest request details -->
          <template v-if="latest">
            <q-separator class="q-my-sm" />
            <div class="row q-col-gutter-md">
              <div v-if="latest.certificateNumber" class="col-12 col-sm-6">
                <div class="text-caption text-grey-6">Certificate number</div>
                <div class="text-body2">{{ latest.certificateNumber }}</div>
              </div>
              <div v-if="latest.vatId" class="col-12 col-sm-6">
                <div class="text-caption text-grey-6">VAT ID</div>
                <div class="text-body2">{{ latest.vatId }}</div>
              </div>
            </div>
            <div v-if="latest.documents && latest.documents.length" class="q-mt-sm">
              <div class="text-caption text-grey-6 q-mb-xs">Documents</div>
              <div class="column q-gutter-xs items-start">
                <a
                  v-for="doc in latest.documents"
                  :key="doc.id"
                  :href="$media(doc.url)"
                  target="_blank"
                  rel="noopener"
                  class="text-primary row items-center no-wrap"
                >
                  <q-icon name="o_description" size="18px" class="q-mr-xs" />
                  <span>{{ doc.fileName }}</span>
                </a>
              </div>
            </div>
          </template>
        </q-card-section>
      </q-card>

      <!-- Submit / re-submit -->
      <q-card v-if="canSubmit" flat bordered>
        <q-card-section class="text-subtitle1 text-weight-medium">
          {{ status === 'Rejected' ? 'Submit a new request' : 'Submit a request' }}
        </q-card-section>
        <q-separator />
        <q-form @submit.prevent="onSubmit">
          <q-card-section class="q-gutter-y-md">
            <div class="text-body2 text-grey-7 q-mb-md">
              Provide your tax certificate number or VAT ID and upload supporting documentation. At least one
              identifier and one document are required.
            </div>

            <div class="row q-col-gutter-md">
              <div class="col-12 col-sm-6">
                <AppTextField v-model="form.certificateNumber" label="Certificate number" />
              </div>
              <div class="col-12 col-sm-6">
                <AppTextField v-model="form.vatId" label="VAT ID" />
              </div>
            </div>

            <AppFileUpload
              v-model="documents"
              multiple
              variant="table"
              label="Supporting documents"
              required
              :upload-fn="uploadTaxDoc"
              accept=".pdf,.jpg,.jpeg,.png,.webp"
              extensions-label="PDF, JPG, PNG or WEBP"
              :single-max-mb="5"
              hint="PDF, JPG, PNG or WEBP · up to 5 MB per file"
            />
          </q-card-section>
          <q-card-actions align="right" class="q-px-md q-pb-md">
            <q-btn
              type="submit"
              color="primary"
              unelevated
              no-caps
              label="Submit request"
              :loading="submitting"
              :disable="!canSend"
            />
          </q-card-actions>
        </q-form>
      </q-card>
    </template>
  </div>
</template>

<script setup>
import { ref, reactive, computed, onMounted } from 'vue'
import { useQuasar } from 'quasar'
import { accountApi } from 'modules/storefront/account-api'
import { getApiErrorMessage } from 'services/api'
import { formatDateTime as formatDate } from 'src/utils/datetime'

const $q = useQuasar()

const loading = ref(false)
const submitting = ref(false)

const data = ref(null)
const form = reactive({ certificateNumber: '', vatId: '' })
const documents = ref([]) // [{ url, name, mediaId }] — bound to AppFileUpload (multiple + table)

const status = computed(() => (data.value && data.value.status) || 'NotSubmitted')
const latest = computed(() => (data.value && data.value.latestRequest) || null)
const canSubmit = computed(() => !!(data.value && data.value.canSubmit))
const hasCertOrVat = computed(() => !!(form.certificateNumber.trim() || form.vatId.trim()))
const canSend = computed(() => documents.value.length > 0 && hasCertOrVat.value)

const statusMeta = computed(() => {
  switch (status.value) {
    case 'PendingReview':
      return {
        color: 'orange',
        icon: 'o_hourglass_top',
        label: 'Pending review',
        description: "Your request is being reviewed. We'll email you once a decision is made."
      }
    case 'Approved':
      return {
        color: 'green',
        icon: 'o_verified',
        label: 'Approved',
        description: 'Approved — tax will not be charged at checkout.'
      }
    case 'Rejected':
      return {
        color: 'red',
        icon: 'o_cancel',
        label: 'Rejected',
        description: 'Your request was rejected. You can submit a new request below.'
      }
    default:
      return {
        color: 'grey',
        icon: 'o_receipt_long',
        label: 'Not submitted',
        description: 'You have not submitted a tax exemption request yet.'
      }
  }
})

async function load () {
  loading.value = true
  try {
    data.value = await accountApi.taxExemption()
  } catch (e) {
    $q.notify({ type: 'negative', message: getApiErrorMessage(e) })
  } finally {
    loading.value = false
  }
}

// Custom upload for AppFileUpload: pushes each file through the customer tax-document endpoint and returns
// the item stored in the model (carrying the mediaId used at submit time).
async function uploadTaxDoc (file) {
  const res = await accountApi.uploadTaxDocument(file)
  return { url: res.url, name: res.fileName, mediaId: res.mediaId }
}

async function onSubmit () {
  if (!hasCertOrVat.value) {
    $q.notify({ type: 'negative', message: 'Enter a certificate number or VAT ID.' })
    return
  }
  if (!documents.value.length) {
    $q.notify({ type: 'negative', message: 'Upload at least one supporting document.' })
    return
  }
  submitting.value = true
  try {
    await accountApi.submitTaxExemption({
      certificateNumber: form.certificateNumber.trim() || null,
      vatId: form.vatId.trim() || null,
      documentMediaIds: documents.value.map((d) => d.mediaId)
    })
    $q.notify({ type: 'positive', message: 'Tax exemption request submitted.' })
    form.certificateNumber = ''
    form.vatId = ''
    documents.value = []
    await load()
  } catch (e) {
    $q.notify({ type: 'negative', message: getApiErrorMessage(e) })
  } finally {
    submitting.value = false
  }
}

onMounted(load)
</script>

<style scoped>
.tax-exemption {
  position: relative;
  min-height: 160px;
}
</style>
