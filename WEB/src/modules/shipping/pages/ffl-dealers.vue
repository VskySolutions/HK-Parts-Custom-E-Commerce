<template>
  <q-page class="app-page">
    <AppListHeader
      title="FFL Dealers"
      subtitle="The licensed dealer directory buyers search at checkout, and the FFL shipping settings."
      :breadcrumbs="[{ label: 'Home', icon: 'o_home', to: '/dashboard' }, { label: 'Shipping', to: '/shipping' }, { label: 'FFL Dealers' }]"
    >
      <template v-if="tab === 'directory'" #actions>
        <q-input v-model="search" dense outlined debounce="400" placeholder="Name, city, ZIP, key or FFL number" style="min-width: 280px">
          <template #prepend><q-icon name="o_search" /></template>
          <template v-if="search" #append><q-icon name="o_close" class="cursor-pointer" @click="search = ''" /></template>
        </q-input>
      </template>
    </AppListHeader>

    <q-tabs v-model="tab" dense align="left" no-caps class="q-mb-md text-primary">
      <q-tab name="directory" label="Directory" />
      <q-tab name="settings" label="Settings" />
    </q-tabs>

    <template v-if="tab === 'directory'">
      <!-- Directory health -->
      <div class="row q-col-gutter-md q-mb-md">
        <div v-for="s in statTiles" :key="s.label" class="col-6 col-md-3">
          <q-card flat bordered>
            <q-card-section class="q-py-sm">
              <div class="text-caption text-grey-7">{{ s.label }}</div>
              <div class="text-h6" :class="s.warn ? 'text-negative' : ''">{{ s.value }}</div>
            </q-card-section>
          </q-card>
        </div>
      </div>

      <AppSection :title="showImport ? 'Import the ATF licensee list' : 'Dealer locations'">
        <!-- ATF import: hidden for now (set SHOW_ATF_IMPORT to bring it back; the API endpoint is unchanged). -->
        <template v-if="showImport">
        <div class="text-body2 text-muted q-mb-sm">
          Upload the monthly ATF "Federal Firearms Listings" file (tab delimited, LIC_REGN … VOICE_PHONE) or a CSV
          export of the old site's dealer table. Dealers are matched by region + district + sequence; your
          "Allows Transfers" and fee data are kept. Last import: {{ stats?.lastImportedOnUtc ? formatDate(stats.lastImportedOnUtc) : 'never' }}.
        </div>
        <div class="row q-col-gutter-md items-center">
          <div class="col-12 col-md-6">
            <q-file v-model="importFile" outlined dense label="Choose file" accept=".txt,.csv,.tsv" clearable>
              <template #prepend><q-icon name="o_upload_file" /></template>
            </q-file>
          </div>
          <div class="col-12 col-md-3">
            <q-checkbox v-model="deactivateMissing" label="Deactivate dealers not in the file" dense />
          </div>
          <div class="col-12 col-md-3 row q-gutter-sm justify-end">
            <q-btn unelevated no-caps color="primary" icon="o_upload" label="Import" :loading="importing" :disable="!importFile || !canWrite" @click="runImport" />
          </div>
        </div>
        <q-banner v-if="importResult" dense class="bg-blue-1 text-blue-10 rounded-borders q-mt-md">
          Read {{ importResult.rowsRead }} rows: {{ importResult.inserted }} added, {{ importResult.updated }} updated,
          {{ importResult.skipped }} skipped<span v-if="importResult.deactivated">, {{ importResult.deactivated }} deactivated</span>.
          {{ importResult.withoutGeocode }} active dealers still need a location.
          <ul v-if="importResult.errors && importResult.errors.length" class="q-my-xs q-pl-md">
            <li v-for="(e, i) in importResult.errors" :key="i">{{ e }}</li>
          </ul>
        </q-banner>

        <q-separator class="q-my-md" />
        </template>
        <div class="row items-center q-gutter-md">
          <div class="col text-body2 text-muted">
            Dealers without a location never appear in a radius search. Geocoding gives each one the centre of its
            premise ZIP (500 ZIPs per run<span v-if="!stats?.geocoderConfigured">; needs a Geocodio API key in Settings</span>).
          </div>
          <q-btn outline no-caps color="primary" icon="o_place" label="Geocode missing" :loading="geocoding" :disable="!canWrite || !stats?.missingGeocode" @click="runGeocode" />
        </div>
        <div v-if="geocodeResult" class="text-body2 q-mt-sm">
          {{ geocodeResult.zipsResolved }} ZIPs located ({{ geocodeResult.dealersUpdated }} dealers), {{ geocodeResult.zipsNotFound }} not found,
          {{ geocodeResult.remainingWithoutGeocode }} dealers still without a location.
          <span v-if="geocodeResult.message" class="text-negative">{{ geocodeResult.message }}</span>
        </div>
      </AppSection>

      <div class="row q-gutter-md items-center q-mb-sm">
        <AppSelect v-model="stateFilter" label="State" :options="stateOptions" clearable style="min-width: 160px" dense />
        <q-checkbox v-model="activeOnly" label="Active only" dense />
        <q-space />
        <q-btn unelevated no-caps color="primary" icon="o_add" label="Add dealer" :disable="!canWrite" @click="createOpen = true">
          <q-tooltip>Add a dealer that is not in the ATF import yet</q-tooltip>
        </q-btn>
      </div>

      <AppDataTable
        page-key="admin-ffl-dealers"
        row-key="key"
        title="Dealers"
        :rows="rows"
        :columns="columns"
        :loading="loading"
        :pagination="pagination"
        show-actions
        :no-data-label="showImport ? 'No dealers. Import the ATF licensee list to fill the directory.' : 'No dealers. Use Add dealer to add one.'"
        @request="onRequest"
        @refresh="reload"
      >
        <template #body-cell-name="cell">
          <q-td :props="cell">
            <div class="text-weight-medium">
              {{ cell.row.name }}
              <q-badge v-if="cell.row.isManual" color="orange-2" text-color="orange-10" label="Manual" class="q-ml-xs">
                <q-tooltip>Added by hand; not yet confirmed by an ATF import</q-tooltip>
              </q-badge>
            </div>
            <div class="text-caption text-grey-7">{{ cell.row.street }}, {{ cell.row.city }} {{ cell.row.state }} {{ cell.row.zip }}</div>
          </q-td>
        </template>
        <template #body-cell-license="cell">
          <q-td :props="cell">
            <div>{{ cell.row.licenseNumber }}</div>
            <div class="text-caption" :class="cell.row.licenseExpired ? 'text-negative' : 'text-grey-7'">
              Type {{ cell.row.licenseType }}<span v-if="cell.row.licenseExpires"> · expires {{ cell.row.licenseExpires }}</span>
            </div>
          </q-td>
        </template>
        <template #body-cell-transfers="cell">
          <q-td :props="cell">
            <q-badge v-if="!cell.row.allowsTransfers" color="negative" label="No transfers" />
            <q-badge v-else-if="cell.row.allowsTransfersRecorded" color="positive" label="Yes" />
            <span v-else class="text-grey-6">Not recorded</span>
            <div v-if="cell.row.lastTransferFee != null" class="text-caption text-grey-7">Fee ${{ Number(cell.row.lastTransferFee).toFixed(2) }}</div>
          </q-td>
        </template>
        <template #body-cell-status="cell">
          <q-td :props="cell">
            <q-badge v-if="!cell.row.isActive" color="grey" label="Inactive" />
            <q-badge v-else-if="!cell.row.hasGeocode" color="warning" text-color="dark" label="No location" />
            <q-badge v-else-if="!cell.row.isEligible" color="negative" label="Not eligible" />
            <q-badge v-else color="positive" label="Searchable" />
          </q-td>
        </template>
        <template #actions="{ row }">
          <q-btn flat round dense icon="o_edit_note" :disable="!canEditDealerData" @click="openDealerData(row)"><q-tooltip>Update dealer data</q-tooltip></q-btn>
          <q-btn flat round dense icon="o_map" :href="row.mapUrl" target="_blank" rel="noopener noreferrer"><q-tooltip>View map</q-tooltip></q-btn>
        </template>
      </AppDataTable>
    </template>

    <!-- Settings -->
    <template v-else>
      <q-inner-loading :showing="settingsLoading" color="primary" />
      <template v-if="settings">
        <AppSection title="FFL shipping">
          <q-toggle v-model="settings.enabled" :disable="!canWrite" label="Firearms enabled: FFL-required products ship to a licensed dealer" />
          <div class="text-body2 text-muted q-mb-md">
            When off, products marked "FFL Required" check out like any other product and no dealer is asked for.
          </div>
          <q-toggle v-model="settings.rememberLastDealer" :disable="!canWrite" label="Pre-select the dealer from a signed-in buyer's last firearm order" />
        </AppSection>

        <AppSection title="Checkout text">
          <AppTextField v-model="settings.introText" label="Intro text above the dealer search" type="textarea" autogrow maxlength="2000" :disable="!canWrite" />
          <AppTextField
            v-model="settings.transferWarning"
            class="q-mt-md"
            label="Warning under the selected dealer"
            hint="Shown in red once a dealer is selected. Tell buyers where their dealer should send a copy of the licence."
            type="textarea"
            autogrow
            maxlength="2000"
            :disable="!canWrite"
          />
        </AppSection>

        <AppSection title="Search">
          <div class="row q-col-gutter-md">
            <div class="col-12 col-md-4">
              <AppTextField v-model="checkoutRadiiText" label="Checkout radius options (miles)" hint="Comma separated; the first is the default. Old site: 15, 25, 50, 100, 150." :disable="!canWrite" />
            </div>
            <div class="col-12 col-md-4">
              <AppTextField v-model="publicRadiiText" label="Dealer finder radius options (miles)" hint="The public /shop/ffl-search page. Old site: 5, 10, 15, 25, 50." :disable="!canWrite" />
            </div>
            <div class="col-12 col-md-4">
              <AppTextField
                label="Most dealers per search"
                type="number"
                :model-value="settings.maxResults"
                :disable="!canWrite"
                @update:model-value="settings.maxResults = Number($event) || 200"
              />
            </div>
          </div>
        </AppSection>

        <AppSection title="ZIP geocoding (Geocodio)">
          <div class="text-body2 text-muted q-mb-sm">
            Finds the centre of the buyer's ZIP for the radius search and locates imported dealers. Without a key the
            search uses the positions of the dealers already in the directory.
            <b>{{ settings.geocodioConfigured ? 'A key is saved.' : 'No key saved.' }}</b>
          </div>
          <div class="row q-col-gutter-md items-center">
            <div class="col-12 col-md-6">
              <AppTextField v-model="geocodioKey" label="Geocodio API key" type="password" autocomplete="off" hint="Leave empty to keep the saved key." :disable="!canWrite" />
            </div>
            <div class="col-12 col-md-6">
              <q-checkbox v-if="settings.geocodioConfigured" v-model="clearKey" label="Remove the saved key" :disable="!canWrite" />
            </div>
          </div>
        </AppSection>

        <div class="row justify-end q-gutter-sm q-mb-lg">
          <q-btn flat no-caps label="Reset" :disable="savingSettings" @click="loadSettings" />
          <q-btn unelevated no-caps color="primary" label="Save changes" :loading="savingSettings" :disable="!canWrite" @click="saveSettings" />
        </div>
      </template>
    </template>

    <FflDealerDataDialog v-model="dealerDataOpen" :dealer="dealerDataTarget" @saved="onDealerDataSaved" />
    <FflDealerCreateDialog v-model="createOpen" :state-options="stateOptions" @saved="onDealerCreated" />
  </q-page>
</template>

<script setup>
/*
 * FFL dealer directory admin (docs/ffl-shipping-information-requirements.md §5.10, §6, FFL-113): directory
 * import from the ATF licensee list, geocoding, dealer data (Allows Transfers / fee) and the FFL settings.
 */
import { ref, computed, watch, onMounted } from 'vue'
import { date } from 'quasar'
import { fflAdminApi } from 'modules/shipping/ffl-api'
import FflDealerDataDialog from 'modules/shipping/components/FflDealerDataDialog.vue'
import FflDealerCreateDialog from 'modules/shipping/components/FflDealerCreateDialog.vue'
import { getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'
import { usePermissions } from 'composables/usePermissions'

const notify = useNotify()
const { hasAny, Permissions } = usePermissions()
const canWrite = computed(() => hasAny([Permissions.StoresWrite]))
const canEditDealerData = computed(() => hasAny([Permissions.StoresWrite, Permissions.OrdersWrite]))

// The ATF licensee-list import is hidden for now; flip to true to show it again.
const SHOW_ATF_IMPORT = false
const showImport = SHOW_ATF_IMPORT

const US_STATES = ['AK', 'AL', 'AR', 'AZ', 'CA', 'CO', 'CT', 'DC', 'DE', 'FL', 'GA', 'GU', 'HI', 'IA', 'ID', 'IL', 'IN', 'KS', 'KY', 'LA', 'MA', 'MD', 'ME', 'MI', 'MN', 'MO', 'MP', 'MS', 'MT', 'NC', 'ND', 'NE', 'NH', 'NJ', 'NM', 'NV', 'NY', 'OH', 'OK', 'OR', 'PA', 'PR', 'RI', 'SC', 'SD', 'TN', 'TX', 'UT', 'VA', 'VI', 'VT', 'WA', 'WI', 'WV', 'WY']
const stateOptions = US_STATES.map((s) => ({ label: s, value: s }))

const columns = [
  { name: 'name', label: 'Dealer', field: 'name', align: 'left' },
  { name: 'license', label: 'Licence', field: 'licenseNumber', align: 'left' },
  { name: 'phone', label: 'Phone', field: 'phoneDisplay', align: 'left' },
  { name: 'transfers', label: 'Transfers', field: 'allowsTransfers', align: 'left' },
  { name: 'status', label: 'Status', field: 'isEligible', align: 'center' }
]

const tab = ref('directory')
const search = ref('')
const stateFilter = ref(null)
const activeOnly = ref(true)
const rows = ref([])
const loading = ref(false)
const pagination = ref({ page: 1, rowsPerPage: 25, rowsNumber: 0 })
const stats = ref(null)

const importFile = ref(null)
const deactivateMissing = ref(false)
const importing = ref(false)
const importResult = ref(null)
const geocoding = ref(false)
const geocodeResult = ref(null)

const createOpen = ref(false)
const dealerDataOpen = ref(false)
const dealerDataTarget = ref(null)

const statTiles = computed(() => [
  { label: 'Dealers', value: stats.value?.total ?? '—' },
  { label: 'Active', value: stats.value?.active ?? '—' },
  { label: 'Without location', value: stats.value?.missingGeocode ?? '—', warn: (stats.value?.missingGeocode || 0) > 0 },
  { label: 'Refusing transfers', value: stats.value?.transfersRefused ?? '—' }
])

function formatDate (v) {
  if (!v) return ''
  const iso = /(Z|[+-]\d\d:\d\d)$/.test(v) ? v : v + 'Z'
  return date.formatDate(iso, 'MMM D, YYYY h:mm A')
}

async function loadStats () {
  try {
    stats.value = await fflAdminApi.stats()
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  }
}

async function fetch (props) {
  const p = props?.pagination || pagination.value
  loading.value = true
  try {
    const r = await fflAdminApi.list({
      search: search.value.trim() || undefined,
      state: stateFilter.value || undefined,
      activeOnly: activeOnly.value || undefined,
      page: p.page,
      pageSize: p.rowsPerPage
    })
    rows.value = r?.items || []
    pagination.value = { ...p, rowsNumber: r?.totalCount ?? rows.value.length }
  } catch (err) {
    rows.value = []
    notify.error(getApiErrorMessage(err))
  } finally {
    loading.value = false
  }
}
function onRequest (props) { fetch(props) }
function reload () { fetch({ pagination: { ...pagination.value, page: 1 } }) }

watch([search, stateFilter, activeOnly], reload)

async function runImport () {
  if (!importFile.value) return
  importing.value = true
  importResult.value = null
  try {
    importResult.value = await fflAdminApi.importFile(importFile.value, deactivateMissing.value)
    notify.success('Directory imported.')
    importFile.value = null
    await Promise.all([loadStats(), fetch()])
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    importing.value = false
  }
}

async function runGeocode () {
  geocoding.value = true
  try {
    geocodeResult.value = await fflAdminApi.geocode(500)
    await Promise.all([loadStats(), fetch()])
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    geocoding.value = false
  }
}

function openDealerData (row) {
  dealerDataTarget.value = row
  dealerDataOpen.value = true
}
function onDealerDataSaved (updated) {
  const i = rows.value.findIndex((r) => r.key === updated.key)
  if (i >= 0) rows.value.splice(i, 1, updated)
  loadStats()
}

function onDealerCreated (dealer) {
  // Show the new dealer: search the grid by its licence number.
  search.value = dealer.licenseNumber
  loadStats()
}

// ---- Settings ----
const settings = ref(null)
const settingsLoading = ref(false)
const savingSettings = ref(false)
const checkoutRadiiText = ref('')
const publicRadiiText = ref('')
const geocodioKey = ref('')
const clearKey = ref(false)

function parseRadii (text) {
  return (text || '').split(',').map((v) => parseInt(v.trim(), 10)).filter((n) => Number.isFinite(n) && n > 0)
}

async function loadSettings () {
  settingsLoading.value = true
  try {
    const s = await fflAdminApi.getSettings()
    settings.value = s
    checkoutRadiiText.value = (s.checkoutRadii || []).join(', ')
    publicRadiiText.value = (s.publicRadii || []).join(', ')
    geocodioKey.value = ''
    clearKey.value = false
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    settingsLoading.value = false
  }
}

async function saveSettings () {
  savingSettings.value = true
  try {
    const s = settings.value
    settings.value = await fflAdminApi.updateSettings({
      enabled: s.enabled,
      introText: s.introText,
      transferWarning: s.transferWarning,
      checkoutRadii: parseRadii(checkoutRadiiText.value),
      publicRadii: parseRadii(publicRadiiText.value),
      maxResults: s.maxResults,
      rememberLastDealer: s.rememberLastDealer,
      geocodioApiKey: geocodioKey.value.trim() || null,
      clearGeocodioApiKey: clearKey.value
    })
    checkoutRadiiText.value = (settings.value.checkoutRadii || []).join(', ')
    publicRadiiText.value = (settings.value.publicRadii || []).join(', ')
    geocodioKey.value = ''
    clearKey.value = false
    notify.success('FFL settings saved.')
    loadStats()
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    savingSettings.value = false
  }
}

watch(tab, (t) => { if (t === 'settings' && !settings.value) loadSettings() })

onMounted(() => {
  loadStats()
  fetch()
})
</script>
