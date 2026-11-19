<template>
  <!-- Public FFL dealer finder (docs/ffl-shipping-information-requirements.md FFL-100/101). No selection, no
       auto-search; results come from our API (no directory credentials in the page) and render as text. -->
  <q-page class="q-pa-md storefront-container">
    <div class="text-h5 text-weight-bold q-mb-xs">FFL Dealers Search</div>
    <div class="text-body2 text-grey-8 q-mb-md">Find a federally licensed firearms dealer near you.</div>

    <q-banner v-if="config && !config.enabled" dense class="bg-grey-2 rounded-borders q-mb-md">
      The dealer search is not available right now.
    </q-banner>

    <q-card v-else flat bordered class="q-mb-lg">
      <q-card-section>
        <div class="row q-col-gutter-sm items-start">
          <div class="col-12 col-sm-5">
            <q-input
              v-model="zip"
              outlined
              dense
              label="Zip Code"
              maxlength="15"
              inputmode="numeric"
              autocomplete="postal-code"
              @keydown.enter.prevent="search"
            />
          </div>
          <div class="col-7 col-sm-4">
            <q-select v-model="radius" outlined dense emit-value map-options label="Search Radius" :options="radiusOptions" />
          </div>
          <div class="col-5 col-sm-3">
            <q-btn
              unelevated
              no-caps
              color="primary"
              class="full-width"
              style="min-height: 40px"
              icon="o_search"
              label="Search Dealers"
              :loading="searching"
              @click="search"
            />
          </div>
        </div>
      </q-card-section>

      <q-separator />

      <q-card-section>
        <q-banner v-if="error" dense class="bg-red-1 text-red-9 rounded-borders q-mb-sm">
          <template #avatar><q-icon name="o_error" color="red-9" /></template>
          {{ error }}
        </q-banner>
        <div v-else-if="!searched" class="text-body2 text-grey-7">Please enter your search criteria</div>
        <div v-else-if="message" class="text-body2 text-grey-7 q-mb-sm">{{ message }}</div>

        <div v-if="dealers.length" class="text-body2 text-weight-medium q-mb-sm" aria-live="polite">
          {{ dealers.length }} {{ dealers.length === 1 ? 'dealer' : 'dealers' }} found
          <span v-if="truncated" class="text-grey-7">(showing the nearest {{ dealers.length }} of {{ totalFound }})</span>
        </div>

        <q-markup-table v-if="dealers.length" flat bordered dense class="gt-xs">
          <thead>
            <tr>
              <th class="text-left">Distance</th>
              <th class="text-left">Name</th>
              <th class="text-left">City</th>
              <th class="text-left">State</th>
              <th class="text-left">Map</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="d in dealers" :key="d.key">
              <td>{{ milesText(d.distanceMiles) }}</td>
              <td>
                <div class="text-weight-medium">{{ d.name }}</div>
                <div class="text-caption text-grey-7">{{ d.street }}<span v-if="d.phoneDisplay"> · {{ d.phoneDisplay }}</span></div>
              </td>
              <td>{{ d.city }}</td>
              <td>{{ d.state }}</td>
              <td><a :href="d.mapUrl" target="_blank" rel="noopener noreferrer" class="text-primary">View Map</a></td>
            </tr>
          </tbody>
        </q-markup-table>

        <!-- Phones: one card per dealer (FFL-135). -->
        <q-list v-if="dealers.length" bordered separator class="lt-sm rounded-borders">
          <q-item v-for="d in dealers" :key="'m-' + d.key">
            <q-item-section>
              <q-item-label class="text-weight-medium">{{ d.name }}</q-item-label>
              <q-item-label caption>{{ d.street }}, {{ d.city }} {{ d.state }}</q-item-label>
              <q-item-label caption>{{ milesText(d.distanceMiles) }}<span v-if="d.phoneDisplay"> · {{ d.phoneDisplay }}</span></q-item-label>
            </q-item-section>
            <q-item-section side>
              <a :href="d.mapUrl" target="_blank" rel="noopener noreferrer" class="text-primary">View Map</a>
            </q-item-section>
          </q-item>
        </q-list>
      </q-card-section>
    </q-card>
  </q-page>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { fflApi } from 'modules/storefront/api'
import { getApiErrorMessage } from 'services/api'

const config = ref(null)
const zip = ref('')
const radius = ref(null)
const dealers = ref([])
const totalFound = ref(0)
const truncated = ref(false)
const searching = ref(false)
const searched = ref(false)
const message = ref('')
const error = ref('')

const radiusOptions = computed(() =>
  (config.value?.publicRadii || [5, 10, 15, 25, 50]).map((r) => ({ label: `${r} miles`, value: r }))
)

function milesText (m) {
  if (m == null) return ''
  const n = Math.round(m)
  return `${n} ${n === 1 ? 'mile' : 'miles'}`
}

async function search () {
  error.value = ''
  message.value = ''
  const z = (zip.value || '').trim().slice(0, 5)
  if (!/^[0-9]{5}$/.test(z)) {
    dealers.value = []
    error.value = 'Please enter a valid 5-digit ZIP code.'
    return
  }
  searching.value = true
  try {
    const r = await fflApi.searchPublic(z, radius.value)
    dealers.value = r.dealers || []
    totalFound.value = r.totalFound || 0
    truncated.value = !!r.truncated
    message.value = r.message || ''
    searched.value = true
  } catch (err) {
    dealers.value = []
    error.value = [400, 429].includes(err?.response?.status)
      ? getApiErrorMessage(err)
      : "We couldn't search dealers right now. Please try again."
  } finally {
    searching.value = false
  }
}

onMounted(async () => {
  try {
    config.value = await fflApi.config()
  } catch (e) {
    config.value = { enabled: true, publicRadii: [5, 10, 15, 25, 50] }
  }
  radius.value = (config.value.publicRadii || [5])[0]
})
</script>

<style scoped>
.storefront-container {
  max-width: 1200px;
  margin: 0 auto;
}
</style>
