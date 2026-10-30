<template>
  <q-dialog
    :model-value="modelValue"
    persistent
    @update:model-value="$emit('update:modelValue', $event)"
  >
    <q-card style="min-width: 340px">
      <q-card-section class="row items-center q-pb-none">
        <div class="text-h6">Update rate</div>
        <q-space />
        <q-btn flat round dense icon="o_close" @click="onCancel" />
      </q-card-section>

      <q-card-section class="q-gutter-sm">
        <div class="text-body2 text-grey-8">
          <strong>{{ item?.currencyCode }}</strong>
          <span v-if="item?.symbol" class="q-ml-xs">({{ item.symbol }})</span>
        </div>
        <div class="text-caption text-grey-7">
          Current rate: {{ item?.exchangeRate ?? '—' }}
          <span v-if="lastUpdated"> · updated {{ lastUpdated }}</span>
        </div>

        <q-form ref="formRef" class="q-mt-sm" @submit.prevent="onSubmit">
          <AppTextField
            v-model="rate"
            label="New exchange rate"
            required
            :v="v$.rate"
            type="number"
            step="any"
            min="0"
            autofocus
          />
        </q-form>
      </q-card-section>

      <q-card-actions align="right" class="q-px-md q-pb-md">
        <q-btn flat label="Cancel" color="grey-8" @click="onCancel" />
        <q-btn unelevated color="primary" icon="o_sync" label="Update rate" :loading="saving" @click="onSubmit" />
      </q-card-actions>
    </q-card>
  </q-dialog>
</template>

<script setup>
/*
 * RateUpdateDialog (WO-91 REQ-TEN-006, AC-TEN-006.3): a focused per-row manual
 * exchange-rate editor. Emits `submit` with the new numeric rate; the page then
 * persists it via currenciesApi.updateRate (which reuses the Update endpoint,
 * keeping symbol/flags intact).
 */
import { ref, computed, watch } from 'vue'
import useVuelidate from '@vuelidate/core'
import { required, helpers } from 'validators'
import AppTextField from 'components/common/AppTextField.vue'
import { formatDateTime } from 'src/utils/datetime'

const props = defineProps({
  modelValue: { type: Boolean, default: false },
  item: { type: Object, default: null },
  saving: { type: Boolean, default: false }
})

const emit = defineEmits(['update:modelValue', 'submit', 'cancel'])

const rate = ref(null)

const positiveRate = helpers.withMessage(
  'Enter a rate greater than 0',
  (v) => !helpers.req(v) || (Number.isFinite(Number(v)) && Number(v) > 0)
)
const v$ = useVuelidate({ rate: { required, positiveRate } }, { rate })

const lastUpdated = computed(() => {
  const iso = props.item && props.item.lastRateUpdatedOnUtc
  return iso ? formatDateTime(iso) : null
})

watch(
  () => [props.modelValue, props.item],
  ([open]) => {
    if (open) {
      rate.value = props.item ? props.item.exchangeRate : null
      v$.value.$reset()
    }
  },
  { immediate: true }
)

function onCancel () {
  emit('cancel')
  emit('update:modelValue', false)
}

async function onSubmit () {
  const ok = await v$.value.$validate()
  if (!ok) return
  emit('submit', Number(rate.value))
}
</script>
