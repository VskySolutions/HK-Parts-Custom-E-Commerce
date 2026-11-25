<template>
  <q-page class="app-page">
    <AppListHeader title="Tax" :breadcrumbs="[{ label: 'Home', icon: 'o_home', to: '/dashboard' }, { label: 'Tax' }]" :show-add="false" show-back @back="router.push('/dashboard')" />

    <q-card flat bordered>
      <q-tabs v-model="tab" dense align="left" no-caps active-color="primary" indicator-color="primary" class="text-grey-8" outside-arrows mobile-arrows>
        <q-tab name="settings" icon="o_tune" label="Settings" />
        <q-tab name="state" icon="o_map" label="State Rates" />
        <q-tab name="zip" icon="o_pin_drop" label="ZIP Rates" />
        <q-tab name="avalara" icon="o_fact_check" label="Avalara" />
        <q-tab name="log" icon="o_receipt_long" label="Avalara Log" />
        <q-tab name="nexus" icon="o_public" label="US Nexus" />
      </q-tabs>
      <q-separator />

      <q-tab-panels v-model="tab" animated keep-alive>
        <!-- PHP "Tax Settings" -->
        <q-tab-panel name="settings" class="q-pa-md">
          <div class="row q-col-gutter-md">
            <div class="col-12 col-md-7">
              <AppSection title="Tax settings">
                <template #footer>
                  <q-btn color="primary" unelevated no-caps icon="o_save" label="Save Settings" :loading="saving" @click="save()" />
                </template>
                <q-inner-loading :showing="loading" />
                <AppSelect v-model="config.activeProvider" label="Default tax method" :options="taxProviderOptions" :hint="methodHint" />
                <q-toggle v-model="config.taxShipping" label="Tax shipping amount" color="primary" class="q-mt-sm" />
                <div class="text-caption text-grey-7 q-ml-sm">
                  Adds the shipping charge to the taxed amount for flat rate, By State and By ZIP. Not applicable to Avalara:
                  your Avalara nexus settings decide this.
                </div>
                <q-separator class="q-my-md" />
                <AppTextField v-model="config.flatRatePercent" label="Flat rate (%)" type="number" step="0.01" hint="The Flat rate method's rate, and the fallback when TaxJar, Stripe Tax or Avalara can't be reached" />
                <AppTextField v-model="config.cacheTtlMinutes" label="Provider cache (minutes)" type="number" hint="How long an identical TaxJar / Stripe Tax / Avalara answer is reused. 0 = off" />
                <q-toggle v-model="config.isEnabled" label="Tax calculation enabled" color="primary" class="q-mt-sm" />
                <div class="text-caption text-grey-7 q-ml-sm">Off = no tax is charged on any order, whatever the method.</div>
              </AppSection>
            </div>
            <div class="col-12 col-md-5">
              <AppSection title="How tax is worked out">
                <ul class="q-pl-md q-my-none text-body2 text-grey-8">
                  <li><b>No Tax Charged</b>: no tax on any order.</li>
                  <li><b>By State</b>: the ship-to state's rate from <a href="#" @click.prevent="tab = 'state'">State Rates</a>.</li>
                  <li><b>By ZIP/Postal Code</b>: the rate for the first 5 characters of the ship-to ZIP from <a href="#" @click.prevent="tab = 'zip'">ZIP Rates</a>.</li>
                  <li><b>Avalara</b>: AvaTax works out the tax at checkout; see the <a href="#" @click.prevent="tab = 'avalara'">Avalara</a> tab.</li>
                  <li>No rate on file for the address = no tax, as on the old site.</li>
                  <li>Gift certificates are never taxed. Tax-exempt customers pay none.</li>
                  <li>A customer group can override the method (Customers → Groups → Tax method), e.g. <b>Tax Exempt</b>.</li>
                </ul>
              </AppSection>
            </div>
          </div>
        </q-tab-panel>

        <q-tab-panel name="state" class="q-pa-md">
          <TaxStateRatesPanel />
        </q-tab-panel>

        <q-tab-panel name="zip" class="q-pa-md">
          <TaxZipRatesPanel />
        </q-tab-panel>

        <q-tab-panel name="avalara" class="q-pa-md">
          <TaxAvalaraPanel :config="config" :saving="saving" :save-config="save" @view-log="tab = 'log'" />
        </q-tab-panel>

        <q-tab-panel name="log" class="q-pa-md">
          <TaxLogPanel />
        </q-tab-panel>

        <q-tab-panel name="nexus" class="q-pa-md">
          <AppSection title="US economic nexus">
            <template #actions>
              <q-input v-model.number="year" dense outlined type="number" style="width: 110px" label="Year" @update:model-value="loadNexus" />
            </template>
            <q-markup-table flat>
              <thead>
                <tr>
                  <th class="text-left">State</th>
                  <th class="text-right">Gross sales</th>
                  <th class="text-right">Txns</th>
                  <th class="text-right">% to threshold</th>
                  <th class="text-left">Status</th>
                </tr>
              </thead>
              <tbody>
                <tr v-if="!nexus.length"><td colspan="5" class="text-center text-grey-6 q-pa-lg">No nexus accumulation recorded for this year.</td></tr>
                <tr v-for="n in nexus" :key="n.stateCode">
                  <td>{{ n.stateCode }}</td>
                  <td class="text-right">{{ money(n.grossSales) }}</td>
                  <td class="text-right">{{ n.transactionCount }}</td>
                  <td class="text-right">{{ Math.round(n.percentToThreshold) }}%</td>
                  <td>
                    <q-badge v-if="n.exceeded" color="negative" label="Exceeded" />
                    <q-badge v-else-if="n.approaching" color="orange" label="Approaching" />
                    <q-badge v-else color="grey" label="OK" />
                  </td>
                </tr>
              </tbody>
            </q-markup-table>
          </AppSection>
        </q-tab-panel>
      </q-tab-panels>
    </q-card>
  </q-page>
</template>

<script setup>
/*
 * Tax admin (WO-120 + PHP ecommcore tax parity): Settings (method, tax shipping), State Rates, ZIP Rates,
 * Avalara settings + test, Avalara log, and US economic-nexus status. The Tax Report is under Reports.
 */
import { ref, reactive, computed, onMounted, watch } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'
import { taxApi, taxProviderOptions } from 'modules/tax/api'
import AppListHeader from 'components/common/AppListHeader.vue'
import AppSection from 'components/common/AppSection.vue'
import AppSelect from 'components/common/AppSelect.vue'
import AppTextField from 'components/common/AppTextField.vue'
import TaxStateRatesPanel from 'modules/tax/components/TaxStateRatesPanel.vue'
import TaxZipRatesPanel from 'modules/tax/components/TaxZipRatesPanel.vue'
import TaxAvalaraPanel from 'modules/tax/components/TaxAvalaraPanel.vue'
import TaxLogPanel from 'modules/tax/components/TaxLogPanel.vue'

const TABS = ['settings', 'state', 'zip', 'avalara', 'log', 'nexus']

const router = useRouter()
const route = useRoute()
const notify = useNotify()
const tab = ref(TABS.includes(route.query.tab) ? route.query.tab : 'settings')
const loading = ref(false)
const saving = ref(false)
const config = reactive({
  activeProvider: 'FlatRate',
  flatRatePercent: 0,
  isEnabled: true,
  cacheTtlMinutes: 60,
  taxShipping: true,
  avalaraCommitDocuments: true,
  avalaraLoggingEnabled: true,
  avalaraTimeoutSeconds: 10,
  avalaraShippingTaxCode: 'FR020100',
  avalaraConfigured: false,
  avalaraCompanyCode: null
})
const nexus = ref([])
const year = ref(new Date().getFullYear())

const methodHint = computed(() => ({
  None: 'No tax is charged on any order.',
  ByState: 'Uses the State Rates tab.',
  ByZip: 'Uses the ZIP Rates tab.',
  Avalara: config.avalaraConfigured ? 'Uses your Avalara account (Avalara tab).' : 'Add your Avalara account first (Avalara tab).',
  FlatRate: 'One rate for every order.'
}[config.activeProvider] || ''))

watch(tab, (t) => { router.replace({ query: { ...route.query, tab: t } }) })

function money (v) { return v == null ? '—' : Number(v).toFixed(2) }

async function loadConfig () {
  loading.value = true
  try {
    const c = await taxApi.getConfig()
    if (c) Object.assign(config, c)
  } catch (err) { notify.error(getApiErrorMessage(err)) } finally { loading.value = false }
}

async function loadNexus () {
  try {
    const res = await taxApi.nexusStatus({ year: year.value })
    nexus.value = Array.isArray(res) ? res : []
  } catch (err) { nexus.value = [] }
}

// quiet: the Avalara tab saves the account too and reports one combined result.
async function save (quiet = false) {
  saving.value = true
  try {
    const c = await taxApi.updateConfig({
      activeProvider: config.activeProvider,
      flatRatePercent: Number(config.flatRatePercent) || 0,
      isEnabled: config.isEnabled,
      cacheTtlMinutes: Number(config.cacheTtlMinutes) || 0,
      taxShipping: config.taxShipping,
      avalaraCommitDocuments: config.avalaraCommitDocuments,
      avalaraLoggingEnabled: config.avalaraLoggingEnabled,
      avalaraTimeoutSeconds: Number(config.avalaraTimeoutSeconds) || 10,
      avalaraShippingTaxCode: config.avalaraShippingTaxCode || ''
    })
    if (c) Object.assign(config, c)
    if (!quiet) notify.success('Tax settings saved')
    return true
  } catch (err) {
    notify.error(getApiErrorMessage(err))
    return false
  } finally { saving.value = false }
}

onMounted(() => { loadConfig(); loadNexus() })
</script>
