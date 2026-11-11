<template>
  <q-page class="app-page">
    <AppListHeader
      title="Checkout Settings"
      subtitle="Rules the storefront applies while a buyer places an order."
      :breadcrumbs="[{ label: 'Home', icon: 'o_home', to: '/dashboard' }, { label: 'Checkout Settings' }]"
    />

    <q-inner-loading :showing="loading" color="primary" />

    <template v-if="!loading">
      <AppSection title="Session and low-stock lock">
        <div class="row q-col-gutter-md">
          <div class="col-12 col-md-4">
            <AppTextField
              label="Checkout session timeout (seconds)"
              hint="Placement is refused after this long without activity. 0 = never expires. Old site: 1800."
              type="number"
              :model-value="form.sessionTimeoutSeconds"
              :disable="!canWrite"
              @update:model-value="form.sessionTimeoutSeconds = toInt($event)"
            />
          </div>
          <div class="col-12 col-md-4">
            <AppTextField
              label="Lock when stock is below"
              hint="A product with fewer units available than this is reserved for one buyer at a time."
              type="number"
              :model-value="form.lockQtyThreshold"
              :disable="!canWrite || !form.lockEnabled"
              @update:model-value="form.lockQtyThreshold = toInt($event)"
            />
          </div>
          <div class="col-12 col-md-4">
            <AppTextField
              label="Lock time (seconds)"
              hint="Counted from when the buyer reaches checkout. Refreshing never extends it."
              type="number"
              :model-value="form.lockSeconds"
              :disable="!canWrite || !form.lockEnabled"
              @update:model-value="form.lockSeconds = toInt($event)"
            />
          </div>
        </div>
        <q-toggle v-model="form.lockEnabled" :disable="!canWrite" label="Lock low-stock products during checkout" class="q-mt-sm" />
        <div class="text-body2 text-muted">
          Stops two buyers checking out the last few units at the same time. The buyer sees a countdown;
          the lock is released when they place the order or the time runs out.
        </div>
      </AppSection>

      <AppSection title="Terms and conditions">
        <q-toggle v-model="form.termsRequired" :disable="!canWrite" label="Buyers must accept the terms to place an order" />
        <div class="row q-col-gutter-md q-mt-xs">
          <div class="col-12 col-md-8">
            <AppTextField
              label="Checkbox text"
              :model-value="form.termsText"
              :disable="!canWrite"
              maxlength="500"
              @update:model-value="form.termsText = $event"
            />
          </div>
          <div class="col-12 col-md-4">
            <AppTextField
              label="Terms page link"
              hint="Leave empty for no link."
              :model-value="form.termsUrl"
              :disable="!canWrite"
              maxlength="500"
              @update:model-value="form.termsUrl = $event"
            />
          </div>
        </div>
      </AppSection>

      <AppSection title="State notices">
        <div class="text-body2 text-muted q-mb-md">
          Shown on the shipping step when the ship-to state matches. Use the two-letter state code.
        </div>
        <div v-for="(n, i) in form.stateNotices" :key="i" class="row q-col-gutter-md items-start q-mb-sm">
          <div class="col-12 col-md-2">
            <AppTextField
              label="State"
              :model-value="n.state"
              :disable="!canWrite"
              maxlength="30"
              @update:model-value="n.state = ($event || '').toUpperCase()"
            />
          </div>
          <div class="col-12 col-md-8">
            <AppTextField
              label="Message"
              type="textarea"
              autogrow
              :model-value="n.message"
              :disable="!canWrite"
              maxlength="1000"
              @update:model-value="n.message = $event"
            />
          </div>
          <div class="col-12 col-md-2 row items-center q-gutter-sm q-pt-md">
            <q-toggle v-model="n.active" :disable="!canWrite" label="Active" dense />
            <q-btn flat round dense icon="o_delete" color="negative" :disable="!canWrite" @click="form.stateNotices.splice(i, 1)">
              <q-tooltip>Remove</q-tooltip>
            </q-btn>
          </div>
        </div>
        <q-btn flat no-caps color="primary" icon="o_add" label="Add notice" :disable="!canWrite" @click="addNotice" />
      </AppSection>

      <AppSection title="Blacklist">
        <div class="row q-col-gutter-md">
          <div class="col-12 col-md-6">
            <AppSelect
              label="Match blocked buyers on"
              :model-value="form.blacklistMatchMode"
              :options="blacklistMatchModeOptions"
              :hint="matchModeHint"
              :disable="!canWrite"
              @update:model-value="form.blacklistMatchMode = $event"
            />
          </div>
          <div class="col-12 col-md-6">
            <AppTextField
              label="Alert these addresses"
              hint="Sent when a blocked buyer tries to order, comma separated. Empty = the store's notification address."
              :model-value="form.blacklistAlertEmail"
              :disable="!canWrite"
              @update:model-value="form.blacklistAlertEmail = $event"
            />
          </div>
        </div>
        <q-btn flat no-caps color="primary" icon="o_block" label="Manage blocked buyers" class="q-mt-sm" :to="{ name: 'admin-customer-blacklist' }" />
      </AppSection>

      <div class="row justify-end q-gutter-sm q-mb-lg">
        <q-btn flat no-caps label="Reset" :disable="!dirty || saving" @click="reset" />
        <q-btn
          unelevated
          color="primary"
          no-caps
          label="Save changes"
          :loading="saving"
          :disable="!canWrite || !dirty"
          @click="save"
        />
      </div>
    </template>
  </q-page>
</template>

<script setup>
/*
 * Checkout Settings (requirements section 7): the values the old site hard-coded — session timeout,
 * low-stock checkout lock, terms checkbox, per-state notices and blacklist matching. Saved as audited
 * platform settings, so each change shows in the settings history and applies on the next request.
 */
import { ref, computed, onMounted } from 'vue'
import { checkoutSettingsApi, blacklistMatchModeOptions } from 'modules/settings/checkout-api'
import { usePermissions, Permissions } from 'composables/usePermissions'
import { useNotify } from 'composables/useNotify'
import { getApiErrorMessage } from 'services/api'

const { has } = usePermissions()
const notify = useNotify()
const canWrite = computed(() => has(Permissions.SettingsWrite))

const loading = ref(true)
const saving = ref(false)
const original = ref(null)
const form = ref(blank())

function blank () {
  return {
    sessionTimeoutSeconds: 1800,
    lockEnabled: true,
    lockQtyThreshold: 10,
    lockSeconds: 600,
    termsRequired: true,
    termsText: '',
    termsUrl: '',
    stateNotices: [],
    blacklistMatchMode: 'NameOrEmail',
    blacklistAlertEmail: ''
  }
}

const dirty = computed(() => JSON.stringify(form.value) !== JSON.stringify(original.value))
const matchModeHint = computed(
  () => (blacklistMatchModeOptions.find((o) => o.value === form.value.blacklistMatchMode) || {}).description || ''
)

function toInt (v) {
  const n = parseInt(v, 10)
  return Number.isFinite(n) ? n : 0
}

function apply (dto) {
  form.value = {
    sessionTimeoutSeconds: dto.sessionTimeoutSeconds ?? 1800,
    lockEnabled: dto.lockEnabled !== false,
    lockQtyThreshold: dto.lockQtyThreshold ?? 10,
    lockSeconds: dto.lockSeconds ?? 600,
    termsRequired: dto.termsRequired !== false,
    termsText: dto.termsText || '',
    termsUrl: dto.termsUrl || '',
    stateNotices: (dto.stateNotices || []).map((n) => ({ state: n.state || '', message: n.message || '', active: n.active !== false })),
    blacklistMatchMode: dto.blacklistMatchMode || 'NameOrEmail',
    blacklistAlertEmail: dto.blacklistAlertEmail || ''
  }
  original.value = JSON.parse(JSON.stringify(form.value))
}

function reset () {
  if (original.value) form.value = JSON.parse(JSON.stringify(original.value))
}

function addNotice () {
  form.value.stateNotices.push({ state: '', message: '', active: true })
}

async function load () {
  loading.value = true
  try {
    apply((await checkoutSettingsApi.get()) || {})
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    loading.value = false
  }
}

async function save () {
  saving.value = true
  try {
    const f = form.value
    const dto = await checkoutSettingsApi.update({
      ...f,
      termsUrl: f.termsUrl || null,
      blacklistAlertEmail: f.blacklistAlertEmail || null,
      stateNotices: f.stateNotices.filter((n) => n.state.trim() || n.message.trim())
    })
    apply(dto || {})
    notify.success('Checkout settings saved.')
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    saving.value = false
  }
}

onMounted(load)
</script>
