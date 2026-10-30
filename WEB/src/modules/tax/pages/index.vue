<template>
  <q-page class="app-page">
    <AppListHeader title="Tax" :breadcrumbs="[{ label: 'Home', icon: 'o_home', to: '/dashboard' }, { label: 'Tax' }]" :show-add="false" show-back @back="router.push('/dashboard')" />

    <div class="row q-col-gutter-md">
      <div class="col-12 col-md-5">
        <AppSection title="Tax configuration">
          <template #footer>
            <q-btn color="primary" unelevated no-caps icon="o_save" label="Save" :loading="saving" @click="save" />
          </template>
          <q-inner-loading :showing="loading" />
          <AppSelect v-model="config.activeProvider" label="Active provider" :options="taxProviderOptions" />
          <AppTextField v-model="config.flatRatePercent" label="Flat-rate fallback (%)" type="number" step="0.01" placeholder="Used when the provider is unavailable" />
          <AppTextField v-model="config.cacheTtlMinutes" label="Cache TTL (minutes)" type="number" placeholder="e.g. 60" />
          <q-toggle v-model="config.isEnabled" label="Tax calculation enabled" color="primary" class="q-mt-sm" />
        </AppSection>
      </div>

      <div class="col-12 col-md-7">
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
      </div>
    </div>
  </q-page>
</template>

<script setup>
/* Tax admin (WO-120): provider configuration + US economic-nexus status. */
import { ref, reactive, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'
import { taxApi, taxProviderOptions } from 'modules/tax/api'
import AppListHeader from 'components/common/AppListHeader.vue'
import AppSection from 'components/common/AppSection.vue'
import AppSelect from 'components/common/AppSelect.vue'
import AppTextField from 'components/common/AppTextField.vue'

const router = useRouter()
const notify = useNotify()
const loading = ref(false)
const saving = ref(false)
const config = reactive({ activeProvider: 'FlatRate', flatRatePercent: 0, isEnabled: true, cacheTtlMinutes: 60 })
const nexus = ref([])
const year = ref(new Date().getFullYear())

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

async function save () {
  saving.value = true
  try {
    await taxApi.updateConfig({
      activeProvider: config.activeProvider,
      flatRatePercent: Number(config.flatRatePercent) || 0,
      isEnabled: config.isEnabled,
      cacheTtlMinutes: Number(config.cacheTtlMinutes) || 0
    })
    notify.success('Tax configuration saved')
  } catch (err) { notify.error(getApiErrorMessage(err)) } finally { saving.value = false }
}

onMounted(() => { loadConfig(); loadNexus() })
</script>
