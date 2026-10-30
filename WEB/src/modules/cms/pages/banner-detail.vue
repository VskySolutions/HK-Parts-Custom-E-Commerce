<template>
  <q-page class="app-page">
    <AppDetailHeader
      :title="isCreate ? 'New banner' : (entity?.title || 'Banner')"
      :breadcrumbs="[
        { label: 'Home', icon: 'o_home', to: '/dashboard' },
        { label: 'Banners', to: { name: 'cms-banners' } },
        { label: isCreate ? 'New banner' : (entity?.title || 'Banner') }
      ]"
      :status="!isCreate && entity ? (form.isEnabled ? 'Enabled' : 'Disabled') : ''"
      :status-color="form.isEnabled ? 'positive' : 'grey'"
      show-back
      @back="router.push({ name: 'cms-banners' })"
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
      Banner not found.
    </q-banner>

    <template v-if="isCreate || entity">
      <div v-if="!isCreate" class="row items-center text-caption text-grey-7 q-mb-sm q-px-xs">
        <q-icon name="o_cloud_sync" size="16px" class="q-mr-xs" />
        Changes are saved automatically as you edit — no need to press save.
      </div>

      <div class="row q-col-gutter-md">
        <!-- Content -->
        <div class="col-12 col-md-7">
          <AppSection title="Content">
            <AppTextField v-model="form.title" label="Title" required :v="v$.title" placeholder="e.g. Summer Sale" :disable="!canWrite" />
            <AppTextField v-model="form.subtitle" label="Subtitle" placeholder="Optional supporting line" :disable="!canWrite" />
            <AppFileUpload media v-model="form.imageMediaId" v-model:preview-url="form.imageUrl" label="Image" accept="image/*" extensions-label="PNG, JPG, WEBP" :disable="!canWrite" />

            <q-separator class="q-my-sm" />
            <AppTextField v-model="form.linkUrl" label="Link URL" placeholder="Where the banner clicks through to, e.g. /shop/c/sale" :disable="!canWrite" />
            <AppTextField v-model="form.ctaLabel" label="Call-to-action label" placeholder="e.g. Shop now" :disable="!canWrite" />
          </AppSection>
        </div>

        <!-- Placement & scheduling -->
        <div class="col-12 col-md-5">
          <AppSection title="Placement & scheduling">
            <AppSelect
              v-model="form.displayLocation"
              label="Display location"
              required
              :v="v$.displayLocation"
              :options="bannerLocationOptions"
              use-input
              fill-input
              hide-selected
              new-value-mode="add-unique"
              input-debounce="0"
              hint="Pick a known slot or type a custom location key"
              :disable="!canWrite"
            />

            <div class="row q-col-gutter-sm">
              <div class="col-12 col-sm-6">
                <AppDateField v-model="form.startsOnUtc" label="Starts on" :disable="!canWrite" />
              </div>
              <div class="col-12 col-sm-6">
                <AppDateField v-model="form.endsOnUtc" label="Ends on" :disable="!canWrite" />
              </div>
            </div>
            <div class="text-caption text-grey-6 q-mb-sm">Leave both empty to keep the banner always on.</div>

            <q-separator class="q-my-sm" />
            <div class="row q-col-gutter-sm items-center">
              <div class="col-6">
                <AppTextField v-model="form.displayOrder" label="Display order" type="number" hint="Lower shows first" :disable="!canWrite" />
              </div>
              <div class="col-auto q-mt-md">
                <q-toggle v-model="form.isEnabled" label="Enabled" color="primary" :disable="!canWrite" />
              </div>
            </div>
          </AppSection>
        </div>
      </div>

      <div v-if="isCreate" class="row justify-end">
        <q-btn v-if="canWrite" unelevated color="primary" no-caps icon="o_check" label="Create banner" :loading="creating" @click="create" />
      </div>
    </template>

    <AppRecordMeta entity-type="cms-banner" :record-id="entity?.id" />
  </q-page>
</template>

<script setup>
/*
 * Banner create + manage page (WO-55): full-page auto-save pattern via useDetailForm. The image is a
 * central Media asset (AppFileUpload media mode) — its id lives on the form and auto-saves like any
 * field. Schedule dates are stored as calendar days; displayLocation is a combobox (known slots + free text).
 */
import { computed } from 'vue'
import { useRouter } from 'vue-router'
import { bannerApi, bannerLocationOptions } from 'modules/cms/api'
import { usePermissions } from 'composables/usePermissions'
import { useDetailForm } from 'composables/useDetailForm'
import { required, maxLength } from 'validators'
import AppDetailHeader from 'components/common/AppDetailHeader.vue'
import AppSection from 'components/common/AppSection.vue'
import AppTextField from 'components/common/AppTextField.vue'
import AppSelect from 'components/common/AppSelect.vue'
import AppDateField from 'components/common/AppDateField.vue'
import AppFileUpload from 'components/common/AppFileUpload.vue'

const router = useRouter()
const { has } = usePermissions()
const canWrite = computed(() => has('Cms.Write'))

// Keep only the calendar day from an ISO timestamp for the date pickers.
function dayOnly (value) {
  return value ? String(value).slice(0, 10) : ''
}

function buildPayload (form) {
  return {
    title: form.title,
    subtitle: form.subtitle || null,
    imageMediaId: form.imageMediaId || null,
    // Only forward a raw URL for un-migrated legacy records (no Media asset chosen).
    imageUrl: form.imageMediaId ? null : (form.imageUrl || null),
    linkUrl: form.linkUrl || null,
    ctaLabel: form.ctaLabel || null,
    displayLocation: form.displayLocation || null,
    startsOnUtc: form.startsOnUtc || null,
    endsOnUtc: form.endsOnUtc || null,
    displayOrder: Number(form.displayOrder) || 0,
    isEnabled: form.isEnabled
  }
}

const {
  form, v$, entity, loading, creating, isCreate, saveStatus, create
} = useDetailForm({
  createRouteName: 'cms-banner-new',
  detailRouteName: 'cms-banner-detail',
  entityLabel: 'banner',
  api: bannerApi,
  buildPayload,
  empty: {
    title: '', subtitle: '', imageMediaId: null, imageUrl: '',
    linkUrl: '', ctaLabel: '', displayLocation: 'home-hero',
    startsOnUtc: '', endsOnUtc: '', displayOrder: 0, isEnabled: true
  },
  // Server sends full ISO timestamps; the pickers want YYYY-MM-DD.
  hydrateForm: (form, e) => {
    form.startsOnUtc = dayOnly(e.startsOnUtc)
    form.endsOnUtc = dayOnly(e.endsOnUtc)
  },
  rules: {
    title: { required, maxLength: maxLength(200) },
    displayLocation: { required }
  }
})
</script>
