<template>
  <div class="column q-gutter-md">
    <AppSection title="Add new rate">
      <q-form class="row q-col-gutter-sm items-start" @submit.prevent="add">
        <div class="col-12 col-sm-4 col-md-3">
          <q-input v-model="newZip" dense outlined label="ZIP/Postal Code" maxlength="20" />
        </div>
        <div class="col-12 col-sm-4 col-md-3">
          <q-input v-model="newRate" dense outlined label="Rate" type="number" step="0.0001" min="0" max="100" suffix="%" />
        </div>
        <div class="col-auto">
          <q-btn type="submit" color="primary" unelevated no-caps icon="o_add" label="Save" :loading="adding" />
        </div>
        <q-space />
        <div class="col-auto">
          <q-btn outline color="primary" no-caps icon="o_upload_file" label="Import CSV" :loading="importing" @click="fileInput.click()" />
          <input ref="fileInput" type="file" accept=".csv,text/csv" class="hidden" @change="onFile">
          <div class="text-caption text-grey-7 q-mt-xs">Columns: zip, rate. Existing ZIPs are updated.</div>
        </div>
      </q-form>
    </AppSection>

    <AppDataTable
      page-key="tax-zip-rates"
      row-key="id"
      title="Tax by ZIP/Postal Code"
      :rows="rows"
      :columns="columns"
      :loading="loading"
      :pagination="pagination"
      show-actions
      no-data-label="No ZIP rates yet."
      @request="onRequest"
      @refresh="reload"
    >
      <template #top>
        <div class="row items-center full-width no-wrap q-gutter-sm">
          <div class="app-section__title col ellipsis">Tax by ZIP/Postal Code</div>
          <q-input v-model="search" dense outlined clearable debounce="400" placeholder="Search ZIP" style="width: 220px" @update:model-value="reload">
            <template #prepend><q-icon name="o_search" /></template>
          </q-input>
          <q-btn flat round dense icon="o_refresh" @click="reload"><q-tooltip>Refresh</q-tooltip></q-btn>
        </div>
      </template>
      <template #body-cell-ratePercent="cell">
        <q-td :props="cell">{{ Number(cell.row.ratePercent) }}%</q-td>
      </template>
      <template #actions="{ row }">
        <q-btn flat round dense icon="o_edit" @click="edit(row)"><q-tooltip>Edit</q-tooltip></q-btn>
        <q-btn flat round dense icon="o_delete" color="negative" @click="remove(row)"><q-tooltip>Delete</q-tooltip></q-btn>
      </template>
    </AppDataTable>

    <q-dialog v-model="editOpen">
      <q-card style="width: 420px; max-width: 95vw">
        <q-card-section class="text-subtitle1 text-weight-medium">Edit tax rate</q-card-section>
        <q-card-section class="q-gutter-sm">
          <q-input v-model="editZip" dense outlined label="ZIP/Postal Code" maxlength="20" />
          <q-input v-model="editRate" dense outlined label="Rate" type="number" step="0.0001" min="0" max="100" suffix="%" />
        </q-card-section>
        <q-card-actions align="right">
          <q-btn flat no-caps label="Cancel" v-close-popup />
          <q-btn color="primary" unelevated no-caps label="Save" :loading="savingEdit" @click="saveEdit" />
        </q-card-actions>
      </q-card>
    </q-dialog>
  </div>
</template>

<script setup>
/* PHP "Tax By ZIP/Postal Code": search, add, edit, delete, plus a CSV import (zip,rate). */
import { ref } from 'vue'
import { getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'
import { deleteConfirmation } from 'dialogs/delete_confirmation'
import { taxApi } from 'modules/tax/api'
import AppSection from 'components/common/AppSection.vue'
import AppDataTable from 'components/common/AppDataTable.vue'

const notify = useNotify()
const rows = ref([])
const loading = ref(false)
const pagination = ref({ page: 1, rowsPerPage: 50, rowsNumber: 0 })
const search = ref('')
const columns = [
  { name: 'postalCode', label: 'ZIP/Postal Code', field: 'postalCode', align: 'left' },
  { name: 'ratePercent', label: 'Rate', field: 'ratePercent', align: 'left' }
]

const newZip = ref('')
const newRate = ref('')
const adding = ref(false)
const importing = ref(false)
const fileInput = ref(null)

const editOpen = ref(false)
const editId = ref(null)
const editZip = ref('')
const editRate = ref('')
const savingEdit = ref(false)

function validRate (v) {
  const n = Number(v)
  return v !== '' && v !== null && Number.isFinite(n) && n >= 0 && n <= 100
}

async function fetch ({ pagination: p }) {
  loading.value = true
  try {
    const res = await taxApi.zipRates({ search: search.value || undefined, page: p.page, pageSize: p.rowsPerPage })
    rows.value = res?.items || []
    pagination.value = { ...p, rowsNumber: res?.totalCount || 0 }
  } catch (err) { notify.error(getApiErrorMessage(err)) } finally { loading.value = false }
}

function onRequest (props) { fetch(props) }
function reload () { fetch({ pagination: { ...pagination.value, page: 1 } }) }

async function add () {
  if (!newZip.value.trim() || !validRate(newRate.value)) {
    notify.error('Enter a ZIP/postal code and a rate between 0 and 100.')
    return
  }
  adding.value = true
  try {
    await taxApi.createZipRate({ postalCode: newZip.value.trim(), ratePercent: Number(newRate.value) })
    notify.success('Tax rate added')
    newZip.value = ''
    newRate.value = ''
    reload()
  } catch (err) { notify.error(getApiErrorMessage(err)) } finally { adding.value = false }
}

function edit (row) {
  editId.value = row.id
  editZip.value = row.postalCode
  editRate.value = row.ratePercent
  editOpen.value = true
}

async function saveEdit () {
  if (!String(editZip.value).trim() || !validRate(editRate.value)) {
    notify.error('Enter a ZIP/postal code and a rate between 0 and 100.')
    return
  }
  savingEdit.value = true
  try {
    await taxApi.updateZipRate(editId.value, { postalCode: String(editZip.value).trim(), ratePercent: Number(editRate.value) })
    notify.success('Tax rate updated')
    editOpen.value = false
    fetch({ pagination: pagination.value })
  } catch (err) { notify.error(getApiErrorMessage(err)) } finally { savingEdit.value = false }
}

async function remove (row) {
  if (!(await deleteConfirmation(`the tax rate for ${row.postalCode}`))) return
  try {
    await taxApi.deleteZipRate(row.id)
    notify.success('Tax rate deleted')
    fetch({ pagination: pagination.value })
  } catch (err) { notify.error(getApiErrorMessage(err)) }
}

async function onFile (e) {
  const file = e.target.files && e.target.files[0]
  e.target.value = ''
  if (!file) return
  importing.value = true
  try {
    const r = await taxApi.importZipRates(file)
    const msg = `Imported: ${r.added} added, ${r.updated} updated` + (r.skipped ? `, ${r.skipped} skipped` : '')
    if (r.skipped) notify.warning(msg + (r.errors && r.errors.length ? `. First problem: ${r.errors[0]}` : ''))
    else notify.success(msg)
    reload()
  } catch (err) { notify.error(getApiErrorMessage(err)) } finally { importing.value = false }
}

reload()
</script>
