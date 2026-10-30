<template>
  <q-page class="app-page">
    <AppDetailHeader
      :breadcrumbs="[
        { label: 'Home', icon: 'o_home', to: '/dashboard' },
        { label: 'Products', to: '/catalog/products' },
        { label: 'Import & Export' }
      ]"
      @back="$router.push('/catalog/products')"
    />

    <div class="row q-col-gutter-md">
      <!-- Export -->
      <div class="col-12 col-md-6">
        <AppSection title="Export products">
          <div class="text-body2 text-grey-7 q-mb-md">
            Download the catalog as a CSV file. Apply the optional filters to export a subset. The exported
            columns match the import format, so an export can be edited and re-imported.
          </div>
          <AppSelect v-model="exportFilters.type" label="Product type" clearable placeholder="Any type" :options="productTypeOptions" />
          <AppSelect v-model="exportFilters.isPublished" label="Status" :options="publishedOptions" placeholder="All" />
          <AppSelect v-model="exportFilters.categoryId" label="Category" clearable placeholder="Any category" :options="categoryOptions" />

          <div class="row q-gutter-sm q-mt-sm">
            <q-btn color="primary" unelevated no-caps icon="o_download" label="Export CSV" :loading="exporting" @click="doExport" />
            <q-btn outline color="primary" no-caps icon="o_description" label="Download template" @click="downloadTemplate" />
          </div>
        </AppSection>
      </div>

      <!-- Import -->
      <div class="col-12 col-md-6">
        <AppSection title="Import products">
          <div class="text-body2 text-grey-7 q-mb-md">
            Upload a CSV to create or update products. Existing products are matched by <strong>SKU</strong>.
            Import is all-or-nothing — if any row is invalid, nothing is saved and the errors are listed below.
          </div>
          <q-file
            v-model="importFile"
            dense
            outlined
            accept=".csv,text/csv"
            label="CSV file"
            hint="Required columns: Name, ProductType, TaxCategory. Max 20 MB."
            clearable
            max-file-size="20971520"
            @rejected="onRejected"
          >
            <template #prepend><q-icon name="o_attach_file" /></template>
          </q-file>

          <div class="row q-mt-md">
            <q-btn color="primary" unelevated no-caps icon="o_upload" label="Import" :disable="!importFile" :loading="importing" @click="doImport" />
          </div>

          <!-- Result -->
          <template v-if="result">
            <q-banner v-if="result.success" class="bg-green-1 text-green-9 q-mt-md" rounded dense>
              <template #avatar><q-icon name="o_check_circle" color="positive" /></template>
              Import successful — {{ result.created }} created, {{ result.updated }} updated.
            </q-banner>
            <template v-else>
              <q-banner class="bg-red-1 text-red-9 q-mt-md" rounded dense>
                <template #avatar><q-icon name="o_error" color="negative" /></template>
                Import failed — {{ result.errors.length }} error(s). Nothing was saved.
              </q-banner>
              <q-markup-table flat dense class="q-mt-sm">
                <thead>
                  <tr><th class="text-left">Row</th><th class="text-left">Field</th><th class="text-left">Message</th></tr>
                </thead>
                <tbody>
                  <tr v-for="(e, i) in result.errors" :key="i">
                    <td class="text-left">{{ e.row }}</td>
                    <td class="text-left">{{ e.field }}</td>
                    <td class="text-left">{{ e.message }}</td>
                  </tr>
                </tbody>
              </q-markup-table>
            </template>
          </template>
        </AppSection>
      </div>
    </div>
  </q-page>
</template>

<script setup>
/*
 * Product bulk import / export (WO-124): admin UI over the WO-13 CSV endpoints. Export downloads a
 * (optionally filtered) CSV; a client-generated template offers the exact header. Import uploads a CSV
 * and shows the all-or-nothing result — created/updated counts on success, or a row/field/message
 * error table on failure.
 */
import { ref, reactive, onMounted } from 'vue'
import { getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'
import { productApi, productTypeOptions, categoryApi } from 'modules/catalog/api'
import AppDetailHeader from 'components/common/AppDetailHeader.vue'
import AppSection from 'components/common/AppSection.vue'
import AppSelect from 'components/common/AppSelect.vue'

const notify = useNotify()

const publishedOptions = [
  { label: 'All', value: null },
  { label: 'Published', value: true },
  { label: 'Draft', value: false }
]

const exportFilters = reactive({ type: null, isPublished: null, categoryId: null })
const categoryOptions = ref([])
const exporting = ref(false)
const importing = ref(false)
const importFile = ref(null)
const result = ref(null)

async function loadCategories () {
  try {
    const r = await categoryApi.list({ page: 1, pageSize: 500 })
    const items = Array.isArray(r) ? r : r?.items || []
    categoryOptions.value = items.map((c) => ({ label: c.name, value: c.id }))
  } catch (e) { categoryOptions.value = [] }
}

// Trigger a browser download for a Blob under the given filename.
function saveBlob (blob, filename) {
  const url = window.URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = filename
  document.body.appendChild(a)
  a.click()
  a.remove()
  window.URL.revokeObjectURL(url)
}

async function doExport () {
  exporting.value = true
  try {
    const params = {
      type: exportFilters.type || undefined,
      isPublished: exportFilters.isPublished === null ? undefined : exportFilters.isPublished,
      categoryId: exportFilters.categoryId || undefined
    }
    const res = await productApi.exportCsv(params)
    saveBlob(res.data, 'products.csv')
    notify.success('Export ready')
  } catch (e) { notify.error(getApiErrorMessage(e)) } finally { exporting.value = false }
}

function downloadTemplate () {
  const header = 'Name,Sku,ProductType,Price,StockQuantity,IsPublished,Slug,TaxCategory'
  const sample = 'Example Tee,TEE-001,Simple,19.99,100,true,example-tee,Standard'
  saveBlob(new Blob([`${header}\n${sample}\n`], { type: 'text/csv' }), 'products-template.csv')
}

async function doImport () {
  if (!importFile.value) return
  importing.value = true
  result.value = null
  try {
    result.value = await productApi.importCsv(importFile.value)
    if (result.value.success) notify.success(`Imported: ${result.value.created} created, ${result.value.updated} updated`)
    else notify.error(`Import failed with ${result.value.errors.length} error(s)`)
  } catch (e) { notify.error(getApiErrorMessage(e)) } finally { importing.value = false }
}

function onRejected () { notify.error('File rejected — must be a CSV under 20 MB.') }

onMounted(loadCategories)
</script>
