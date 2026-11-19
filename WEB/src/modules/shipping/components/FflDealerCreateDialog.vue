<template>
  <!-- Add a dealer by hand: for a dealer the buyer needs that is not in the last ATF import yet. The FFL number
       becomes the dealer's key, so a later import of the same licence confirms it instead of duplicating it. -->
  <q-dialog :model-value="modelValue" persistent @update:model-value="$emit('update:modelValue', $event)">
    <q-card style="width: 620px; max-width: 95vw">
      <q-card-section class="text-h6">Add dealer</q-card-section>
      <q-separator />
      <q-form ref="formRef" greedy @submit.prevent="save">
        <q-card-section class="column q-gutter-sm">
          <q-banner dense class="bg-orange-1 text-orange-10 rounded-borders">
            <template #avatar><q-icon name="o_verified_user" color="orange-10" /></template>
            Check the licence on
            <a :href="FFL_EZ_CHECK_URL" target="_blank" rel="noopener noreferrer" class="text-primary">ATF FFL eZ Check</a>
            before adding it. The dealer is marked "Manual" until an ATF import includes it.
          </q-banner>

          <AppTextField
            v-model="form.licenseNumber"
            label="FFL Number"
            required
            placeholder="1-57-011-01-6K-12345"
            hint="Region-District-County-Type-Expiry-Sequence, as printed on the licence"
            lazy-rules
            :rules="[(v) => licenseError(v)]"
          />
          <div class="row q-col-gutter-md">
            <div class="col-12 col-sm-6">
              <AppTextField v-model="form.licenseName" label="Licence Name" required maxlength="255" lazy-rules :rules="[req('Dealer Name required')]" />
            </div>
            <div class="col-12 col-sm-6">
              <AppTextField v-model="form.businessName" label="Business Name" maxlength="255" hint="Shown to buyers when set" />
            </div>
          </div>
          <AppTextField v-model="form.street" label="Premise Street" required maxlength="200" lazy-rules :rules="[req('Dealer Shipping Address required')]" />
          <div class="row q-col-gutter-md">
            <div class="col-12 col-sm-5">
              <AppTextField v-model="form.city" label="City" required maxlength="100" lazy-rules :rules="[req('Dealer Shipping City required')]" />
            </div>
            <div class="col-6 col-sm-3">
              <AppSelect v-model="form.state" label="State" required :options="stateOptions" :rules="[req('Dealer Shipping State required')]" />
            </div>
            <div class="col-6 col-sm-4">
              <AppTextField
                v-model="form.zip"
                label="ZIP Code"
                required
                maxlength="10"
                inputmode="numeric"
                lazy-rules
                :rules="[(v) => /^\d{5}(-?\d{4})?$/.test((v || '').trim()) || 'Please enter a valid 5-digit ZIP code.']"
              />
            </div>
          </div>
          <div class="row q-col-gutter-md">
            <div class="col-12 col-sm-6">
              <AppTextField
                v-model="form.phone"
                label="Phone"
                required
                inputmode="tel"
                maxlength="30"
                lazy-rules
                :rules="[(v) => ((v || '').replace(/\D/g, '').length >= 10) || 'Enter a valid phone number.']"
              />
            </div>
            <div class="col-6 col-sm-3">
              <AppSelect v-model="form.allowsTransfers" label="Allows Transfers?" :options="yesNo" />
            </div>
            <div class="col-6 col-sm-3">
              <AppTextField
                v-model="form.fee"
                label="Transfer Fee"
                type="number"
                min="0"
                step="0.01"
                prefix="$"
                :rules="[(v) => v === '' || v == null || Number(v) >= 0 || 'Enter 0 or more.']"
              />
            </div>
          </div>
        </q-card-section>
        <q-separator />
        <q-card-actions align="right">
          <q-btn flat no-caps label="Cancel" :disable="saving" @click="$emit('update:modelValue', false)" />
          <q-btn unelevated no-caps color="primary" type="submit" label="Add dealer" :loading="saving" />
        </q-card-actions>
      </q-form>
    </q-card>
  </q-dialog>
</template>

<script setup>
import { ref, watch } from 'vue'
import { fflAdminApi, FFL_EZ_CHECK_URL } from 'modules/shipping/ffl-api'
import { getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'

const props = defineProps({
  modelValue: { type: Boolean, default: false },
  stateOptions: { type: Array, default: () => [] }
})
const emit = defineEmits(['update:modelValue', 'saved'])
const notify = useNotify()

const yesNo = [{ label: 'Yes', value: true }, { label: 'No', value: false }]
const TRANSFER_TYPES = ['01', '02', '07', '08', '09', '10', '11']

const formRef = ref(null)
const saving = ref(false)
const form = ref(blank())

function blank () {
  return { licenseNumber: '', licenseName: '', businessName: '', street: '', city: '', state: null, zip: '', phone: '', allowsTransfers: true, fee: '' }
}

watch(() => props.modelValue, (open) => { if (open) form.value = blank() })

const req = (msg) => (v) => !!(v || '').toString().trim() || msg

// Mirrors the server: R-DD-CCC-TT-XX-SSSSS (dashes optional), expiry month letter A–M with no I, a transfer type.
function licenseError (v) {
  const m = (v || '').trim().replace(/\s/g, '').match(/^(\d)-?(\d{2})-?(\d{3})-?(\d{2})-?(\d[A-HJ-M])-?(\d{5})$/i)
  if (!m) return 'Enter the full FFL number as R-DD-CCC-TT-XX-SSSSS.'
  if (!TRANSFER_TYPES.includes(m[4])) return `Licence type ${m[4]} can't receive transfers.`
  return true
}

async function save () {
  if (!(await formRef.value?.validate())) return
  saving.value = true
  try {
    const f = form.value
    const dealer = await fflAdminApi.createDealer({
      licenseNumber: f.licenseNumber.trim(),
      licenseName: f.licenseName.trim(),
      businessName: f.businessName.trim() || null,
      street: f.street.trim(),
      city: f.city.trim(),
      state: f.state,
      zip: f.zip.trim(),
      phone: f.phone.trim(),
      allowsTransfers: f.allowsTransfers,
      lastTransferFee: f.fee === '' || f.fee == null ? null : Number(f.fee)
    })
    notify.success(dealer.hasGeocode
      ? 'Dealer added. Buyers can find it at checkout now.'
      : 'Dealer added, but its ZIP could not be located yet. Run "Geocode missing" so it shows in searches.')
    emit('saved', dealer)
    emit('update:modelValue', false)
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    saving.value = false
  }
}
</script>
