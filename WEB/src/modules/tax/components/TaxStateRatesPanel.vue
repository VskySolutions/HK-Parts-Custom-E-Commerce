<template>
  <AppSection title="Rates by state">
    <template #actions>
      <q-input v-model="filter" dense outlined clearable placeholder="Find a state" style="width: 200px">
        <template #prepend><q-icon name="o_search" /></template>
      </q-input>
    </template>
    <template #footer>
      <div class="text-caption text-grey-7 col">Rates are percentages (6.5 = 6.5%). Leave blank for no tax in that state.</div>
      <q-btn color="primary" unelevated no-caps icon="o_save" label="Save Rates" :loading="saving" @click="save" />
    </template>
    <q-inner-loading :showing="loading" />
    <div class="row q-col-gutter-sm">
      <div v-for="s in visible" :key="s.stateCode" class="col-12 col-sm-6 col-md-4 col-lg-3">
        <q-input
          v-model="s.ratePercent"
          dense
          outlined
          type="number"
          step="0.0001"
          min="0"
          max="100"
          :label="`${s.stateName} (${s.stateCode})`"
          suffix="%"
          :error="invalid(s)"
          error-message="0 to 100"
        />
      </div>
      <div v-if="!loading && !visible.length" class="col-12 text-center text-grey-6 q-pa-lg">No state matches "{{ filter }}".</div>
    </div>
  </AppSection>
</template>

<script setup>
/* PHP "State Tax Rates": one rate per state, saved together (Tax → State Rates). */
import { ref, computed, onMounted } from 'vue'
import { getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'
import { taxApi } from 'modules/tax/api'
import AppSection from 'components/common/AppSection.vue'

const notify = useNotify()
const rows = ref([])
const loading = ref(false)
const saving = ref(false)
const filter = ref('')

const visible = computed(() => {
  const f = (filter.value || '').trim().toLowerCase()
  if (!f) return rows.value
  return rows.value.filter((s) => s.stateName.toLowerCase().includes(f) || s.stateCode.toLowerCase() === f)
})

function toNumber (v) {
  if (v === '' || v === null || v === undefined) return null
  const n = Number(v)
  return Number.isFinite(n) ? n : NaN
}

function invalid (s) {
  const n = toNumber(s.ratePercent)
  return n !== null && (Number.isNaN(n) || n < 0 || n > 100)
}

function apply (list) {
  rows.value = (Array.isArray(list) ? list : []).map((s) => ({ ...s, ratePercent: s.ratePercent ?? '' }))
}

async function load () {
  loading.value = true
  try { apply(await taxApi.stateRates()) } catch (err) { notify.error(getApiErrorMessage(err)) } finally { loading.value = false }
}

async function save () {
  if (rows.value.some(invalid)) {
    notify.error('Each state rate must be a number between 0 and 100.')
    return
  }
  saving.value = true
  try {
    apply(await taxApi.saveStateRates(rows.value.map((s) => ({ stateCode: s.stateCode, ratePercent: toNumber(s.ratePercent) }))))
    notify.success('State tax rates saved')
  } catch (err) { notify.error(getApiErrorMessage(err)) } finally { saving.value = false }
}

onMounted(load)
</script>
