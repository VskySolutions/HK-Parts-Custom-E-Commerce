<template>
  <div v-if="show" class="app-record-meta">
    <q-separator class="q-mb-sm" />
    <div class="row q-col-gutter-x-lg q-col-gutter-y-xs text-caption">
      <div class="col-auto">
        <span class="app-record-meta__label">Created by</span>
        <span class="app-record-meta__value">{{ createdBy }}</span>
      </div>
      <div v-if="createdOn" class="col-auto">
        <span class="app-record-meta__label">Created on</span>
        <span class="app-record-meta__value">{{ createdOn }}</span>
      </div>
      <div class="col-auto">
        <span class="app-record-meta__label">Updated by</span>
        <span class="app-record-meta__value">{{ updatedBy }}</span>
      </div>
      <div v-if="updatedOn" class="col-auto">
        <span class="app-record-meta__label">Updated on</span>
        <span class="app-record-meta__value">{{ updatedOn }}</span>
      </div>
    </div>
  </div>
</template>

<script setup>
/*
 * AppRecordMeta — the standard audit footer for admin detail pages (Created by/on, Updated by/on).
 * Given a registered entity-type key (see the backend RecordAuditRegistry) and a record id it
 * self-fetches its own metadata and renders a subtle strip at the bottom of the page. Renders nothing
 * in create mode (no id yet), for unregistered types, or when the caller lacks the owning module.
 * Globally registered — drop `<AppRecordMeta entity-type="product" :record-id="id" />` at the foot of
 * any detail page.
 */
import { ref, computed, watch } from 'vue'
import { api, unwrap } from 'services/api'
import { formatDateTime } from 'src/utils/datetime'

const props = defineProps({
  entityType: { type: String, required: true },
  recordId: { type: [String, Number], default: null }
})

const audit = ref(null)

// A real persisted timestamp; guards against unset/epoch dates on legacy or seed rows.
function realDate (value) {
  if (!value) return null
  const d = new Date(value)
  return Number.isNaN(d.getTime()) || d.getFullYear() <= 1 ? null : formatDateTime(value)
}

const createdOn = computed(() => realDate(audit.value?.createdOnUtc))
const updatedOn = computed(() => realDate(audit.value?.updatedOnUtc))
const createdBy = computed(() => actor(audit.value?.createdBy, audit.value?.createdById))
const updatedBy = computed(() => actor(audit.value?.updatedBy, audit.value?.updatedById))

// Resolved name → the name; known actor without a name (deleted user) → Unknown; no actor → System.
function actor (name, id) {
  return name || (id ? 'Unknown' : 'System')
}

const show = computed(() => !!audit.value && (createdOn.value || updatedOn.value || audit.value.createdById || audit.value.updatedById))

async function load () {
  audit.value = null
  if (!props.entityType || !props.recordId) return
  try {
    audit.value = await api.get(`/api/admin/records/${props.entityType}/${props.recordId}/audit`).then(unwrap)
  } catch {
    audit.value = null // missing / forbidden / unregistered → render nothing
  }
}

watch(() => [props.entityType, props.recordId], load, { immediate: true })
</script>

<style scoped>
.app-record-meta {
  margin-top: 28px;
}
.app-record-meta__label {
  color: var(--q-color-grey-6, #9e9e9e);
  font-size: 12px;
  margin-right: 6px;
}
.app-record-meta__value {
  color: var(--q-color-grey-8, #616161);
  font-weight: 500;
}
</style>
