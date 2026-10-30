<template>
  <q-page class="app-page">
    <AppDetailHeader
      :title="isCreate ? 'New widget' : (entity?.name || 'Widget')"
      :breadcrumbs="[
        { label: 'Home', icon: 'o_home', to: '/dashboard' },
        { label: 'Widgets', to: { name: 'widgets' } },
        { label: isCreate ? 'New widget' : (entity?.name || 'Widget') }
      ]"
      :status="!isCreate && entity ? (form.isActive ? 'Active' : 'Inactive') : ''"
      :status-color="form.isActive ? 'positive' : 'grey'"
      show-back
      @back="router.push({ name: 'widgets' })"
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
      Widget not found.
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
          <q-tab name="general" icon="o_widgets" label="General" />
        </q-tabs>

        <q-separator />

        <q-tab-panels v-model="tab" animated keep-alive>
          <q-tab-panel name="general" class="q-gutter-y-sm">
            <AppTextField v-model="form.name" label="Name" required :v="v$.name" placeholder="Widget name" />
            <AppTextField v-model="form.slug" label="Slug" :v="v$.slug" hint="Auto-filled from the name until you edit it" />
            <AppRichText v-model="form.description" label="Description" placeholder="Describe this widget…" />
            <div class="row q-col-gutter-sm items-center">
              <div class="col-12 col-md-4">
                <AppSelect v-model="form.status" label="Status" :options="statusOptions" />
              </div>
              <div class="col-auto q-mt-md">
                <q-toggle v-model="form.isActive" label="Active" color="primary" />
              </div>
            </div>
          </q-tab-panel>
        </q-tab-panels>

        <template v-if="isCreate">
          <q-separator />
          <q-card-actions class="q-pa-md">
            <div class="text-caption text-grey-7">Create the widget — changes auto-save from then on.</div>
            <q-space />
            <q-btn unelevated color="primary" no-caps icon="o_check" label="Create widget" :loading="creating" @click="create" />
          </q-card-actions>
        </template>
      </q-card>
    </template>
  </q-page>
</template>

<script setup>
/*
 * Widget create + manage page (feature-module template; full-page auto-save via useDetailForm).
 */
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { widgetApi } from 'services/api'
import { useDetailForm } from 'composables/useDetailForm'
import { required, maxLength } from 'validators'
import AppDetailHeader from 'components/common/AppDetailHeader.vue'
import AppTextField from 'components/common/AppTextField.vue'
import AppSelect from 'components/common/AppSelect.vue'
import AppRichText from 'components/common/AppRichText.vue'

const router = useRouter()
const tab = ref('general')

const statusOptions = [
  { label: 'Draft', value: 'draft' },
  { label: 'Published', value: 'published' },
  { label: 'Archived', value: 'archived' }
]

function buildPayload (f) {
  return {
    name: f.name,
    slug: f.slug || null,
    description: f.description || null,
    status: f.status,
    isActive: f.isActive
  }
}

const {
  form, v$, entity, loading, creating, isCreate, saveStatus, create
} = useDetailForm({
  createRouteName: 'widget-new',
  detailRouteName: 'widget-detail',
  entityLabel: 'widget',
  deriveSlug: true,
  api: widgetApi,
  buildPayload,
  empty: { name: '', slug: '', description: '', status: 'draft', isActive: true },
  rules: {
    name: { required, maxLength: maxLength(120) },
    slug: { maxLength: maxLength(120) }
  }
})
</script>

<style scoped lang="scss">
.app-detail-tabs {
  :deep(.q-tab) { min-height: 44px; }
}
</style>
