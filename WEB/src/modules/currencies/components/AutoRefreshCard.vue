<template>
  <q-card flat bordered>
    <q-card-section>
      <div class="row items-center">
        <div class="col">
          <div class="text-subtitle1">Automatic rate refresh</div>
          <div class="text-caption text-grey-7">
            Periodically refresh unlocked exchange rates from an external source.
          </div>
        </div>
        <q-toggle
          v-model="form.enabled"
          color="primary"
          :disable="!canWrite"
          @update:model-value="dirty = true"
        />
      </div>

      <q-slide-transition>
        <div v-show="form.enabled" class="q-mt-md">
          <AppSelect
            v-model="form.intervalHours"
            label="Refresh interval"
            :options="intervalOptions"
            :disable="!canWrite"
            @update:model-value="dirty = true"
          />
          <AppTextField
            v-model="form.sourceUrl"
            label="Rate source URL"
            :v="v$.sourceUrl"
            placeholder="https://api.example.com/rates"
            :disable="!canWrite"
            @update:model-value="dirty = true"
          >
            <template #hint><span class="text-caption text-grey-6">Optional</span></template>
          </AppTextField>
          <div class="text-caption text-grey-6 q-mt-xs">
            External endpoint the scheduled job fetches rates from.
          </div>
        </div>
      </q-slide-transition>
    </q-card-section>

    <q-separator />
    <q-card-actions align="right">
      <q-btn
        v-if="canWrite"
        unelevated
        color="primary"
        icon="o_save"
        label="Save"
        no-caps
        :loading="saving"
        :disable="!dirty"
        @click="onSave"
      />
    </q-card-actions>
  </q-card>
</template>

<script setup>
/*
 * AutoRefreshCard (WO-91 REQ-TEN-006, AC-TEN-006.4): the auto-refresh toggle,
 * interval selector and external rate-source URL. Self-contained form state
 * seeded from the `config` prop; emits `save` with the AutoRefreshConfig payload
 * for the page to persist via PUT /api/tenant/currencies/auto-refresh.
 */
import { reactive, ref, watch } from 'vue'
import useVuelidate from '@vuelidate/core'
import { url } from 'validators'
import AppSelect from 'components/common/AppSelect.vue'
import AppTextField from 'components/common/AppTextField.vue'

const props = defineProps({
  config: { type: Object, default: null },
  saving: { type: Boolean, default: false },
  canWrite: { type: Boolean, default: false }
})

const emit = defineEmits(['save'])

const EMPTY = { enabled: false, intervalHours: 24, sourceUrl: '' }
const form = reactive({ ...EMPTY })
const dirty = ref(false)

const v$ = useVuelidate({ sourceUrl: { url } }, form)

const intervalOptions = [
  { label: 'Every hour', value: 1 },
  { label: 'Every 3 hours', value: 3 },
  { label: 'Every 6 hours', value: 6 },
  { label: 'Every 12 hours', value: 12 },
  { label: 'Daily (24 hours)', value: 24 },
  { label: 'Every 2 days', value: 48 },
  { label: 'Weekly', value: 168 }
]

watch(
  () => props.config,
  (cfg) => {
    Object.assign(form, EMPTY, cfg ? {
      enabled: !!cfg.enabled,
      intervalHours: cfg.intervalHours > 0 ? cfg.intervalHours : 24,
      sourceUrl: cfg.sourceUrl || ''
    } : {})
    dirty.value = false
    v$.value.$reset()
  },
  { immediate: true }
)

async function onSave () {
  const ok = await v$.value.$validate()
  if (!ok) return
  emit('save', {
    enabled: !!form.enabled,
    intervalHours: Number(form.intervalHours) || 24,
    sourceUrl: (form.sourceUrl || '').trim() || null
  })
}
</script>
