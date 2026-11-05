<template>
  <div class="sf-gift-certificate">
    <!-- Applied -->
    <div v-if="applied" class="row items-center no-wrap q-gutter-sm">
      <q-icon name="o_redeem" color="positive" size="20px" />
      <div class="col">
        <div class="text-weight-medium" style="font-family: monospace; letter-spacing: 1px">{{ applied.code }}</div>
        <div class="text-caption text-grey-7">Balance {{ formatPrice(applied.currentBalance) }}</div>
      </div>
      <div class="text-positive text-weight-medium">− {{ formatPrice(applied.applyAmount) }} Applied</div>
      <q-btn flat dense no-caps color="grey-8" label="Clear" :loading="busy" @click="clear" />
    </div>

    <!-- Entry -->
    <template v-else>
      <q-expansion-item v-if="collapsible" v-model="open" dense dense-toggle icon="o_redeem" label="Have a gift certificate?" header-class="q-px-none text-primary">
        <div class="q-pt-sm"><EntryRow /></div>
      </q-expansion-item>
      <EntryRow v-else />
      <div v-if="error" class="text-negative text-caption q-mt-xs">{{ error }}</div>
    </template>
  </div>
</template>

<script setup>
/*
 * Reusable "Apply gift certificate" control for the cart and the checkout summary. Applies the code to the
 * shopper's cart (one certificate per cart; a new code replaces the old one) and shows code, balance and
 * "− $X Applied" with a Clear link. The applied state is the cart's (re-validated server-side on every read),
 * passed in via `applied`; the component emits `changed` so the page re-prices.
 */
import { ref, h } from 'vue'
import { QInput, QBtn } from 'quasar'
import { useCart } from 'modules/storefront/composables/useCart'
import { formatPrice } from 'modules/storefront/api'

const props = defineProps({
  /** The cart's applied certificate { code, currentBalance, applyAmount } or null. */
  applied: { type: Object, default: null },
  collapsible: { type: Boolean, default: false }
})
const emit = defineEmits(['changed'])

const { applyGiftCertificate, removeGiftCertificate } = useCart()
const code = ref('')
const busy = ref(false)
const error = ref('')
const open = ref(false)

function messageOf (err) {
  const status = err?.response?.status
  if (status === 429) return err.response.data?.title || 'Too many attempts. Please wait a few minutes and try again.'
  if (status === 422) return err.response.data?.title || 'Invalid Gift Certificate'
  return 'Could not apply the gift certificate. Please try again.'
}

async function apply () {
  if (!code.value.trim() || busy.value) return
  busy.value = true
  error.value = ''
  try {
    await applyGiftCertificate(code.value.trim())
    code.value = ''
    emit('changed')
  } catch (err) {
    error.value = messageOf(err)
  } finally {
    busy.value = false
  }
}

async function clear () {
  busy.value = true
  try {
    await removeGiftCertificate()
    emit('changed')
  } finally {
    busy.value = false
  }
}

// Small inline render function so the entry row can sit inside or outside the expansion item.
const EntryRow = () => h('div', { class: 'row no-wrap q-gutter-sm items-start' }, [
  h(QInput, {
    class: 'col',
    dense: true,
    outlined: true,
    modelValue: code.value,
    'onUpdate:modelValue': (v) => { code.value = v; error.value = '' },
    placeholder: 'Gift certificate code',
    autocomplete: 'off',
    spellcheck: false,
    inputStyle: { textTransform: 'uppercase', fontFamily: 'monospace' },
    onKeyup: (e) => { if (e.key === 'Enter') apply() },
    error: !!error.value,
    hideBottomSpace: true
  }),
  h(QBtn, { unelevated: true, noCaps: true, color: 'primary', label: 'Apply', loading: busy.value, disable: !code.value.trim(), onClick: apply })
])

</script>
