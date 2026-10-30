<template>
  <q-page class="app-page">
    <AppListHeader
      title="Page groups"
      :breadcrumbs="[{ label: 'Home', icon: 'o_home', to: '/dashboard' }, { label: 'CMS' }, { label: 'Page groups' }]"
      :show-add="canWrite"
      add-label="New group"
      @add="onAdd"
    >
      <template #actions>
        <q-input v-model="search" dense outlined debounce="300" placeholder="Search groups" style="min-width: 240px">
          <template #prepend><q-icon name="o_search" /></template>
          <template v-if="search" #append><q-icon name="o_close" class="cursor-pointer" @click="search = ''" /></template>
        </q-input>
      </template>
    </AppListHeader>

    <AppDataTable
      page-key="cms-page-groups"
      row-key="id"
      title="All page groups"
      :rows="filteredRows"
      :columns="columns"
      :loading="loading"
      :pagination="pagination"
      show-actions
      no-data-label="No page groups yet."
      @refresh="load"
    >
      <template #body-cell-name="cell">
        <q-td :props="cell">
          <a class="text-primary cursor-pointer text-weight-medium" @click="onEdit(cell.row)">{{ cell.row.name }}</a>
        </q-td>
      </template>

      <template #body-cell-slug="cell">
        <q-td :props="cell"><span class="text-grey-8">/{{ cell.row.slug }}</span></q-td>
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
      :title="editing ? 'Edit page group' : 'New page group'"
      :saving="saving"
      @submit="onSubmit"
      @cancel="drawerOpen = false"
    >
      <div class="q-gutter-y-sm">
        <AppTextField v-model="form.name" label="Name" required :v="v$.name" placeholder="e.g. Footer links" :disable="!canWrite" />
        <AppTextField v-model="form.slug" label="Slug" :v="v$.slug" placeholder="e.g. footer-links" hint="Auto-filled from the name until you edit it" :disable="!canWrite" />
        <AppTextField v-model="form.displayOrder" label="Display order" type="number" hint="Lower shows first" :disable="!canWrite" />
      </div>
    </AppFormDrawer>
  </q-page>
</template>

<script setup>
/*
 * CMS Page Groups list + inline manage (WO-54). The list endpoint returns a small (unpaged) set, so the
 * table runs client-side (rowsPerPage 0) and the search box filters loaded rows in memory. Create/edit
 * happen in a right-side AppFormDrawer rather than a routed detail page — groups are just name/slug/order.
 * Slug is derived from the name until the user edits it. Delete confirms first.
 */
import { ref, reactive, computed, watch, onMounted } from 'vue'
import useVuelidate from '@vuelidate/core'
import { cmsPageGroupApi } from '../api'
import { getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'
import { usePermissions } from 'composables/usePermissions'
import { deleteConfirmation } from 'dialogs/delete_confirmation'
import { required, maxLength } from 'validators'

const notify = useNotify()
const { has } = usePermissions()
const canWrite = computed(() => has('Cms.Write'))

const columns = [
  { name: 'name', label: 'Name', field: 'name', align: 'left' },
  { name: 'slug', label: 'Slug', field: 'slug', align: 'left' },
  { name: 'displayOrder', label: 'Display order', field: 'displayOrder', align: 'right' }
]

// Client-side table: page-group lists are small, so show every group on one page.
const pagination = ref({ rowsPerPage: 0 })

const rows = ref([])
const loading = ref(false)
const search = ref('')

const filteredRows = computed(() => {
  const q = search.value.trim().toLowerCase()
  if (!q) return rows.value
  return rows.value.filter((g) =>
    (g.name || '').toLowerCase().includes(q) || (g.slug || '').toLowerCase().includes(q)
  )
})

async function load () {
  loading.value = true
  try {
    const result = await cmsPageGroupApi.list()
    rows.value = Array.isArray(result) ? result : result?.items || result?.data || []
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
const form = reactive({ name: '', slug: '', displayOrder: 0 })
const rules = {
  name: { required, maxLength: maxLength(200) },
  slug: { maxLength: maxLength(220) }
}
const v$ = useVuelidate(rules, form)

function slugify (value) {
  return (value || '').toString().trim().toLowerCase()
    .replace(/['’"]/g, '')
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
}
let lastAutoSlug = ''
watch(() => form.name, (name) => {
  if (!form.slug || form.slug === lastAutoSlug) {
    lastAutoSlug = slugify(name)
    form.slug = lastAutoSlug
  }
})

function onAdd () {
  editing.value = null
  form.name = ''
  form.slug = ''
  form.displayOrder = 0
  lastAutoSlug = ''
  v$.value.$reset()
  drawerOpen.value = true
}

function onEdit (row) {
  editing.value = row
  form.name = row.name || ''
  form.slug = row.slug || ''
  form.displayOrder = row.displayOrder ?? 0
  lastAutoSlug = '' // an existing slug is user-owned; don't auto-overwrite it when the name changes
  v$.value.$reset()
  drawerOpen.value = true
}

async function onSubmit () {
  const ok = await v$.value.$validate()
  if (!ok) { notify.warning('Fill in the required fields'); return }
  saving.value = true
  try {
    const payload = { name: form.name, slug: form.slug || null, displayOrder: Number(form.displayOrder) || 0 }
    if (editing.value) await cmsPageGroupApi.update(editing.value.id, payload)
    else await cmsPageGroupApi.create(payload)
    notify.success(editing.value ? 'Page group saved' : 'Page group created')
    drawerOpen.value = false
    await load()
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    saving.value = false
  }
}

async function onDelete (row) {
  if (!(await deleteConfirmation(`the page group "${row.name}"`))) return
  try {
    await cmsPageGroupApi.remove(row.id)
    notify.success('Page group deleted')
    await load()
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  }
}

onMounted(load)
</script>
