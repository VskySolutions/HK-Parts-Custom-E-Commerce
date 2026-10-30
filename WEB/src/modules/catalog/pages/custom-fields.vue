<template>
  <q-page class="app-page">
    <AppListHeader
      title="Product custom fields"
      :breadcrumbs="[{ label: 'Home', icon: 'o_home', to: '/dashboard' }, { label: 'Catalog' }, { label: 'Custom fields' }]"
      :show-add="canWrite"
      add-label="New field"
      @add="onAdd"
    >
      <template #actions>
        <q-input v-model="search" dense outlined debounce="300" placeholder="Search fields" style="min-width: 240px">
          <template #prepend><q-icon name="o_search" /></template>
          <template v-if="search" #append><q-icon name="o_close" class="cursor-pointer" @click="search = ''" /></template>
        </q-input>
      </template>
    </AppListHeader>

    <AppDataTable
      page-key="catalog-custom-fields"
      row-key="id"
      title="All custom fields"
      :rows="filteredRows"
      :columns="columns"
      :loading="loading"
      :pagination="pagination"
      show-actions
      no-data-label="No custom fields yet. For option-valued, filterable properties (Condition, Size…) use specification attributes instead."
      @refresh="load"
    >
      <template #body-cell-name="cell">
        <q-td :props="cell">
          <a class="text-primary cursor-pointer text-weight-medium" @click="onEdit(cell.row)">{{ cell.row.name }}</a>
        </q-td>
      </template>
      <template #body-cell-code="cell">
        <q-td :props="cell"><code class="text-grey-8">{{ cell.row.code }}</code></q-td>
      </template>
      <template #body-cell-isRequired="cell">
        <q-td :props="cell" class="text-center">
          <q-icon v-if="cell.row.isRequired" name="o_check" color="primary" size="18px" />
          <span v-else class="text-grey-5">—</span>
        </q-td>
      </template>
      <template #body-cell-isActive="cell">
        <q-td :props="cell" class="text-center">
          <q-chip dense square :color="cell.row.isActive ? 'green-1' : 'grey-3'" :text-color="cell.row.isActive ? 'positive' : 'grey-8'">
            {{ cell.row.isActive ? 'Active' : 'Inactive' }}
          </q-chip>
        </q-td>
      </template>

      <template #actions="{ row }">
        <q-btn flat round dense icon="o_tune" @click="onEdit(row)">
          <q-tooltip>Edit</q-tooltip>
        </q-btn>
        <q-btn v-if="canWrite" flat round dense icon="o_delete" color="negative" @click="onDelete(row)">
          <q-tooltip>Delete</q-tooltip>
        </q-btn>
      </template>
    </AppDataTable>

    <AppFormDrawer
      v-model="drawerOpen"
      :title="editing ? 'Edit custom field' : 'New custom field'"
      :saving="saving"
      @submit="onSubmit"
      @cancel="drawerOpen = false"
    >
      <div class="q-gutter-y-sm">
        <AppTextField v-model="form.name" label="Name" required :v="v$.name" placeholder="e.g. Warranty notes" :disable="!canWrite" />
        <AppTextField v-model="form.code" label="Code" required :v="v$.code" placeholder="e.g. warranty_notes" hint="Stable key for import/export — auto-filled from the name until you edit it" :disable="!canWrite" />
        <AppSelect v-model="form.fieldType" label="Input type" :options="customFieldTypeOptions" :disable="!canWrite" />
        <AppTextField v-model="form.displayOrder" label="Display order" type="number" hint="Lower shows first" :disable="!canWrite" />
        <div class="row q-gutter-md">
          <q-toggle v-model="form.isRequired" label="Required" color="primary" :disable="!canWrite" />
          <q-toggle v-model="form.isActive" label="Active" color="primary" :disable="!canWrite" />
        </div>
      </div>
    </AppFormDrawer>
  </q-page>
</template>

<script setup>
/*
 * Product custom fields — merchant-defined free-text fields whose values are set per product on the
 * product page's General tab (replaces the PHP store's custom_varchar_N / custom_longtext_N columns).
 * Small unpaged list → client-side table; create/edit in a drawer; code derived from the name until edited.
 */
import { ref, reactive, computed, watch, onMounted } from 'vue'
import useVuelidate from '@vuelidate/core'
import { productCustomFieldApi, customFieldTypeOptions } from 'modules/catalog/api'
import { getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'
import { usePermissions } from 'composables/usePermissions'
import { deleteConfirmation } from 'dialogs/delete_confirmation'
import { required, maxLength, helpers } from 'validators'

const notify = useNotify()
const { has } = usePermissions()
const canWrite = computed(() => has('Catalog.Write'))

const columns = [
  { name: 'name', label: 'Name', field: 'name', align: 'left', sortable: true },
  { name: 'code', label: 'Code', field: 'code', align: 'left', sortable: true },
  { name: 'fieldType', label: 'Input type', field: (r) => (customFieldTypeOptions.find((o) => o.value === r.fieldType)?.label || r.fieldType), align: 'left' },
  { name: 'isRequired', label: 'Required', field: 'isRequired', align: 'center' },
  { name: 'isActive', label: 'Status', field: 'isActive', align: 'center' },
  { name: 'displayOrder', label: 'Display order', field: 'displayOrder', align: 'right', sortable: true }
]
const pagination = ref({ rowsPerPage: 0 })

const rows = ref([])
const loading = ref(false)
const search = ref('')

const filteredRows = computed(() => {
  const q = search.value.trim().toLowerCase()
  if (!q) return rows.value
  return rows.value.filter((f) => (f.name || '').toLowerCase().includes(q) || (f.code || '').toLowerCase().includes(q))
})

async function load () {
  loading.value = true
  try {
    const result = await productCustomFieldApi.list()
    rows.value = Array.isArray(result) ? result : []
  } catch (err) {
    rows.value = []
    notify.error(getApiErrorMessage(err))
  } finally {
    loading.value = false
  }
}

// ---- Create / edit drawer ----
const drawerOpen = ref(false)
const editing = ref(null)
const saving = ref(false)
const EMPTY = { name: '', code: '', fieldType: 'Text', isRequired: false, isActive: true, displayOrder: 0 }
const form = reactive({ ...EMPTY })
const rules = {
  name: { required, maxLength: maxLength(200) },
  code: {
    required,
    maxLength: maxLength(100),
    format: helpers.withMessage('Lowercase letters, digits and underscores only', (v) => !v || /^[a-z0-9_]+$/.test(v))
  }
}
const v$ = useVuelidate(rules, form)

function codify (value) {
  return (value || '').toString().trim().toLowerCase()
    .replace(/['’"]/g, '')
    .replace(/[^a-z0-9]+/g, '_')
    .replace(/^_+|_+$/g, '')
}
let lastAutoCode = ''
watch(() => form.name, (name) => {
  if (!form.code || form.code === lastAutoCode) {
    lastAutoCode = codify(name)
    form.code = lastAutoCode
  }
})

function onAdd () {
  editing.value = null
  Object.assign(form, EMPTY)
  lastAutoCode = ''
  v$.value.$reset()
  drawerOpen.value = true
}

function onEdit (row) {
  editing.value = row
  Object.assign(form, {
    name: row.name || '',
    code: row.code || '',
    fieldType: row.fieldType || 'Text',
    isRequired: !!row.isRequired,
    isActive: !!row.isActive,
    displayOrder: row.displayOrder ?? 0
  })
  lastAutoCode = '' // an existing code is referenced by imports; never auto-overwrite it
  v$.value.$reset()
  drawerOpen.value = true
}

async function onSubmit () {
  const ok = await v$.value.$validate()
  if (!ok) { notify.warning('Fill in the required fields'); return }
  saving.value = true
  try {
    const payload = { ...form, displayOrder: Number(form.displayOrder) || 0 }
    if (editing.value) await productCustomFieldApi.update(editing.value.id, payload)
    else await productCustomFieldApi.create(payload)
    notify.success(editing.value ? 'Custom field saved' : 'Custom field created')
    drawerOpen.value = false
    await load()
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    saving.value = false
  }
}

async function onDelete (row) {
  if (!(await deleteConfirmation(`the custom field "${row.name}"`, { message: `Delete "${row.name}"? Its value is removed from every product.` }))) return
  try {
    await productCustomFieldApi.remove(row.id)
    notify.success('Custom field deleted')
    await load()
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  }
}

onMounted(load)
</script>
