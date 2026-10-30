<template>
  <q-dialog
    :model-value="modelValue"
    persistent
    @update:model-value="$emit('update:modelValue', $event)"
  >
    <q-card style="min-width: 380px">
      <q-card-section class="row items-center q-pb-none">
        <div class="text-h6">Change base currency</div>
        <q-space />
        <q-btn flat round dense icon="o_close" @click="onCancel" />
      </q-card-section>

      <q-card-section>
        <q-banner dense rounded class="bg-orange-1 text-orange-9 q-mb-md">
          <template #avatar>
            <q-icon name="o_warning" color="orange-9" />
          </template>
          Changing the base currency re-bases every exchange rate. The new base is
          pinned to a rate of 1 and locked; all other rates remain as entered and
          should be reviewed afterwards. This affects the whole platform.
        </q-banner>

        <div class="text-caption text-grey-7 q-mb-xs">
          Current base: <strong>{{ currentBaseCode || '—' }}</strong>
        </div>

        <AppSelect
          v-model="targetCode"
          label="New base currency"
          required
          :v="v$.targetCode"
          :options="options"
        />
      </q-card-section>

      <q-card-actions align="right" class="q-px-md q-pb-md">
        <q-btn flat label="Cancel" color="grey-8" @click="onCancel" />
        <q-btn
          unelevated
          color="warning"
          text-color="dark"
          icon="o_swap_horiz"
          label="Change base currency"
          :loading="saving"
          :disable="!options.length"
          @click="onSubmit"
        />
      </q-card-actions>
    </q-card>
  </q-dialog>
</template>

<script setup>
/*
 * BaseCurrencyDialog (WO-91 REQ-TEN-006, AC-TEN-006.1): the SuperAdmin-only
 * confirmation warning modal for promoting a supported currency to base. The
 * dialog is the warning surface itself (prominent banner + explicit confirm).
 * Emits `submit` with the chosen currency code; the page persists it via the
 * SuperAdmin-gated PUT /api/tenant/base-currency.
 */
import { ref, computed, watch } from 'vue'
import useVuelidate from '@vuelidate/core'
import { required } from 'validators'
import AppSelect from 'components/common/AppSelect.vue'

const props = defineProps({
  modelValue: { type: Boolean, default: false },
  currencies: { type: Array, default: () => [] },
  currentBaseCode: { type: String, default: '' },
  saving: { type: Boolean, default: false }
})

const emit = defineEmits(['update:modelValue', 'submit', 'cancel'])

const targetCode = ref(null)
const v$ = useVuelidate({ targetCode: { required } }, { targetCode })

// Any supported currency except the current base can be promoted.
const options = computed(() =>
  (props.currencies || [])
    .filter((c) => !c.isBaseCurrency)
    .map((c) => ({ label: `${c.currencyCode}${c.symbol ? ' (' + c.symbol + ')' : ''}`, value: c.currencyCode }))
)

watch(
  () => props.modelValue,
  (open) => {
    if (open) {
      targetCode.value = null
      v$.value.$reset()
    }
  }
)

function onCancel () {
  emit('cancel')
  emit('update:modelValue', false)
}

async function onSubmit () {
  const ok = await v$.value.$validate()
  if (!ok) return
  emit('submit', targetCode.value)
}
</script>
