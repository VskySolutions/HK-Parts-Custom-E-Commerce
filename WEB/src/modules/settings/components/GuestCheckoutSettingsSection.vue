<template>
  <AppSection title="Guest checkout">
    <q-inner-loading :showing="loading" color="primary" />
    <template v-if="!loading">
      <q-toggle v-model="form.enabled" :disable="!canWrite" label="Allow guest checkout" />
      <div class="text-body2 text-muted q-mb-md">
        Off: shoppers must sign in or create an account to place an order. Orders already in progress may still finish.
        Products marked "Account required" always need an account.
      </div>

      <div class="row q-col-gutter-md">
        <div class="col-12 col-md-6">
          <AppSelect
            label="Payment methods guests may use"
            multiple
            use-chips
            clearable
            hint="Leave empty to offer guests every enabled method. Gift certificates and zero-total orders are always allowed."
            :model-value="form.allowedPaymentMethods"
            :options="guestPaymentMethodOptions"
            :disable="!canWrite || !form.enabled"
            @update:model-value="form.allowedPaymentMethods = $event || []"
          />
        </div>
        <div class="col-12 col-md-3">
          <AppTextField
            label="Guest order limit"
            type="number"
            hint="Largest order total a guest may place. Empty = no limit."
            :model-value="form.maxOrderTotal"
            :disable="!canWrite || !form.enabled"
            @update:model-value="form.maxOrderTotal = $event"
          />
        </div>
        <div class="col-12 col-md-3">
          <AppTextField
            label="Order link lifetime (days)"
            type="number"
            hint="How long the &quot;View your order&quot; link in guest emails works."
            :model-value="form.linkLifetimeDays"
            :disable="!canWrite"
            @update:model-value="form.linkLifetimeDays = $event"
          />
        </div>
      </div>

      <q-toggle
        v-model="form.autoLinkOrdersOnVerify"
        :disable="!canWrite"
        label="Move a guest's past orders to their account once they verify their email"
        class="q-mt-sm"
      />
      <div class="text-body2 text-muted">
        Off: link them yourself from Customers &rarr; Guest Customers.
      </div>

      <div class="row justify-end q-gutter-sm q-mt-md">
        <q-btn flat no-caps label="Reset" :disable="!dirty || saving" @click="reset" />
        <q-btn
          unelevated
          color="primary"
          no-caps
          label="Save guest settings"
          :loading="saving"
          :disable="!canWrite || !dirty"
          @click="save"
        />
      </div>
    </template>
  </AppSection>
</template>

<script setup>
/*
 * Guest checkout settings (docs/guest-checkout-requirements.md §4.1, A-01..A-05): on/off switch, guest payment
 * methods, guest order limit, email link lifetime and automatic order linking. Saved as audited platform
 * settings (A-06), separately from the rest of the checkout settings on this page.
 */
import { ref, computed, onMounted } from 'vue'
import { guestCheckoutSettingsApi, guestPaymentMethodOptions } from 'modules/settings/checkout-api'
import { useNotify } from 'composables/useNotify'
import { getApiErrorMessage } from 'services/api'

defineProps({
  canWrite: { type: Boolean, default: false }
})

const notify = useNotify()
const loading = ref(true)
const saving = ref(false)
const original = ref(null)
const form = ref({ enabled: true, allowedPaymentMethods: [], maxOrderTotal: '', linkLifetimeDays: 30, autoLinkOrdersOnVerify: true })

const dirty = computed(() => JSON.stringify(form.value) !== JSON.stringify(original.value))

function apply (dto) {
  form.value = {
    enabled: dto.enabled !== false,
    allowedPaymentMethods: Array.isArray(dto.allowedPaymentMethods) ? [...dto.allowedPaymentMethods] : [],
    maxOrderTotal: dto.maxOrderTotal == null ? '' : String(dto.maxOrderTotal),
    linkLifetimeDays: dto.linkLifetimeDays ?? 30,
    autoLinkOrdersOnVerify: dto.autoLinkOrdersOnVerify !== false
  }
  original.value = JSON.parse(JSON.stringify(form.value))
}

function reset () {
  if (original.value) form.value = JSON.parse(JSON.stringify(original.value))
}

async function load () {
  loading.value = true
  try {
    apply((await guestCheckoutSettingsApi.get()) || {})
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    loading.value = false
  }
}

async function save () {
  const f = form.value
  const limit = String(f.maxOrderTotal ?? '').trim()
  const days = parseInt(f.linkLifetimeDays, 10)
  saving.value = true
  try {
    apply((await guestCheckoutSettingsApi.update({
      enabled: f.enabled,
      allowedPaymentMethods: f.allowedPaymentMethods,
      maxOrderTotal: limit === '' ? null : Number(limit),
      linkLifetimeDays: Number.isFinite(days) ? days : 30,
      autoLinkOrdersOnVerify: f.autoLinkOrdersOnVerify
    })) || {})
    notify.success('Guest checkout settings saved.')
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    saving.value = false
  }
}

onMounted(load)
</script>
