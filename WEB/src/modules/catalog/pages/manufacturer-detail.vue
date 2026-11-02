<template>
  <q-page class="app-page">
    <AppDetailHeader
      :title="isCreate ? 'New brand' : (entity?.name || 'Brand')"
      :breadcrumbs="[
        { label: 'Home', icon: 'o_home', to: '/dashboard' },
        { label: 'Brands', to: { name: 'catalog-manufacturers' } },
        { label: isCreate ? 'New brand' : (entity?.name || 'Brand') }
      ]"
      :status="!isCreate && entity ? (form.isEnabled ? 'Enabled' : 'Disabled') : ''"
      :status-color="form.isEnabled ? 'positive' : 'grey'"
      show-back
      @back="router.push({ name: 'catalog-manufacturers' })"
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
      Brand not found.
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
          <q-tab name="general" icon="o_info" label="General" />
          <q-tab name="seo" icon="o_search" label="SEO" :disable="isCreate" />
        </q-tabs>

        <q-separator />

        <q-tab-panels v-model="tab" animated keep-alive>
          <!-- ============ GENERAL ============ -->
          <q-tab-panel name="general" class="q-gutter-y-xs">
            <AppTextField v-model="form.name" label="Name" required :v="v$.name" placeholder="e.g. Acme Corp" :disable="!canWrite" />
            <AppTextField v-model="form.slug" label="Slug" :v="v$.slug" placeholder="e.g. acme-corp" hint="Storefront URL — auto-filled from the name until you edit it" :disable="!canWrite" />
            <AppRichText v-model="form.description" label="Description" placeholder="Describe this brand…" :disable="!canWrite" />

            <AppFileUpload media v-model="form.logoMediaId" v-model:preview-url="form.logoUrl" label="Logo" accept="image/*" extensions-label="PNG, JPG, SVG" :disable="!canWrite" />

            <q-separator class="q-my-sm" />
            <div class="row q-col-gutter-sm items-center">
              <div class="col-6 col-md-3">
                <AppTextField v-model="form.displayOrder" label="Display order" type="number" hint="Lower shows first" :disable="!canWrite" />
              </div>
              <div class="col-auto q-mt-md">
                <q-toggle v-model="form.isEnabled" label="Enabled" color="primary" :disable="!canWrite" />
              </div>
            </div>
          </q-tab-panel>

          <!-- ============ SEO ============ -->
          <q-tab-panel name="seo">
            <AppSeoPanel
              entity-type="Manufacturer"
              :entity-id="entity?.id"
              :slug="form.slug"
              :fallback-title="form.name"
              :fallback-description="form.description"
              fallback-label="brand name"
              :disable="!canWrite"
              :run-save="runSave"
            />
          </q-tab-panel>
        </q-tab-panels>

        <template v-if="isCreate">
          <q-separator />
          <q-card-actions class="q-pa-md">
            <div class="text-caption text-grey-7">
              Create the brand to unlock SEO — all auto-saved from then on.
            </div>
            <q-space />
            <q-btn v-if="canWrite" unelevated color="primary" no-caps icon="o_check" label="Create brand" :loading="creating" @click="create" />
          </q-card-actions>
        </template>
      </q-card>
    </template>

    <AppRecordMeta entity-type="manufacturer" :record-id="entity?.id" />
  </q-page>
</template>

<script setup>
/*
 * Brand (Manufacturer entity) create + manage page (full-page auto-save pattern via useDetailForm). Logo is a single
 * central Media asset (AppFileUpload media mode) — its id lives on the form and auto-saves like any field.
 */
import { ref, computed } from 'vue'
import { useRouter } from 'vue-router'
import { manufacturerApi } from 'modules/catalog/api'
import { usePermissions } from 'composables/usePermissions'
import { useDetailForm } from 'composables/useDetailForm'
import { required, maxLength } from 'validators'
import AppDetailHeader from 'components/common/AppDetailHeader.vue'
import AppTextField from 'components/common/AppTextField.vue'
import AppRichText from 'components/common/AppRichText.vue'
import AppFileUpload from 'components/common/AppFileUpload.vue'

const router = useRouter()
const { has } = usePermissions()
const canWrite = computed(() => has('Catalog.Write'))

const tab = ref('general')

function buildPayload (form) {
  return {
    name: form.name,
    slug: form.slug || null,
    description: form.description || null,
    logoMediaId: form.logoMediaId || null,
    // Only forward a raw URL for un-migrated legacy records (no Media asset chosen).
    logoUrl: form.logoMediaId ? null : (form.logoUrl || null),
    displayOrder: Number(form.displayOrder) || 0,
    isEnabled: form.isEnabled
  }
}

const {
  form, v$, entity, loading, creating, isCreate, saveStatus, create, runSave
} = useDetailForm({
  createRouteName: 'catalog-manufacturer-new',
  detailRouteName: 'catalog-manufacturer-detail',
  entityLabel: 'brand',
  deriveSlug: true,
  api: manufacturerApi,
  buildPayload,
  empty: {
    name: '', slug: '', description: '', logoMediaId: null, logoUrl: '',
    displayOrder: 0, isEnabled: true
  },
  rules: {
    name: { required, maxLength: maxLength(200) },
    slug: { maxLength: maxLength(220) }
  }
})
</script>

<style scoped lang="scss">
.app-detail-tabs {
  :deep(.q-tab) { min-height: 44px; }
}
</style>
