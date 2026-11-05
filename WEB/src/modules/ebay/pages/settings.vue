<template>
  <q-page class="app-page">
    <AppListHeader
      title="eBay Settings"
      subtitle="Connect a store to eBay, choose listing defaults and keep eBay reference data current."
      :breadcrumbs="[{ label: 'Home', icon: 'o_home', to: '/dashboard' }, { label: 'eBay Settings' }]"
    >
      <template #actions>
        <EbayStorePicker @change="load" />
      </template>
    </AppListHeader>

    <q-inner-loading :showing="loading" />

    <template v-if="settings">
      <!-- ============ CONNECTION ============ -->
      <q-card flat bordered class="app-section q-mb-md">
        <q-card-section class="row items-center q-gutter-sm">
          <div class="text-subtitle1 text-weight-medium">Connection</div>
          <q-chip :color="connection.color" text-color="white" :icon="connection.icon" dense square>{{ connection.label }}</q-chip>
          <span v-if="settings.connectedAtUtc" class="text-caption text-grey-7">since {{ formatDate(settings.connectedAtUtc) }}</span>
          <q-space />
          <template v-if="canWrite">
            <q-btn
              v-if="settings.connectionState === 'NotConnected'"
              unelevated no-caps color="primary" icon="o_link" label="Connect to eBay"
              :loading="connecting" :disable="!settings.appCredentialsConfigured" @click="connect"
            />
            <template v-else>
              <q-btn outline no-caps color="primary" icon="o_sync" label="Reconnect" :loading="connecting"
                :disable="!settings.appCredentialsConfigured" @click="connect" />
              <q-btn flat no-caps color="negative" icon="o_link_off" label="Disconnect" @click="disconnect" />
            </template>
          </template>
        </q-card-section>
        <q-banner v-if="!settings.appCredentialsConfigured" class="bg-orange-1 text-orange-10">
          The eBay {{ settings.environment }} app credentials are not configured on the server. Set
          <code>Ebay:{{ settings.environment }}:ClientId</code>, <code>ClientSecret</code> and <code>RuName</code>
          in User Secrets / environment variables / Key Vault. The RuName's accept URL must point at
          <code>/api/admin/ebay/oauth/callback</code>.
        </q-banner>
      </q-card>

      <!-- ============ SETTINGS ============ -->
      <q-card flat bordered class="app-section q-mb-md">
        <q-card-section class="text-subtitle1 text-weight-medium">Listing defaults</q-card-section>
        <q-separator />
        <q-card-section class="q-gutter-y-sm">
          <div class="row q-col-gutter-md items-center">
            <div class="col-12 col-md-4">
              <q-toggle v-model="form.isEnabled" label="eBay Enabled" :disable="!canWrite" />
            </div>
            <div class="col-12 col-md-4">
              <q-toggle v-model="form.loggingEnabled" label="API Logging" :disable="!canWrite" />
            </div>
            <div class="col-12 col-md-4">
              <q-toggle v-model="form.fitmentEnabled" label="Vehicle Fitment Enabled" :disable="!canWrite">
                <q-tooltip>Send parts compatibility for categories that support it</q-tooltip>
              </q-toggle>
            </div>
            <div class="col-12 col-md-4">
              <q-toggle v-model="form.syncPriceFromStore" label="Sync price from store" :disable="!canWrite">
                <q-tooltip>Off: listings keep the price saved on them. On: the inventory sync also pushes the store price.</q-tooltip>
              </q-toggle>
            </div>
          </div>

          <div class="row q-col-gutter-md">
            <div class="col-12 col-md-6">
              <AppSelect v-model="form.environment" label="Environment" :options="environmentOptions" emit-value map-options :disable="!canWrite"
                hint="Changing the environment or marketplace disconnects eBay" />
            </div>
            <div class="col-12 col-md-6">
              <AppSelect v-model="form.marketplaceId" label="Marketplace" :options="marketplaceOptions" emit-value map-options :disable="!canWrite" />
            </div>
          </div>

          <div>
            <AppFieldLabel label="Default Category" />
            <div class="row items-center q-gutter-sm">
              <span v-if="form.defaultCategoryId" class="text-body2">{{ defaultCategoryName || form.defaultCategoryId }} <span class="text-grey-6">#{{ form.defaultCategoryId }}</span></span>
              <span v-else class="text-grey-6">None. The listing wizard asks every time.</span>
              <q-btn v-if="canWrite" flat dense no-caps color="primary" icon="o_account_tree" label="Choose" @click="categoryDialog = true" />
              <q-btn v-if="canWrite && form.defaultCategoryId" flat dense no-caps color="grey-7" icon="o_close" label="Clear" @click="form.defaultCategoryId = null" />
            </div>
          </div>

          <div class="row q-col-gutter-md items-end">
            <div v-for="p in policyFields" :key="p.key" class="col-12 col-md-4">
              <AppSelect
                v-model="form[p.key]"
                :label="p.label"
                :options="policies[p.type] || []"
                option-value="id"
                option-label="name"
                emit-value
                map-options
                clearable
                :loading="policiesLoading"
                :disable="!canWrite || !connected"
                :hint="connected ? '' : 'Connect eBay to load policies'"
              />
            </div>
          </div>
          <div class="row justify-end">
            <q-btn flat dense no-caps color="primary" icon="o_refresh" label="Refresh policies" :disable="!connected" :loading="policiesLoading" @click="loadPolicies(true)" />
          </div>
        </q-card-section>
        <q-separator />
        <q-card-actions align="right" class="q-pa-md">
          <q-btn v-if="canWrite" unelevated no-caps color="primary" icon="o_check" label="Save settings" :loading="saving" @click="save" />
        </q-card-actions>
      </q-card>

      <!-- ============ LOCATION + REFERENCE DATA ============ -->
      <div class="row q-col-gutter-md">
        <div class="col-12 col-md-6">
          <q-card flat bordered class="app-section full-height">
            <q-card-section>
              <div class="text-subtitle1 text-weight-medium">Inventory Location</div>
              <div class="text-caption text-grey-7 q-mb-sm">eBay needs a merchant location (the store's shipper address) before an offer can be published.</div>
              <div class="text-body2">Location key: <b>{{ settings.merchantLocationKey || 'not set' }}</b></div>
            </q-card-section>
            <q-card-actions align="right">
              <q-btn v-if="canWrite" outline no-caps color="primary" icon="o_place" label="Create / update from store address"
                :disable="!connected" :loading="syncingLocation" @click="syncLocation" />
            </q-card-actions>
          </q-card>
        </div>
        <div class="col-12 col-md-6">
          <q-card flat bordered class="app-section full-height">
            <q-card-section>
              <div class="text-subtitle1 text-weight-medium">Reference data</div>
              <div class="text-caption text-grey-7 q-mb-sm">The marketplace category tree, used by the category pickers. Refreshed monthly.</div>
              <div class="text-body2">
                {{ settings.categoryCount.toLocaleString() }} categories
                <span v-if="settings.categoryTreeVersion" class="text-grey-7">(tree version {{ settings.categoryTreeVersion }})</span>
              </div>
            </q-card-section>
            <q-card-actions align="right">
              <q-btn v-if="canWrite" outline no-caps color="primary" icon="o_cloud_download" label="Refresh eBay Categories"
                :disable="!connected" :loading="refreshingCategories" @click="refreshCategories" />
            </q-card-actions>
          </q-card>
        </div>
      </div>
    </template>

    <q-dialog v-model="categoryDialog">
      <q-card style="width: 640px; max-width: 95vw">
        <q-card-section class="text-subtitle1 text-weight-medium">Default eBay category (leaf only)</q-card-section>
        <q-card-section class="q-pt-none">
          <EbayCategoryTree v-model="pickedCategory" @select="(c) => (pickedCategoryName = c.name)" />
        </q-card-section>
        <q-card-actions align="right">
          <q-btn v-close-popup flat no-caps label="Cancel" />
          <q-btn v-close-popup unelevated no-caps color="primary" label="Use this category" :disable="!pickedCategory" @click="applyCategory" />
        </q-card-actions>
      </q-card>
    </q-dialog>
  </q-page>
</template>

<script setup>
/*
 * eBay Settings (/settings/ebay): OAuth connection, listing defaults (category + business policies loaded
 * live from the Account API and cached 24h), merchant location and the category-tree refresh. Tokens never
 * reach the browser; the OAuth callback redirects back here with ?ebay=connected|declined|invalid-state|error.
 */
import { ref, reactive, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { date } from 'quasar'
import { getApiErrorMessage } from 'services/api'
import { ebayApi, notifyEbayErrors, setEbayStore } from 'modules/ebay/api'
import { usePermissions } from 'composables/usePermissions'
import { useNotify } from 'composables/useNotify'
import { confirmation } from 'dialogs/confirmation'
import AppFieldLabel from 'components/common/AppFieldLabel.vue'
import EbayCategoryTree from 'modules/ebay/components/EbayCategoryTree.vue'
import EbayStorePicker from 'modules/ebay/components/EbayStorePicker.vue'

const route = useRoute()
const router = useRouter()
const notify = useNotify()
const { has } = usePermissions()
const canWrite = computed(() => has('Ebay.Write'))

const environmentOptions = [{ label: 'Sandbox', value: 'Sandbox' }, { label: 'Production', value: 'Production' }]
const marketplaceOptions = [
  { label: 'eBay US (EBAY_US)', value: 'EBAY_US' },
  { label: 'eBay Motors US (EBAY_MOTORS_US)', value: 'EBAY_MOTORS_US' },
  { label: 'eBay Canada (EBAY_CA)', value: 'EBAY_CA' },
  { label: 'eBay UK (EBAY_GB)', value: 'EBAY_GB' },
  { label: 'eBay Australia (EBAY_AU)', value: 'EBAY_AU' },
  { label: 'eBay Germany (EBAY_DE)', value: 'EBAY_DE' }
]
const policyFields = [
  { key: 'defaultPaymentPolicyId', type: 'Payment', label: 'Default Payment Policy' },
  { key: 'defaultFulfillmentPolicyId', type: 'Fulfillment', label: 'Default Shipping Policy' },
  { key: 'defaultReturnPolicyId', type: 'Return', label: 'Default Return Policy' }
]

const settings = ref(null)
const form = reactive({})
const policies = reactive({ Payment: [], Fulfillment: [], Return: [] })
const defaultCategoryName = ref('')
const loading = ref(false)
const saving = ref(false)
const connecting = ref(false)
const policiesLoading = ref(false)
const syncingLocation = ref(false)
const refreshingCategories = ref(false)
const categoryDialog = ref(false)
const pickedCategory = ref(null)
const pickedCategoryName = ref('')

const connected = computed(() => settings.value?.connectionState === 'Connected')
const connection = computed(() => {
  const s = settings.value
  if (!s || s.connectionState === 'NotConnected') return { label: 'Not connected', color: 'grey', icon: 'o_link_off' }
  if (s.connectionState === 'TokenExpired') return { label: 'Token expired', color: 'negative', icon: 'o_error' }
  return { label: `Connected as ${s.connectedEbayUser || 'eBay seller'}`, color: 'positive', icon: 'o_check_circle' }
})

const formatDate = (v) => date.formatDate(v, 'YYYY-MM-DD HH:mm')

function fill (dto) {
  settings.value = dto
  defaultCategoryName.value = dto.defaultCategoryName || ''
  Object.assign(form, {
    isEnabled: dto.isEnabled,
    environment: dto.environment,
    marketplaceId: dto.marketplaceId,
    loggingEnabled: dto.loggingEnabled,
    defaultCategoryId: dto.defaultCategoryId,
    defaultPaymentPolicyId: dto.defaultPaymentPolicyId,
    defaultFulfillmentPolicyId: dto.defaultFulfillmentPolicyId,
    defaultReturnPolicyId: dto.defaultReturnPolicyId,
    fitmentEnabled: dto.fitmentEnabled,
    syncPriceFromStore: dto.syncPriceFromStore
  })
}

async function load () {
  loading.value = true
  try {
    fill(await ebayApi.getSettings())
    if (connected.value) loadPolicies(false)
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    loading.value = false
  }
}

async function loadPolicies (refresh) {
  policiesLoading.value = true
  try {
    const [payment, fulfillment, ret] = await Promise.all(
      ['Payment', 'Fulfillment', 'Return'].map((t) => ebayApi.policies(t, refresh)))
    Object.assign(policies, { Payment: payment, Fulfillment: fulfillment, Return: ret })
    if (refresh) notify.success('Policies refreshed from eBay')
  } catch (err) {
    notifyEbayErrors(err, 'ERROR - eBay policies could not be loaded:')
  } finally {
    policiesLoading.value = false
  }
}

async function save () {
  saving.value = true
  try {
    fill(await ebayApi.saveSettings({ ...form }))
    notify.success('eBay settings saved')
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    saving.value = false
  }
}

async function connect () {
  connecting.value = true
  try {
    const { url } = await ebayApi.connectUrl()
    window.location.assign(url)
  } catch (err) {
    notify.error(getApiErrorMessage(err))
    connecting.value = false
  }
}

async function disconnect () {
  const ok = await confirmation({
    title: 'Disconnect eBay?',
    message: 'Listings stay on eBay, but quantity sync and order import stop until you reconnect.',
    okLabel: 'Disconnect',
    color: 'negative'
  })
  if (!ok) return
  try {
    await ebayApi.disconnect()
    notify.success('eBay disconnected')
    await load()
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  }
}

async function syncLocation () {
  syncingLocation.value = true
  try {
    const { merchantLocationKey } = await ebayApi.syncLocation()
    settings.value.merchantLocationKey = merchantLocationKey
    notify.success('eBay inventory location saved')
  } catch (err) {
    if (err?.response?.status === 422) notifyEbayErrors(err, 'ERROR - eBay rejected the location:')
    else notify.error(getApiErrorMessage(err))
  } finally {
    syncingLocation.value = false
  }
}

async function refreshCategories () {
  refreshingCategories.value = true
  try {
    await ebayApi.refreshCategories()
    notify.info('Downloading the eBay category tree in the background. This can take a minute.')
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    refreshingCategories.value = false
  }
}

function applyCategory () {
  form.defaultCategoryId = pickedCategory.value
  defaultCategoryName.value = pickedCategoryName.value
}

function handleOAuthReturn () {
  const outcome = route.query.ebay
  if (!outcome) return
  if (route.query.storeId) setEbayStore(String(route.query.storeId))
  if (outcome === 'connected') notify.success('Connected to eBay')
  else if (outcome === 'declined') notify.warning('eBay connection was cancelled')
  else if (outcome === 'invalid-state') notify.error('The eBay sign-in link expired or was already used. Try Connect again.')
  else notify.error(route.query.message ? String(route.query.message) : 'eBay connection failed')
  router.replace({ name: 'ebay-settings' })
}

onMounted(() => {
  handleOAuthReturn()
  load()
})
</script>
