<template>
  <div class="row q-col-gutter-md">
    <div class="col-12 col-lg-8">
      <AppSection title="Avalara settings">
        <template #actions>
          <q-btn flat no-caps color="primary" icon="o_receipt_long" label="View Log" @click="emit('view-log')" />
        </template>
        <template #footer>
          <q-btn outline color="orange-8" no-caps icon="o_wifi_tethering" label="Test Avalara Settings" :loading="testing" :disable="busy" @click="test" />
          <q-btn color="primary" unelevated no-caps icon="o_save" label="Save Avalara Settings" :loading="busy" @click="saveAll" />
        </template>

        <q-inner-loading :showing="loadingAccount" />

        <div class="row q-col-gutter-sm">
          <div class="col-12 col-sm-6">
            <q-input
              v-model="account.accountNumber"
              dense
              outlined
              maxlength="100"
              label="Account Number *"
              :error="!!errors.accountNumber"
              :error-message="errors.accountNumber"
            />
          </div>
          <div class="col-12 col-sm-6">
            <q-input
              v-model="account.licenseKey"
              dense
              outlined
              :type="revealKey ? 'text' : 'password'"
              autocomplete="new-password"
              :label="account.hasLicenseKey ? 'License Key' : 'License Key *'"
              :hint="account.hasLicenseKey ? 'A key is on file. Leave blank to keep it.' : ''"
              :error="!!errors.licenseKey"
              :error-message="errors.licenseKey"
            >
              <template #append>
                <q-icon :name="revealKey ? 'o_visibility_off' : 'o_visibility'" class="cursor-pointer" @click="revealKey = !revealKey" />
              </template>
            </q-input>
          </div>
          <div class="col-12 col-sm-6">
            <q-input
              v-model="account.companyCode"
              dense
              outlined
              maxlength="100"
              label="Company Code *"
              :error="!!errors.companyCode"
              :error-message="errors.companyCode"
            />
          </div>
          <div class="col-12 col-sm-6">
            <q-input
              v-model="account.serviceUrl"
              dense
              outlined
              maxlength="500"
              label="Service URL *"
              :hint="account.serviceUrl ? (isLive ? 'Live (production) AvaTax' : 'Sandbox / test AvaTax') : 'https://sandbox-rest.avatax.com or https://rest.avatax.com'"
              :error="!!errors.serviceUrl"
              :error-message="errors.serviceUrl"
            >
              <template #after>
                <q-btn-dropdown flat dense no-caps size="sm" label="Use">
                  <q-list dense>
                    <q-item v-close-popup clickable @click="account.serviceUrl = SANDBOX_URL"><q-item-section>Sandbox</q-item-section></q-item>
                    <q-item v-close-popup clickable @click="account.serviceUrl = LIVE_URL"><q-item-section>Live</q-item-section></q-item>
                  </q-list>
                </q-btn-dropdown>
              </template>
            </q-input>
          </div>

          <div class="col-12 col-sm-6">
            <q-select
              v-model="cfg.avalaraCommitDocuments"
              :options="enabledOptions"
              emit-value
              map-options
              dense
              outlined
              label="Document Committing"
              hint="Commit a sales invoice in Avalara when a paid order is placed; void it when the order is cancelled"
            />
          </div>
          <div class="col-12 col-sm-6">
            <q-select
              v-model="cfg.avalaraLoggingEnabled"
              :options="enabledOptions"
              emit-value
              map-options
              dense
              outlined
              label="Logging"
              hint="Record every Avalara request and response (Avalara Log tab)"
            />
          </div>
          <div class="col-12 col-sm-6">
            <q-input
              v-model="cfg.avalaraTimeoutSeconds"
              dense
              outlined
              type="number"
              min="1"
              max="120"
              label="Request Timeout *"
              suffix="seconds"
              :error="!!errors.timeout"
              :error-message="errors.timeout"
            />
          </div>
          <div class="col-12 col-sm-6">
            <q-input v-model="cfg.avalaraShippingTaxCode" dense outlined maxlength="50" label="Default Shipping Tax Code" hint="Sent on the shipping line, e.g. FR020100" />
          </div>
        </div>

        <div v-if="result" class="q-mt-md">
          <q-banner dense rounded :class="result.success ? 'bg-green-1 text-positive' : 'bg-red-1 text-negative'">
            <template #avatar><q-icon :name="result.success ? 'o_check_circle' : 'o_error_outline'" /></template>
            Test connection: {{ result.message }}
          </q-banner>
        </div>
      </AppSection>
    </div>

    <div class="col-12 col-lg-4">
      <AppSection title="How Avalara is used">
        <div class="row items-center q-gutter-sm q-mb-sm">
          <q-badge :color="cfg.avalaraConfigured ? 'positive' : 'orange-8'" :label="cfg.avalaraConfigured ? 'Account on file' : 'No account on file'" />
          <q-badge v-if="account.id" outline :color="account.isProduction ? 'negative' : 'grey-7'" :label="account.isProduction ? 'Live' : 'Sandbox'" />
        </div>
        <ul class="text-body2 text-grey-8 q-pl-md q-my-none">
          <li>Choose <b>Avalara</b> as the default tax method on the Settings tab, or on a customer group.</li>
          <li>Product tax codes come from each product's <b>Tax code</b> field.</li>
          <li>A customer's <b>Tax exempt reason</b> is sent to Avalara as the exemption number.</li>
          <li>This account is shared with Avalara address verification.</li>
        </ul>
        <q-btn class="q-mt-md" flat color="primary" no-caps icon="o_vpn_key" label="Manage in Integrations" :to="{ path: '/integrations', query: { section: 'avalara' } }" />
      </AppSection>
    </div>
  </div>
</template>

<script setup>
/*
 * PHP "Avalara Settings" (/echo/taxes/avalara): account number, license key, company code, service URL, document
 * committing, logging, request timeout and default shipping tax code. Save stores the account (on the active Avalara
 * credential) and the settings, then runs the connection test, as PHP did. "Test" pings with what is on file.
 */
import { computed, onMounted, reactive, ref } from 'vue'
import { getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'
import { taxApi } from 'modules/tax/api'
import AppSection from 'components/common/AppSection.vue'

const SANDBOX_URL = 'https://sandbox-rest.avatax.com'
const LIVE_URL = 'https://rest.avatax.com'

// The parent owns the settings object (shared with the Settings tab) and its save; saveConfig(quiet) → boolean.
const props = defineProps({
  config: { type: Object, required: true },
  saving: { type: Boolean, default: false },
  saveConfig: { type: Function, required: true }
})
const emit = defineEmits(['view-log'])

const cfg = props.config

const notify = useNotify()
const testing = ref(false)
const savingAccount = ref(false)
const loadingAccount = ref(false)
const revealKey = ref(false)
const result = ref(null)
const enabledOptions = [{ label: 'Enabled', value: true }, { label: 'Disabled', value: false }]

const account = reactive({
  id: null,
  serviceUrl: '',
  accountNumber: '',
  companyCode: '',
  licenseKey: '',
  hasLicenseKey: false,
  isProduction: false
})
const errors = reactive({ serviceUrl: '', accountNumber: '', companyCode: '', licenseKey: '', timeout: '' })

const busy = computed(() => savingAccount.value || props.saving)
const isLive = computed(() => {
  try { return new URL(account.serviceUrl).host.toLowerCase() === 'rest.avatax.com' } catch { return false }
})

function applyAccount (a) {
  Object.assign(account, {
    id: a?.id ?? null,
    serviceUrl: a?.serviceUrl || '',
    accountNumber: a?.accountNumber || '',
    companyCode: a?.companyCode || '',
    licenseKey: '',
    hasLicenseKey: !!a?.hasLicenseKey,
    isProduction: !!a?.isProduction
  })
}

async function loadAccount () {
  loadingAccount.value = true
  try {
    applyAccount(await taxApi.avalaraAccount())
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    loadingAccount.value = false
  }
}

// PHP AvalaraSettingsRequest: all four account fields are required, and the timeout must be an integer.
function validate () {
  const url = (account.serviceUrl || '').trim()
  let urlOk = false
  try { urlOk = /^https?:$/.test(new URL(url).protocol) } catch { urlOk = false }
  const timeout = Number(cfg.avalaraTimeoutSeconds)
  errors.serviceUrl = !url ? 'Service URL is required' : (urlOk ? '' : 'Enter an absolute http(s) URL')
  errors.accountNumber = (account.accountNumber || '').trim() ? '' : 'Account Number is required'
  errors.companyCode = (account.companyCode || '').trim() ? '' : 'Company Code is required'
  errors.licenseKey = account.hasLicenseKey || (account.licenseKey || '').trim() ? '' : 'License Key is required'
  errors.timeout = Number.isInteger(timeout) && timeout >= 1 && timeout <= 120 ? '' : 'Enter whole seconds between 1 and 120'
  return !Object.values(errors).some(Boolean)
}

async function saveAll () {
  if (!validate()) return
  savingAccount.value = true
  result.value = null
  try {
    applyAccount(await taxApi.saveAvalaraAccount({
      serviceUrl: account.serviceUrl.trim(),
      accountNumber: account.accountNumber.trim(),
      companyCode: account.companyCode.trim(),
      licenseKey: (account.licenseKey || '').trim() || null
    }))
  } catch (err) {
    notify.error(getApiErrorMessage(err))
    savingAccount.value = false
    return
  }
  savingAccount.value = false

  // The settings save refreshes cfg.avalaraConfigured from the server.
  const ok = await props.saveConfig(true)
  if (!ok) return
  notify.success('Avalara settings saved')
  await test()
}

async function test () {
  testing.value = true
  result.value = null
  try {
    result.value = await taxApi.testAvalara()
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    testing.value = false
  }
}

onMounted(loadAccount)
</script>
