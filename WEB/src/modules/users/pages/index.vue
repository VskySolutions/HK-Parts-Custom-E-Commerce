<template>
  <q-page class="app-page">
    <AppListHeader
      title="Users"
      subtitle="Admin user accounts and their assigned role."
      :breadcrumbs="[{ label: 'Home', icon: 'o_home', to: '/dashboard' }, { label: 'Users' }]"
      show-add
      add-label="New user"
      @add="onAdd"
    >
      <template #actions>
        <q-input
          v-model="search"
          dense
          outlined
          debounce="400"
          placeholder="Search"
          style="min-width: 240px"
          @update:model-value="reload"
        >
          <template #prepend><q-icon name="o_search" /></template>
          <template v-if="search" #append><q-icon name="o_close" class="cursor-pointer" @click="search = ''; reload()" /></template>
        </q-input>
        <q-btn outline color="primary" no-caps icon="o_tune" label="Advanced" class="q-ml-sm" @click="filtersOpen = true">
          <q-badge v-if="activeFilterCount" color="red" floating>{{ activeFilterCount }}</q-badge>
        </q-btn>
      </template>
    </AppListHeader>

    <AppFilterDrawer v-model="filtersOpen" title="Filter users" @clear="clearFilters">
      <AppSelect v-model="activeFilter" label="Status" :options="statusOptions" @update:model-value="reload" />
      <AppSelect v-model="verifiedFilter" label="Email verified" :options="verifiedOptions" @update:model-value="reload" />
    </AppFilterDrawer>

    <AppDataTable
      page-key="users"
      row-key="id"
      title="All users"
      :rows="rows"
      :columns="columns"
      :loading="loading"
      :pagination="pagination"
      show-actions
      @request="onRequest"
      @refresh="reload"
    >
      <template #body-cell-email="cell">
        <q-td :props="cell"><a class="text-primary cursor-pointer text-weight-medium" @click="onManage(cell.row)">{{ cell.row.email }}</a></q-td>
      </template>
      <template #body-cell-roles="cell">
        <q-td :props="cell">
          <template v-if="cell.row.roles && cell.row.roles.length">
            <q-badge
              v-for="r in cell.row.roles"
              :key="r.id"
              color="primary"
              class="q-mr-xs"
              :label="r.name"
            />
          </template>
          <span v-else class="text-grey-6">—</span>
        </q-td>
      </template>

      <template #body-cell-isActive="cell">
        <q-td :props="cell">
          <q-badge
            :color="cell.row.isActive ? 'positive' : 'grey'"
            :label="cell.row.isActive ? 'Active' : 'Inactive'"
          />
        </q-td>
      </template>

      <template #actions="{ row }">
        <q-btn flat round dense icon="o_tune" @click="onManage(row)">
          <q-tooltip>Edit</q-tooltip>
        </q-btn>
        <q-btn flat round dense icon="o_lock_reset" color="primary" :loading="sendingResetId === row.id" @click="onSendReset(row)">
          <q-tooltip>Send password-reset link</q-tooltip>
        </q-btn>
        <q-btn v-if="!isSuperAdmin(row)" flat round dense icon="o_delete" color="negative" @click="onDelete(row)">
          <q-tooltip>Delete</q-tooltip>
        </q-btn>
        <q-btn v-else flat round dense icon="o_shield" color="grey-5">
          <q-tooltip>SuperAdmin accounts are protected and can't be deleted</q-tooltip>
        </q-btn>
      </template>
    </AppDataTable>
  </q-page>
</template>

<script setup>
/*
 * Users list page (WO-62 / REQ-ADM-004): AppListHeader + AppDataTable (server
 * pagination). Create/edit open the full-page user detail (`user-new` / `user-detail`).
 */
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { userApi } from 'modules/users/api'
import { getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'
import { deleteConfirmation } from 'dialogs/delete_confirmation'

const router = useRouter()
const notify = useNotify()

const columns = [
  { name: 'email', label: 'Email', field: 'email', align: 'left', sortable: true },
  { name: 'fullName', label: 'Name', field: 'fullName', align: 'left', sortable: true },
  { name: 'roles', label: 'Role', field: 'roles', align: 'left' },
  { name: 'isActive', label: 'Status', field: 'isActive', align: 'center', sortable: true }
]

const statusOptions = [
  { label: 'All', value: null },
  { label: 'Active', value: true },
  { label: 'Inactive', value: false }
]
const verifiedOptions = [
  { label: 'All', value: null },
  { label: 'Verified', value: true },
  { label: 'Unverified', value: false }
]

const rows = ref([])
const loading = ref(false)
const sendingResetId = ref(null)
const search = ref('')
const activeFilter = ref(null)
const verifiedFilter = ref(null)
const filtersOpen = ref(false)
const pagination = ref({ page: 1, rowsPerPage: 10, rowsNumber: 0 })

const activeFilterCount = computed(() =>
  (activeFilter.value !== null ? 1 : 0) + (verifiedFilter.value !== null ? 1 : 0)
)

function clearFilters () {
  activeFilter.value = null
  verifiedFilter.value = null
  reload()
}

async function fetch (props) {
  const p = props?.pagination || pagination.value
  loading.value = true
  try {
    const result = await userApi.list({
      page: p.page,
      pageSize: p.rowsPerPage,
      search: search.value || undefined,
      isActive: activeFilter.value === null ? undefined : activeFilter.value,
      emailVerified: verifiedFilter.value === null ? undefined : verifiedFilter.value,
      sortBy: p.sortBy || undefined,
      sortDescending: !!p.descending
    })
    const items = Array.isArray(result) ? result : result?.items || result?.data || []
    const total = Array.isArray(result)
      ? result.length
      : result?.totalCount ?? result?.total ?? items.length
    rows.value = items
    pagination.value = { ...p, rowsNumber: total }
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

function onAdd () { router.push({ name: 'user-new' }) }
function onManage (row) { router.push({ name: 'user-detail', params: { id: row.id } }) }

// SuperAdmin accounts are protected server-side; hide the delete action for them in the UI too.
function isSuperAdmin (row) {
  return Array.isArray(row.roles) && row.roles.some((r) => r.name === 'SuperAdmin')
}

async function onSendReset (row) {
  sendingResetId.value = row.id
  try {
    await userApi.sendPasswordReset(row.id)
    notify.success(`Password-reset link sent to ${row.email}`)
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    sendingResetId.value = null
  }
}

async function onDelete (row) {
  if (!(await deleteConfirmation(`the user "${row.email}"`))) return
  try {
    await userApi.remove(row.id)
    notify.success('User deleted')
    reload()
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  }
}

onMounted(() => fetch())
</script>
