<template>
  <div>
    <q-inner-loading :showing="loading" />

    <div v-for="group in groups" :key="group.key" :class="group.key === 'billing' ? 'q-mt-lg' : ''">
      <div class="row items-center justify-between q-mb-sm">
        <div class="row items-center text-subtitle1 text-weight-medium">
          <q-icon :name="group.icon" class="q-mr-sm" color="grey-7" />
          {{ group.title }}
        </div>
        <q-btn flat dense no-caps color="primary" icon="o_add" label="Add" @click="openAdd(group.type)" />
      </div>

      <div v-if="book[group.key].length === 0" class="text-grey-6 q-pa-md text-center bg-grey-1 rounded-borders">
        No {{ group.type.toLowerCase() }} addresses yet.
      </div>

      <div v-else class="row q-col-gutter-md">
        <div v-for="a in book[group.key]" :key="a.id" class="col-12 col-sm-6">
          <q-card flat bordered class="full-height">
            <q-card-section class="q-pb-xs">
              <div class="row items-center justify-between">
                <div class="text-weight-medium">{{ a.firstName }} {{ a.lastName }}</div>
                <q-badge v-if="a.isDefault" color="primary" label="Default" />
              </div>
              <div v-if="a.company" class="text-grey-7 text-caption">{{ a.company }}</div>
              <div class="text-grey-8 text-body2 q-mt-xs">{{ formatLine(a) }}</div>
              <div v-if="a.phoneNumber" class="text-grey-7 text-caption q-mt-xs">{{ a.phoneNumber }}</div>
            </q-card-section>
            <q-card-actions align="right">
              <q-btn v-if="!a.isDefault" flat dense no-caps size="sm" label="Set default" @click="setDefault(a)" />
              <q-btn flat dense no-caps size="sm" icon="o_tune" label="Edit" @click="openEdit(a)" />
              <q-btn flat dense no-caps size="sm" color="negative" icon="o_delete" label="Delete" @click="confirmRemove(a)" />
            </q-card-actions>
          </q-card>
        </div>
      </div>
    </div>

    <!-- Add / edit dialog -->
    <q-dialog v-model="dialog.open">
      <q-card style="width: 560px; max-width: 95vw">
        <q-card-section class="text-subtitle1 text-weight-medium">
          {{ dialog.id ? 'Edit address' : 'Add address' }}
        </q-card-section>
        <q-separator />
        <q-form @submit.prevent="save">
          <q-card-section class="q-gutter-sm scroll" style="max-height: 65vh">
            <div class="row q-col-gutter-sm">
              <q-select
                v-model="dialog.form.addressType"
                :options="typeOptions"
                emit-value
                map-options
                label="Type"
                outlined
                dense
                class="col-6"
              />
              <q-toggle v-model="dialog.form.isDefault" label="Default for this type" class="col-6" />
            </div>
            <AppAddressForm v-model="dialog.form" required />
          </q-card-section>
          <q-separator />
          <q-card-actions align="right">
            <q-btn flat no-caps label="Cancel" v-close-popup />
            <q-btn type="submit" color="primary" unelevated no-caps :label="dialog.id ? 'Save' : 'Add address'" :loading="dialog.saving" />
          </q-card-actions>
        </q-form>
      </q-card>
    </q-dialog>
  </div>
</template>

<script setup>
import { ref, reactive, onMounted } from 'vue'
import { useQuasar } from 'quasar'
import { accountApi } from 'modules/storefront/account-api'
import { getApiErrorMessage } from 'services/api'
import AppAddressForm from 'components/common/AppAddressForm.vue'

const $q = useQuasar()

const loading = ref(false)
const book = reactive({ shipping: [], billing: [] })

const groups = [
  { key: 'shipping', type: 'Shipping', title: 'Shipping addresses', icon: 'o_local_shipping' },
  { key: 'billing', type: 'Billing', title: 'Billing addresses', icon: 'o_receipt' }
]

const typeOptions = [
  { label: 'Shipping', value: 'Shipping' },
  { label: 'Billing', value: 'Billing' }
]

const dialog = reactive({ open: false, id: null, saving: false, form: emptyForm('Shipping') })

function emptyForm (type) {
  return {
    addressType: type,
    isDefault: false,
    firstName: '',
    lastName: '',
    company: '',
    addressLine1: '',
    addressLine2: '',
    landmark: '',
    city: '',
    stateProvince: '',
    postalCode: '',
    countryCode: '',
    phoneNumber: ''
  }
}

function formatLine (a) {
  return [a.addressLine1, a.addressLine2, a.landmark, a.city, a.stateProvince, a.postalCode, a.countryCode]
    .filter(Boolean)
    .join(', ')
}

async function load () {
  loading.value = true
  try {
    const b = await accountApi.addressBook()
    book.shipping = Array.isArray(b?.shipping) ? b.shipping : []
    book.billing = Array.isArray(b?.billing) ? b.billing : []
  } catch (e) {
    $q.notify({ type: 'negative', message: getApiErrorMessage(e) })
  } finally {
    loading.value = false
  }
}

function openAdd (type) {
  dialog.id = null
  dialog.form = emptyForm(type)
  dialog.open = true
}

function openEdit (addr) {
  dialog.id = addr.id
  dialog.form = { ...addressToPayload(addr) }
  dialog.open = true
}

function addressToPayload (a) {
  return {
    addressType: a.addressType,
    isDefault: a.isDefault,
    firstName: a.firstName || '',
    lastName: a.lastName || '',
    company: a.company || '',
    addressLine1: a.addressLine1 || '',
    addressLine2: a.addressLine2 || '',
    landmark: a.landmark || '',
    city: a.city || '',
    stateProvince: a.stateProvince || '',
    postalCode: a.postalCode || '',
    countryCode: a.countryCode || '',
    phoneNumber: a.phoneNumber || ''
  }
}

function payload (form) {
  return {
    addressType: form.addressType,
    isDefault: !!form.isDefault,
    firstName: (form.firstName || '').trim(),
    lastName: (form.lastName || '').trim(),
    company: (form.company || '').trim() || null,
    addressLine1: (form.addressLine1 || '').trim(),
    addressLine2: (form.addressLine2 || '').trim() || null,
    landmark: (form.landmark || '').trim() || null,
    city: (form.city || '').trim(),
    stateProvince: (form.stateProvince || '').trim() || null,
    postalCode: (form.postalCode || '').trim(),
    countryCode: (form.countryCode || '').toUpperCase().trim(),
    phoneNumber: (form.phoneNumber || '').trim() || null
  }
}

async function save () {
  dialog.saving = true
  try {
    if (dialog.id) await accountApi.updateAddress(dialog.id, payload(dialog.form))
    else await accountApi.createAddress(payload(dialog.form))
    dialog.open = false
    $q.notify({ type: 'positive', message: 'Address saved.' })
    await load()
  } catch (e) {
    $q.notify({ type: 'negative', message: getApiErrorMessage(e) })
  } finally {
    dialog.saving = false
  }
}

async function setDefault (addr) {
  if (addr.isDefault) return
  try {
    // Dedicated set-default endpoint (only the id) — flipping the default must not re-validate the
    // whole address, which would 400 on an address missing an optional field.
    await accountApi.setDefaultAddress(addr.id)
    await load()
  } catch (e) {
    $q.notify({ type: 'negative', message: getApiErrorMessage(e) })
  }
}

function confirmRemove (addr) {
  $q.dialog({
    title: 'Delete address',
    message: `Remove this ${addr.addressType.toLowerCase()} address?`,
    cancel: true,
    ok: { label: 'Delete', color: 'negative', unelevated: true, noCaps: true }
  }).onOk(async () => {
    try {
      await accountApi.removeAddress(addr.id)
      $q.notify({ type: 'positive', message: 'Address removed.' })
      await load()
    } catch (e) {
      $q.notify({ type: 'negative', message: getApiErrorMessage(e) })
    }
  })
}

onMounted(load)
</script>
