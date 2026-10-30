<template>
  <q-dialog :model-value="modelValue" @update:model-value="$emit('update:modelValue', $event)">
    <q-card style="min-width: 560px; max-width: 95vw">
      <q-card-section class="row items-center">
        <div class="text-subtitle1 text-weight-medium col">Delivery zones — {{ store?.name }}</div>
        <q-btn flat round dense icon="o_close" v-close-popup />
      </q-card-section>
      <q-separator />

      <q-card-section>
        <q-inner-loading :showing="loading" />
        <q-markup-table v-if="zones.length" flat dense class="q-mb-md">
          <thead>
            <tr><th class="text-left">Name</th><th class="text-left">Country</th><th class="text-left">Region</th><th class="text-left">Postal range</th><th></th></tr>
          </thead>
          <tbody>
            <tr v-for="z in zones" :key="z.id">
              <td>{{ z.name }} <q-badge v-if="!z.isActive" color="grey" label="off" /></td>
              <td>{{ z.countryCode }}</td>
              <td>{{ z.region || '—' }}</td>
              <td>{{ z.postalCodeStart ? `${z.postalCodeStart}–${z.postalCodeEnd || ''}` : '—' }}</td>
              <td class="text-right"><q-btn flat round dense size="sm" icon="o_delete" color="negative" @click="remove(z)" /></td>
            </tr>
          </tbody>
        </q-markup-table>
        <div v-else-if="!loading" class="text-grey-6 q-mb-md">No delivery zones yet.</div>

        <div class="text-caption text-grey-7 q-mb-xs">Add a zone</div>
        <div class="row q-col-gutter-sm">
          <div class="col-6"><q-input v-model="draft.name" dense outlined label="Name" /></div>
          <div class="col-3"><q-input v-model="draft.countryCode" dense outlined label="Country" maxlength="2" placeholder="US" /></div>
          <div class="col-3"><q-input v-model="draft.region" dense outlined label="Region" /></div>
          <div class="col-6"><q-input v-model="draft.postalCodeStart" dense outlined label="Postal from" /></div>
          <div class="col-6"><q-input v-model="draft.postalCodeEnd" dense outlined label="Postal to" /></div>
        </div>
      </q-card-section>

      <q-separator />
      <q-card-actions align="right">
        <q-btn flat no-caps label="Close" v-close-popup />
        <q-btn color="primary" unelevated no-caps label="Add zone" :loading="adding" @click="add" />
      </q-card-actions>
    </q-card>
  </q-dialog>
</template>

<script setup>
/* DeliveryZonesDialog (WO-113): per-store add/remove of delivery zones (country / region / postal range). */
import { ref, reactive, watch } from 'vue'
import { getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'
import { deleteConfirmation } from 'dialogs/delete_confirmation'
import { deliveryZoneApi } from 'modules/stores/api'

const props = defineProps({
  modelValue: { type: Boolean, default: false },
  store: { type: Object, default: null }
})
defineEmits(['update:modelValue'])

const notify = useNotify()
const zones = ref([])
const loading = ref(false)
const adding = ref(false)
const draft = reactive({ name: '', countryCode: '', region: '', postalCodeStart: '', postalCodeEnd: '' })

watch(() => [props.modelValue, props.store?.id], ([open]) => {
  if (open && props.store?.id) load()
})

async function load () {
  loading.value = true
  try {
    const res = await deliveryZoneApi.list(props.store.id)
    zones.value = Array.isArray(res) ? res : res?.items || []
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    loading.value = false
  }
}

async function add () {
  if (!draft.name || !draft.countryCode) { notify.warning('Name and country are required'); return }
  adding.value = true
  try {
    await deliveryZoneApi.create(props.store.id, {
      name: draft.name.trim(),
      countryCode: draft.countryCode.toUpperCase(),
      region: draft.region || null,
      postalCodeStart: draft.postalCodeStart || null,
      postalCodeEnd: draft.postalCodeEnd || null,
      isActive: true
    })
    Object.assign(draft, { name: '', countryCode: '', region: '', postalCodeStart: '', postalCodeEnd: '' })
    await load()
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    adding.value = false
  }
}

async function remove (zone) {
  if (!(await deleteConfirmation(zone.name ? `the "${zone.name}" delivery zone` : 'this delivery zone', { title: 'Remove', okLabel: 'Remove' }))) return
  try {
    await deliveryZoneApi.remove(props.store.id, zone.id)
    await load()
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  }
}
</script>
