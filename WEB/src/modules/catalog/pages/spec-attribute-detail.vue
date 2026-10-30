<template>
  <q-page class="app-page">
    <AppDetailHeader
      :title="isCreate ? 'New specification attribute' : (entity?.name || 'Specification attribute')"
      :breadcrumbs="[
        { label: 'Home', icon: 'o_home', to: '/dashboard' },
        { label: 'Attributes', to: { name: 'catalog-attributes', query: { tab: 'specification' } } },
        { label: isCreate ? 'New attribute' : (entity?.name || 'Specification attribute') }
      ]"
      show-back
      @back="router.push({ name: 'catalog-attributes', query: { tab: 'specification' } })"
    >
      <template #actions>
        <q-chip
          v-if="saveStatus"
          :icon="saveStatus.icon"
          :color="saveStatus.chip"
          :text-color="saveStatus.text"
          square
          dense
          class="q-mr-sm text-caption"
        >
          <q-spinner v-if="saveStatus.spin" size="14px" class="q-mr-xs" />
          {{ saveStatus.label }}
        </q-chip>
      </template>
    </AppDetailHeader>

    <q-inner-loading :showing="loading" color="primary" />

    <q-banner v-if="!loading && !isCreate && !entity" class="bg-grey-2 rounded-borders">
      Attribute not found.
    </q-banner>

    <template v-if="isCreate || entity">
      <div v-if="!isCreate" class="row items-center text-caption text-grey-7 q-mb-sm q-px-xs">
        <q-icon name="o_cloud_sync" size="16px" class="q-mr-xs" />
        Changes are saved automatically as you edit — no need to press save.
      </div>

      <q-card flat bordered class="app-section">
        <q-tabs
          v-model="tab"
          align="left"
          active-color="primary"
          indicator-color="primary"
          class="text-grey-7 app-detail-tabs"
          no-caps
          inline-label
        >
          <q-tab name="general" icon="o_tune" label="General" />
        </q-tabs>

        <q-separator />

        <q-tab-panels v-model="tab" animated keep-alive>
          <q-tab-panel name="general" class="q-gutter-y-sm">
            <AppTextField v-model="form.name" label="Name" required :v="v$.name" placeholder="e.g. Material, Warranty" :disable="!canWrite" />
            <div class="row items-center q-col-gutter-sm">
              <div class="col-6 col-md-3"><AppTextField v-model="form.displayOrder" label="Display order" type="number" :disable="!canWrite" /></div>
              <div class="col-auto q-mt-md">
                <q-toggle v-model="form.isFilterable" label="Filterable" color="primary" :disable="!canWrite" />
                <div class="text-caption text-grey-6">Show in storefront filters</div>
              </div>
            </div>

            <q-separator class="q-my-md" />
            <div class="row items-center justify-between q-mb-sm">
              <AppFieldLabel label="Options" />
              <q-btn v-if="canWrite" flat dense no-caps color="primary" icon="o_add" label="Add option" @click="addOption" />
            </div>
            <div v-if="!form.options.length" class="text-grey-6 text-caption q-mb-sm">No options yet. Add one or more (e.g. Cotton, Leather).</div>
            <div v-for="(opt, i) in form.options" :key="opt._key" class="row items-center q-col-gutter-xs q-mb-xs no-wrap">
              <div class="col-auto column">
                <q-btn flat dense round size="sm" icon="o_keyboard_arrow_up" :disable="i === 0 || !canWrite" @click="move(i, -1)" />
                <q-btn flat dense round size="sm" icon="o_keyboard_arrow_down" :disable="i === form.options.length - 1 || !canWrite" @click="move(i, 1)" />
              </div>
              <div class="col"><q-input v-model="opt.value" dense outlined placeholder="Option label" :disable="!canWrite" /></div>
              <div class="col-auto"><q-btn v-if="canWrite" flat dense round size="sm" icon="o_delete" color="negative" @click="removeOption(i)" /></div>
            </div>
          </q-tab-panel>
        </q-tab-panels>

        <template v-if="isCreate">
          <q-separator />
          <q-card-actions class="q-pa-md">
            <div class="text-caption text-grey-7">Create the attribute — changes auto-save from then on.</div>
            <q-space />
            <q-btn v-if="canWrite" unelevated color="primary" no-caps icon="o_check" label="Create attribute" :loading="creating" @click="create" />
          </q-card-actions>
        </template>
      </q-card>
    </template>

    <AppRecordMeta entity-type="specification-attribute" :record-id="entity?.id" />
  </q-page>
</template>

<script setup>
/* Specification attribute create + manage page (full-page auto-save via useDetailForm). Options are part
 * of the form, so add/remove/reorder/edit auto-save via the deep form watch. */
import { ref, computed } from 'vue'
import { useRouter } from 'vue-router'
import { usePermissions } from 'composables/usePermissions'
import { useDetailForm } from 'composables/useDetailForm'
import { required, maxLength } from 'validators'
import { specificationAttributeApi } from 'modules/catalog/api'
import AppDetailHeader from 'components/common/AppDetailHeader.vue'
import AppTextField from 'components/common/AppTextField.vue'
import AppFieldLabel from 'components/common/AppFieldLabel.vue'

const router = useRouter()
const { has } = usePermissions()
const canWrite = computed(() => has('Catalog.Write'))

const tab = ref('general')
let keySeq = 0
const nextKey = () => `o${keySeq++}`

function buildPayload (f) {
  return {
    name: (f.name || '').trim(),
    isFilterable: f.isFilterable,
    displayOrder: Number(f.displayOrder) || 0,
    options: (f.options || [])
      .filter((o) => (o.value || '').trim())
      .map((o, index) => ({ id: o.id || undefined, value: o.value.trim(), displayOrder: index }))
  }
}

const {
  form, v$, entity, loading, creating, isCreate, saveStatus, create
} = useDetailForm({
  createRouteName: 'spec-attribute-new',
  detailRouteName: 'spec-attribute-detail',
  entityLabel: 'attribute',
  api: specificationAttributeApi,
  buildPayload,
  empty: { name: '', isFilterable: true, displayOrder: 0, options: [] },
  rules: { name: { required, maxLength: maxLength(200) } },
  hydrateForm: (f, e) => {
    f.options = (e.options || []).map((o) => ({ _key: nextKey(), id: o.id || null, value: o.value || '' }))
  }
})

function addOption () { form.options.push({ _key: nextKey(), id: null, value: '' }) }
function removeOption (i) { form.options.splice(i, 1) }
function move (i, dir) {
  const j = i + dir
  if (j < 0 || j >= form.options.length) return
  const [row] = form.options.splice(i, 1)
  form.options.splice(j, 0, row)
}
</script>

<style scoped lang="scss">
.app-detail-tabs {
  :deep(.q-tab) { min-height: 44px; }
}
</style>
