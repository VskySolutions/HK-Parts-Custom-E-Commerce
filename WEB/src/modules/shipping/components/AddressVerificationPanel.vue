<template>
  <div class="relative-position">
    <q-inner-loading :showing="loading" />

    <div class="row q-col-gutter-md">
      <!-- ============ LEFT: settings ============ -->
      <div class="col-12 col-md-7">
        <AppSection title="Address verification">
          <div class="text-caption text-grey-7 q-mb-sm">
            Checks the delivery address at checkout, before shipping is calculated, and again before payment.
            Pickup orders and FFL dealers chosen from the directory are not checked.
          </div>

          <AppSelect
            v-model="form.provider"
            label="Address verification"
            :options="verificationProviderOptions"
            emit-value map-options
            :disable="!canWrite"
          />
          <div v-if="form.provider !== 'None'" class="q-mt-xs">
            <q-chip
              dense square
              :icon="providerConfigured ? 'o_check_circle' : 'o_error_outline'"
              :color="providerConfigured ? 'green-1' : 'orange-1'"
              :text-color="providerConfigured ? 'positive' : 'warning'"
            >
              {{ providerConfigured ? 'Credentials configured' : 'No active credentials — every address will count as “not verified”' }}
            </q-chip>
            <router-link :to="{ path: '/integrations', query: { section: form.provider === 'Usps' ? 'usps' : 'avalara' } }" class="text-caption q-ml-sm">
              {{ form.provider === 'Usps' ? 'USPS credentials' : 'Avalara credentials' }}
            </router-link>
          </div>

          <AppTextField
            v-model="form.failedMessage"
            class="q-mt-md"
            label="Failed validation message"
            :required="form.provider !== 'None'"
            maxlength="255"
            counter
            placeholder="We could not verify your shipping address. Please check it and try again."
            :disable="!canWrite"
          />
          <div class="text-caption text-grey-7">Shown to the customer, with the provider's reason underneath.</div>

          <q-select
            v-model="form.countries"
            class="q-mt-md"
            label="Countries to verify"
            outlined dense multiple use-chips use-input new-value-mode="add-unique" hide-dropdown-icon
            input-debounce="0"
            :disable="!canWrite"
            :hint="form.provider === 'Usps' ? 'USPS checks US addresses only.' : 'Two-letter codes, e.g. US. Type a code and press Enter.'"
            @new-value="(v, done) => done(String(v).trim().toUpperCase(), 'add-unique')"
          />

          <q-toggle v-model="form.offerSuggestion" class="q-mt-md" label="Offer the corrected address (“We suggest: …”)" color="primary" :disable="!canWrite" />
          <div class="text-caption text-grey-7 q-mb-sm">
            The customer chooses Use suggested or Keep mine. Off: a corrected address is simply accepted.
          </div>

          <AppSelect
            v-model="form.onUnavailable"
            label="When the provider is down"
            :options="verificationUnavailableOptions"
            emit-value map-options
            :disable="!canWrite"
          />
          <div class="text-caption text-grey-7">Flagged orders show “address not verified” on the order and in ShipStation.</div>

          <q-toggle v-model="form.loggingEnabled" class="q-mt-md" label="Log every verification call" color="primary" :disable="!canWrite" />
        </AppSection>

        <AppSection title="P.O. boxes" class="q-mt-md">
          <q-toggle v-model="form.blockPoBox" label="Prevent shipping to P.O. boxes" color="primary" :disable="!canWrite" />
          <div class="text-caption text-grey-7 q-mb-sm">
            Applies even when address verification is disabled. Catches PO Box, P.O. Box, Post Office Box, POB 12,
            and “Box 12” on its own line.
          </div>
          <AppTextField
            v-model="form.poBoxMessage"
            label="P.O. box error message"
            :required="form.blockPoBox"
            placeholder="We cannot ship to a P.O. box. Please enter a street address."
            :disable="!canWrite || !form.blockPoBox"
          />
        </AppSection>

        <div v-if="canWrite" class="row justify-end q-mt-md">
          <q-btn unelevated no-caps color="primary" icon="o_save" label="Save" :loading="saving" @click="save" />
        </div>
      </div>

      <!-- ============ RIGHT: test box ============ -->
      <div class="col-12 col-md-5">
        <AppSection title="Test an address">
          <div class="text-caption text-grey-7 q-mb-sm">
            Runs the provider against the address below and shows its raw answer. Nothing is saved.
          </div>
          <AppSelect
            v-model="test.provider"
            label="Provider"
            :options="verificationProviderOptions.filter((o) => o.value !== 'None')"
            emit-value map-options
          />
          <AppTextField v-model="test.line1" class="q-mt-sm" label="Address line 1" placeholder="3334 Gray Moss Rd" />
          <AppTextField v-model="test.line2" class="q-mt-sm" label="Address line 2" />
          <div class="row q-col-gutter-sm q-mt-none">
            <div class="col-12 col-sm-6"><AppTextField v-model="test.city" label="City" placeholder="Charlotte" /></div>
            <div class="col-6 col-sm-3"><AppTextField v-model="test.region" label="State" placeholder="NC" /></div>
            <div class="col-6 col-sm-3"><AppTextField v-model="test.postalCode" label="ZIP" placeholder="28270" /></div>
          </div>
          <AppTextField v-model="test.countryCode" class="q-mt-sm" label="Country" placeholder="US" maxlength="2" />
          <div class="row justify-end q-mt-sm">
            <q-btn outline no-caps color="primary" icon="o_fact_check" label="Test address" :loading="testing" :disable="!test.line1 || !test.countryCode" @click="runTest" />
          </div>

          <div v-if="testResult" class="q-mt-md">
            <div class="row items-center q-gutter-sm">
              <q-badge :color="statusColor(testResult.status)" class="text-body2 q-px-sm">{{ testResult.status }}</q-badge>
              <span class="text-caption text-grey-7">{{ testResult.durationMs }} ms</span>
              <q-badge v-if="testResult.isPoBox" color="warning" text-color="dark">P.O. box</q-badge>
              <q-badge v-if="testResult.isResidential === true" outline color="grey-8">Residential</q-badge>
              <q-badge v-if="testResult.isResidential === false" outline color="grey-8">Commercial</q-badge>
            </div>
            <div v-if="testResult.suggestion" class="q-mt-sm">
              <div class="text-caption text-grey-7">Standardised address</div>
              <div>{{ formatAddress(testResult.suggestion) }}</div>
            </div>
            <ul v-if="testResult.messages?.length" class="q-mt-sm q-mb-none q-pl-md text-caption">
              <li v-for="(m, i) in testResult.messages" :key="i">{{ m }}</li>
            </ul>
            <q-expansion-item v-if="testResult.rawResponse" dense dense-toggle label="Raw response" class="q-mt-sm">
              <pre class="raw-response">{{ prettyRaw }}</pre>
            </q-expansion-item>
          </div>
        </AppSection>
      </div>
    </div>
  </div>
</template>

<script setup>
/* Address Verification (docs/shipstation-verification-requirements.md §5.2): provider, failure message,
 * countries, suggestion, provider-down policy, logging and the P.O. box rule; plus the "Test address" box
 * (AD-61). Saved with the Save button so the server's cross-field checks (countries required, USPS = US only)
 * come back as one message rather than on every keystroke. */
import { ref, reactive, computed, onMounted } from 'vue'
import { getApiErrorMessage } from 'services/api'
import { usePermissions } from 'composables/usePermissions'
import { useNotify } from 'composables/useNotify'
import { addressVerificationApi, verificationProviderOptions, verificationUnavailableOptions } from 'modules/shipping/api'
import AppSection from 'components/common/AppSection.vue'
import AppTextField from 'components/common/AppTextField.vue'
import AppSelect from 'components/common/AppSelect.vue'

const notify = useNotify()
const { has } = usePermissions()
const canWrite = computed(() => has('Stores.Write'))

defineProps({ search: { type: String, default: '' } })
const emit = defineEmits(['save-status'])
function noop () {}
defineExpose({ onAdd: noop, openFilters: noop })

const loading = ref(false)
const saving = ref(false)
const configured = ref({})

const form = reactive({
  provider: 'None',
  failedMessage: '',
  countries: ['US'],
  offerSuggestion: true,
  onUnavailable: 'AllowAndFlag',
  loggingEnabled: false,
  blockPoBox: false,
  poBoxMessage: ''
})

const providerConfigured = computed(() => !!configured.value[form.provider])

const test = reactive({ provider: 'Usps', line1: '', line2: '', city: '', region: '', postalCode: '', countryCode: 'US' })
const testing = ref(false)
const testResult = ref(null)

const prettyRaw = computed(() => {
  const raw = testResult.value?.rawResponse
  if (!raw) return ''
  try { return JSON.stringify(JSON.parse(raw), null, 2) } catch { return raw }
})

function statusColor (s) {
  return { Valid: 'positive', Suggested: 'primary', Invalid: 'negative', Unavailable: 'warning', PoBox: 'warning' }[s] || 'grey-7'
}

function formatAddress (a) {
  return [a.line1, a.line2, a.city, [a.region, a.postalCode].filter(Boolean).join(' '), a.countryCode].filter(Boolean).join(', ')
}

function apply (d) {
  Object.assign(form, {
    provider: d.provider || 'None',
    failedMessage: d.failedMessage || '',
    countries: d.countries?.length ? d.countries : (d.provider === 'None' ? ['US'] : []),
    offerSuggestion: d.offerSuggestion,
    onUnavailable: d.onUnavailable || 'AllowAndFlag',
    loggingEnabled: d.loggingEnabled,
    blockPoBox: d.blockPoBox,
    poBoxMessage: d.poBoxMessage || ''
  })
  configured.value = d.providerConfigured || {}
  if (d.provider && d.provider !== 'None') test.provider = d.provider
}

async function load () {
  loading.value = true
  try {
    apply(await addressVerificationApi.get())
  } catch (err) { notify.error(getApiErrorMessage(err)) } finally { loading.value = false }
}

async function save () {
  saving.value = true
  try {
    apply(await addressVerificationApi.update({
      ...form,
      failedMessage: form.failedMessage || null,
      poBoxMessage: form.poBoxMessage || null
    }))
    notify.success('Address verification settings updated successfully')
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    saving.value = false
  }
}

async function runTest () {
  testing.value = true
  testResult.value = null
  try {
    testResult.value = await addressVerificationApi.test({
      provider: test.provider,
      address: {
        line1: test.line1, line2: test.line2 || null, city: test.city, region: test.region || null,
        postalCode: test.postalCode || null, countryCode: (test.countryCode || '').toUpperCase()
      }
    })
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    testing.value = false
  }
}

onMounted(() => {
  emit('save-status', null)
  load()
})
</script>

<style scoped>
.raw-response {
  max-height: 320px;
  overflow: auto;
  font-size: 12px;
  white-space: pre-wrap;
  word-break: break-word;
  margin: 0;
}
</style>
