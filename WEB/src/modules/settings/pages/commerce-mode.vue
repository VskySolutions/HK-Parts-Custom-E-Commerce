<template>
  <q-page class="app-page">
    <AppListHeader title="Commerce Mode" />

    <q-inner-loading :showing="loading" color="primary" />

    <template v-if="!loading">
      <!-- The switch itself: two mutually exclusive ways for this tenant to sell. -->
      <AppSection title="How this store sells">
        <div class="row q-col-gutter-md">
          <div v-for="opt in MODES" :key="opt.value" class="col-12 col-md-6">
            <q-card
              flat
              bordered
              class="cm-mode full-height cursor-pointer"
              :class="{ 'cm-mode--active': form.mode === opt.value }"
              @click="canWrite && (form.mode = opt.value)"
            >
              <q-card-section class="row items-start no-wrap q-gutter-sm">
                <q-radio
                  :model-value="form.mode"
                  :val="opt.value"
                  :disable="!canWrite"
                  color="primary"
                  @update:model-value="form.mode = $event"
                />
                <div class="col">
                  <div class="row items-center q-gutter-xs">
                    <q-icon :name="opt.icon" size="20px" color="primary" />
                    <span class="text-weight-bold">{{ opt.label }}</span>
                  </div>
                  <div class="text-body2 text-muted q-mt-xs">{{ opt.description }}</div>
                  <q-list dense class="q-mt-sm">
                    <q-item v-for="line in opt.bullets" :key="line" dense class="q-pa-none">
                      <q-item-section avatar style="min-width: 24px">
                        <q-icon :name="opt.bulletIcon" size="16px" :color="opt.bulletColor" />
                      </q-item-section>
                      <q-item-section class="text-body2">{{ line }}</q-item-section>
                    </q-item>
                  </q-list>
                </div>
              </q-card-section>
            </q-card>
          </div>
        </div>
      </AppSection>

      <!-- Inquiry behaviour. Shown for both modes: a Standard tenant can still flag individual
           products as quote-only, and these settings govern those requests too. -->
      <AppSection title="Inquiry settings">
        <div class="row q-col-gutter-md">
          <div class="col-12 col-md-6">
            <AppTextField
              label="Request button label"
              hint="Storefront call-to-action for quote-only products. A product can override it."
              :model-value="form.inquiryButtonLabel"
              :disable="!canWrite"
              maxlength="80"
              @update:model-value="form.inquiryButtonLabel = $event"
            />
          </div>
          <div class="col-12 col-md-6">
            <AppSelect
              label="Default store for inquiries"
              hint="Where a request goes when no address is collected, or routing finds no store."
              :model-value="form.defaultStoreId"
              :options="storeOptions"
              :disable="!canWrite"
              clearable
              @update:model-value="form.defaultStoreId = $event"
            />
          </div>
          <div class="col-12 col-md-6">
            <AppTextField
              label="Notify these addresses"
              hint="Extra recipients for every new inquiry, comma separated. The assigned store is always notified."
              :model-value="form.notifyEmails"
              :disable="!canWrite"
              @update:model-value="form.notifyEmails = $event"
            />
          </div>
          <div class="col-12 col-md-6">
            <AppTextField
              label="Reassurance note"
              hint="Shown under the submit button so buyers know they are not being charged."
              :model-value="form.submitNote"
              :disable="!canWrite"
              maxlength="500"
              @update:model-value="form.submitNote = $event"
            />
          </div>
        </div>

        <q-separator class="q-my-md" />

        <q-toggle
          v-model="form.showPrices"
          :disable="!canWrite || form.mode !== 'InquiryOnly'"
          label="Show prices on the storefront"
        />
        <div class="text-body2 text-muted q-mb-md">
          Only applies to inquiry-only mode. Turn it off to run a catalogue with no prices at all;
          in Standard mode prices always show.
        </div>

        <q-toggle
          v-model="form.collectAddress"
          :disable="!canWrite"
          label="Ask for a delivery address on the inquiry form"
        />
        <div class="text-body2 text-muted">
          Off collects name, email and phone only. The address is what routes a request to the nearest
          store, so leave it on if you run more than one.
        </div>
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
          @click="confirmSave"
        />
      </div>
    </template>
  </q-page>
</template>

<script setup>
/*
 * Commerce Mode (REQ-INQ-001).
 *
 * Switches the tenant between the full commerce flow and an inquiry-only catalogue, and configures
 * how inquiries behave in either mode. Switching to InquiryOnly stops the storefront taking payment
 * at all — gateways, carriers and tax providers are never called — so the save is confirmed first,
 * the same friction a destructive action gets.
 */
import { ref, computed, onMounted } from 'vue'
import { commerceApi } from 'modules/settings/api'
import { storeApi } from 'modules/stores/api'
import { usePermissions, Permissions } from 'composables/usePermissions'
import { useNotify } from 'composables/useNotify'
import { confirmation } from 'src/dialogs/confirmation'
import { getApiErrorMessage } from 'services/api'

const { has } = usePermissions()
const notify = useNotify()
const canWrite = computed(() => has(Permissions.SettingsWrite))

const MODES = [
  {
    value: 'Standard',
    label: 'Standard store',
    icon: 'o_shopping_cart',
    description: 'Buyers add to cart, choose delivery and pay online.',
    bulletIcon: 'o_check_circle',
    bulletColor: 'positive',
    bullets: [
      'Payment gateways',
      'Shipping rates and carriers',
      'Tax calculation',
      'Individual products can still be quote-only'
    ]
  },
  {
    value: 'InquiryOnly',
    label: 'Inquiry only',
    icon: 'o_contact_support',
    description: 'Buyers submit a request and your team replies with a quote. No money is taken online.',
    bulletIcon: 'o_do_not_disturb_on',
    bulletColor: 'grey-6',
    bullets: [
      'No payment step',
      'No shipping rates',
      'No tax calculation',
      'Every product is quote-only'
    ]
  }
]

const loading = ref(true)
const saving = ref(false)
const stores = ref([])
const original = ref(null)
const form = ref({
  mode: 'Standard',
  showPrices: true,
  collectAddress: true,
  inquiryButtonLabel: 'Request a Quote',
  defaultStoreId: null,
  notifyEmails: '',
  submitNote: ''
})

const storeOptions = computed(() => stores.value.map((s) => ({ label: s.name, value: s.id })))

const dirty = computed(() => JSON.stringify(form.value) !== JSON.stringify(original.value))

function apply (dto) {
  form.value = {
    mode: dto.mode || 'Standard',
    showPrices: dto.showPrices !== false,
    collectAddress: dto.collectAddress !== false,
    inquiryButtonLabel: dto.inquiryButtonLabel || 'Request a Quote',
    defaultStoreId: dto.defaultStoreId || null,
    notifyEmails: dto.notifyEmails || '',
    submitNote: dto.submitNote || ''
  }
  original.value = { ...form.value }
}

function reset () {
  if (original.value) form.value = { ...original.value }
}

async function load () {
  loading.value = true
  try {
    const [mode, storeList] = await Promise.all([
      commerceApi.getMode(),
      // Only used to populate the fallback-store picker; a failure must not block the page.
      storeApi.list({ page: 1, pageSize: 200 }).catch(() => null)
    ])
    apply(mode || {})
    stores.value = (storeList && (storeList.items || storeList)) || []
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    loading.value = false
  }
}

async function confirmSave () {
  // Turning payment off (or back on) changes what the storefront can do for every buyer, so make it
  // a deliberate act rather than a quiet save.
  if (original.value && form.value.mode !== original.value.mode) {
    const toInquiry = form.value.mode === 'InquiryOnly'
    const ok = await confirmation({
      title: toInquiry ? 'Switch to inquiry only?' : 'Switch to a standard store?',
      message: toInquiry
        ? 'Your storefront will stop accepting online payment. Buyers will submit quote requests instead, and payment, shipping and tax will be hidden. Orders already placed are unaffected.'
        : 'Your storefront will start accepting online payment again, with shipping and tax calculated at checkout. Inquiries already submitted are unaffected.',
      okLabel: 'Switch mode'
    })
    if (!ok) return
  }
  await save()
}

async function save () {
  saving.value = true
  try {
    const dto = await commerceApi.updateMode({
      mode: form.value.mode,
      showPrices: form.value.showPrices,
      collectAddress: form.value.collectAddress,
      inquiryButtonLabel: form.value.inquiryButtonLabel || null,
      defaultStoreId: form.value.defaultStoreId || null,
      notifyEmails: form.value.notifyEmails || null,
      submitNote: form.value.submitNote || null
    })
    apply(dto || {})
    notify.success('Commerce mode saved.')
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    saving.value = false
  }
}

onMounted(load)
</script>

<style scoped>
.cm-mode {
  transition: border-color 0.15s ease, box-shadow 0.15s ease;
}
.cm-mode--active {
  border-color: var(--brand-primary, var(--q-primary));
  box-shadow: 0 0 0 1px var(--brand-primary, var(--q-primary));
}
</style>
