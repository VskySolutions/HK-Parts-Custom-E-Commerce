<template>
  <q-page class="app-page">
    <AppListHeader
      title="Loyalty Points"
      :breadcrumbs="[{ label: 'Home', icon: 'o_home', to: '/dashboard' }, { label: 'Loyalty Points' }]"
      :show-add="false"
      show-back
      @back="router.push('/dashboard')"
    />

    <div class="row q-col-gutter-md">
      <!-- Program configuration -->
      <div class="col-12 col-md-7">
        <AppSection title="Loyalty program">
          <template #footer>
            <q-btn
              v-if="canWrite"
              color="primary"
              unelevated
              no-caps
              icon="o_save"
              label="Save"
              :loading="saving"
              @click="save"
            />
          </template>

          <q-inner-loading :showing="loading" />

          <q-toggle
            v-model="config.enabled"
            label="Loyalty program enabled"
            color="primary"
            :disable="!canWrite"
            class="q-mb-md"
          />
          <div class="text-caption text-grey-6 q-mb-md">
            When disabled, customers neither earn nor redeem points.
          </div>

          <AppTextField
            v-model="config.earnRate"
            label="Earn rate"
            type="number"
            step="0.01"
            min="0"
            :v="v$.earnRate"
            :disable="!canWrite"
          >
            <template #hint>
              <span class="text-caption text-grey-7">Points earned per 1 currency unit spent.</span>
            </template>
          </AppTextField>

          <AppTextField
            v-model="config.redeemRate"
            label="Redeem rate"
            type="number"
            step="1"
            min="1"
            :v="v$.redeemRate"
            :disable="!canWrite"
          >
            <template #hint>
              <span class="text-caption text-grey-7">Points needed per 1 currency unit of discount.</span>
            </template>
          </AppTextField>
        </AppSection>
      </div>

      <!-- Worked example -->
      <div class="col-12 col-md-5">
        <AppSection title="Worked example">
          <div class="row items-center no-wrap q-gutter-xs text-body2">
            <q-icon name="o_add_circle" color="positive" size="20px" />
            <span>Earn: spend $10 &rarr; <strong>{{ earnExample }}</strong> points</span>
          </div>
          <div class="row items-center no-wrap q-gutter-xs text-body2 q-mt-sm">
            <q-icon name="o_redeem" color="primary" size="20px" />
            <span>Redeem: <strong>{{ redeemExample }}</strong> points = $1 discount</span>
          </div>
          <q-separator class="q-my-md" />
          <div class="text-caption text-grey-6">
            Rates apply to the store base currency. Redeem rate is the number of points a
            customer spends to knock $1 off an order.
          </div>
        </AppSection>
      </div>
    </div>
  </q-page>
</template>

<script setup>
/*
 * Loyalty settings (WO-27): the keyed-singleton loyalty program config — enabled
 * toggle, earn rate (points per currency unit spent) and redeem rate (points per
 * currency unit of discount), plus a live worked example. Backed by
 * GET/PUT /api/admin/loyalty. Load-on-mount + Save, not the useDetailForm pattern,
 * because there is no entity id to route on.
 */
import { reactive, ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import useVuelidate from '@vuelidate/core'
import { minValue } from 'validators'
import { getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'
import { usePermissions, Permissions } from 'composables/usePermissions'
import { loyaltyApi } from 'modules/loyalty/api'

const router = useRouter()
const notify = useNotify()
const { has } = usePermissions()

// There is no Promotions.Write in the Permissions catalog yet, so gate writes on
// Catalog.Write (per the WO-27 spec's "else has('Catalog.Write')").
const canWrite = computed(() => has(Permissions.CatalogWrite))

const loading = ref(false)
const saving = ref(false)
const config = reactive({ enabled: false, earnRate: 0, redeemRate: 1 })

const rules = {
  earnRate: { minValue: minValue(0) },
  redeemRate: { minValue: minValue(1) }
}
const v$ = useVuelidate(rules, config)

// Round to avoid floating-point noise in the example (e.g. 0.1 * 10).
const earnExample = computed(() => Math.round((Number(config.earnRate) || 0) * 10 * 100) / 100)
const redeemExample = computed(() => Number(config.redeemRate) || 0)

async function load () {
  loading.value = true
  try {
    const dto = await loyaltyApi.get()
    if (dto) {
      config.enabled = !!dto.enabled
      config.earnRate = dto.earnRate != null ? dto.earnRate : 0
      config.redeemRate = dto.redeemRate != null ? dto.redeemRate : 1
    }
    v$.value.$reset()
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    loading.value = false
  }
}

async function save () {
  const ok = await v$.value.$validate()
  if (!ok) return
  saving.value = true
  try {
    await loyaltyApi.update({
      enabled: !!config.enabled,
      earnRate: Number(config.earnRate) || 0,
      redeemRate: Number(config.redeemRate) || 0
    })
    notify.success('Loyalty settings saved')
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    saving.value = false
  }
}

onMounted(load)
</script>
