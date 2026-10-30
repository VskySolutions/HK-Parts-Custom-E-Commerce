<template>
  <q-page class="app-page">
    <AppListHeader
      title="Roles"
      subtitle="Custom roles and the admin modules they grant."
      :breadcrumbs="[{ label: 'Home', icon: 'o_home', to: '/dashboard' }, { label: 'Roles' }]"
      show-add
      add-label="New role"
      @add="onAdd"
    />

    <AppDataTable
      page-key="roles"
      row-key="id"
      :rows="rows"
      :columns="columns"
      :loading="loading"
      :pagination="pagination"
      show-actions
      @request="onRequest"
    >
      <template #body-cell-name="cell">
        <q-td :props="cell"><a class="text-primary cursor-pointer text-weight-medium" @click="onManage(cell.row)">{{ cell.row.name }}</a></q-td>
      </template>

      <template #body-cell-isSystemRole="cell">
        <q-td :props="cell">
          <q-badge
            :color="cell.row.isSystemRole ? 'primary' : 'grey'"
            :label="cell.row.isSystemRole ? 'System' : 'Custom'"
          />
        </q-td>
      </template>

      <template #body-cell-modules="cell">
        <q-td :props="cell">
          {{ (cell.row.accessibleModules || []).length }}
        </q-td>
      </template>

      <template #actions="{ row }">
        <q-btn flat round dense :icon="row.isSystemRole ? 'o_visibility' : 'o_tune'" @click="onManage(row)">
          <q-tooltip>{{ row.isSystemRole ? 'View (read-only)' : 'Edit' }}</q-tooltip>
        </q-btn>
        <q-btn
          flat
          round
          dense
          icon="o_delete"
          color="negative"
          :disable="row.isSystemRole"
          @click="onDelete(row)"
        >
          <q-tooltip>{{ row.isSystemRole ? 'System roles cannot be deleted' : 'Delete' }}</q-tooltip>
        </q-btn>
      </template>
    </AppDataTable>
  </q-page>
</template>

<script setup>
/*
 * Roles list page (WO-62 / REQ-ADM-004): AppListHeader + AppDataTable. Create/edit
 * open the full-page role detail (`role-new` / `role-detail`). System roles
 * (isSystemRole) are read-only — they open a read-only detail and cannot be deleted.
 *
 * The list endpoint returns a plain (unpaged) array; rowsPerPage is 0 ("All")
 * so the table shows every role in one page.
 */
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { roleApi } from 'modules/roles/api'
import { getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'
import { deleteConfirmation } from 'dialogs/delete_confirmation'

const router = useRouter()
const notify = useNotify()

const columns = [
  { name: 'name', label: 'Name', field: 'name', align: 'left' },
  { name: 'isSystemRole', label: 'Type', field: 'isSystemRole', align: 'left' },
  { name: 'modules', label: 'Modules', field: 'accessibleModules', align: 'left' }
]

const rows = ref([])
const loading = ref(false)
const pagination = ref({ page: 1, rowsPerPage: 0, rowsNumber: 0 })

async function fetch (props) {
  const p = props?.pagination || pagination.value
  loading.value = true
  try {
    const result = await roleApi.list()
    const items = Array.isArray(result) ? result : result?.items || result?.data || []
    rows.value = items
    pagination.value = { ...p, rowsNumber: items.length }
  } catch (err) {
    rows.value = []
    pagination.value = { ...p, rowsNumber: 0 }
    notify.error(getApiErrorMessage(err))
  } finally {
    loading.value = false
  }
}

function onRequest (props) {
  fetch(props)
}

function reload () {
  fetch({ pagination: { ...pagination.value, page: 1 } })
}

function onAdd () { router.push({ name: 'role-new' }) }
function onManage (row) { router.push({ name: 'role-detail', params: { id: row.id } }) }

async function onDelete (row) {
  if (row.isSystemRole) return
  if (!(await deleteConfirmation(`the role "${row.name}"`))) return
  try {
    await roleApi.remove(row.id)
    notify.success('Role deleted')
    reload()
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  }
}

onMounted(() => fetch())
</script>
