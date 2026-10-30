<template>
  <q-page class="app-page">
    <AppDetailHeader
      :title="isCreate ? 'New currency' : (entity?.currencyCode || 'Currency')"
      :breadcrumbs="[
        { label: 'Home', icon: 'o_home', to: '/dashboard' },
        { label: 'Currencies', to: { name: 'currencies' } },
        { label: isCreate ? 'New currency' : (entity?.currencyCode || 'Currency') }
      ]"
      :status="!isCreate && entity ? (form.isEnabled ? 'Enabled' : 'Disabled') : ''"
      :status-color="form.isEnabled ? 'positive' : 'grey'"
      show-back
      @back="router.push({ name: 'currencies' })"
    />

    <q-inner-loading :showing="loading" color="primary" />

    <q-banner v-if="!loading && !isCreate && !entity" class="bg-grey-2 rounded-borders">
      Currency not found.
    </q-banner>

    <template v-if="isCreate || entity">
      <q-card flat bordered class="app-section">
        <q-tabs v-model="tab" align="left" active-color="primary" indicator-color="primary" class="text-grey-7 app-detail-tabs" no-caps inline-label>
          <q-tab name="general" icon="o_payments" label="General" />
        </q-tabs>
        <q-separator />

        <q-tab-panels v-model="tab" animated keep-alive>
          <q-tab-panel name="general" class="q-gutter-y-sm">
            <q-banner v-if="isBase" dense rounded class="bg-blue-1 text-blue-9 q-mb-sm">
              <template #avatar><q-icon name="o_star" color="blue" /></template>
              This is the base currency — its rate is fixed at 1 and it is always enabled.
            </q-banner>
            <div class="row q-col-gutter-sm">
              <div class="col-12 col-md-4">
                <AppTextField v-model="form.currencyCode" label="Currency code" required :v="v$.currencyCode" :disable="!isCreate" placeholder="USD" maxlength="3" hint="ISO 4217" @update:model-value="upperCode" />
              </div>
              <div class="col-12 col-md-4">
                <AppTextField v-model="form.symbol" label="Symbol" required :v="v$.symbol" placeholder="$" maxlength="8" />
              </div>
              <div class="col-12 col-md-4">
                <AppTextField v-model="form.exchangeRate" label="Exchange rate" required :v="v$.exchangeRate" type="number" step="any" min="0" :disable="isBase" :hint="isBase ? 'Fixed at 1' : 'Per 1 base unit'" />
              </div>
            </div>
            <div class="row q-gutter-md q-mt-xs">
              <q-toggle v-model="form.isEnabled" label="Enabled" color="primary" :disable="isBase" />
              <q-toggle v-model="form.isRateLocked" label="Lock rate (skip auto-refresh)" color="primary" :disable="isBase" />
            </div>
          </q-tab-panel>
        </q-tab-panels>

        <q-separator />
        <q-card-actions class="q-pa-md">
          <div class="text-caption text-grey-7">{{ isCreate ? 'Create this currency.' : 'Save your changes.' }}</div>
          <q-space />
          <q-btn v-if="canWrite" unelevated color="primary" no-caps :icon="isCreate ? 'o_check' : 'o_save'" :label="isCreate ? 'Create currency' : 'Save'" :loading="saving > 0 || creating" @click="save" />
        </q-card-actions>
      </q-card>
    </template>

    <AppRecordMeta entity-type="currency" :record-id="entity?.id" />
  </q-page>
</template>

<script setup>
/* Currency create + edit page (full-page, explicit Save — currency is code-keyed and the base currency
 * is special, so it does not use the auto-save flow). */
import { ref, computed } from 'vue'
import { useRouter } from 'vue-router'
import { usePermissions } from 'composables/usePermissions'
import { useDetailForm } from 'composables/useDetailForm'
import { required, maxLength, helpers } from 'validators'
import { currenciesApi } from 'modules/currencies/api'
import AppDetailHeader from 'components/common/AppDetailHeader.vue'
import AppTextField from 'components/common/AppTextField.vue'

const router = useRouter()
const { has } = usePermissions()
const canWrite = computed(() => has('Currencies.Write'))

const tab = ref('general')

const isoCode = helpers.withMessage('Use a three-letter code (e.g. USD)', (v) => !helpers.req(v) || /^[A-Za-z]{3}$/.test(String(v)))
const positiveRate = helpers.withMessage('Enter a rate greater than 0', (v) => !helpers.req(v) || (Number.isFinite(Number(v)) && Number(v) > 0))

function buildPayload (f) {
  return {
    currencyCode: (f.currencyCode || '').trim().toUpperCase(),
    symbol: (f.symbol || '').trim(),
    exchangeRate: Number(f.exchangeRate),
    isEnabled: !!f.isEnabled,
    isRateLocked: !!f.isRateLocked
  }
}

const {
  form, v$, entity, loading, creating, saving, isCreate, saveStatus, save
} = useDetailForm({
  createRouteName: 'currency-new',
  detailRouteName: 'currency-detail',
  entityLabel: 'currency',
  autoSave: false,
  idField: 'currencyCode',
  api: currenciesApi,
  buildPayload,
  empty: { currencyCode: '', symbol: '', exchangeRate: 1, isEnabled: true, isRateLocked: false },
  rules: {
    currencyCode: { required, isoCode },
    symbol: { required, maxLength: maxLength(8) },
    exchangeRate: { required, positiveRate }
  }
})

const isBase = computed(() => !!entity.value?.isBaseCurrency)
function upperCode (v) { form.currencyCode = (v || '').toUpperCase().slice(0, 3) }
// saveStatus referenced to keep the import shape consistent; header omits the chip for explicit-save pages.
void saveStatus
</script>

<style scoped lang="scss">
.app-detail-tabs {
  :deep(.q-tab) { min-height: 44px; }
}
</style>
