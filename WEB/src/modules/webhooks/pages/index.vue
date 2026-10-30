<template>
  <q-page class="app-page">
    <AppListHeader
      title="Webhooks"
      :breadcrumbs="[{ label: 'Home', icon: 'o_home', to: '/dashboard' }, { label: 'Webhooks' }]"
      :show-add="canWrite"
      add-label="New webhook"
      @add="onAdd"
    />

    <AppDataTable
      page-key="admin-webhooks"
      row-key="id"
      title="Endpoints"
      :rows="rows"
      :columns="columns"
      :loading="loading"
      :pagination="pagination"
      show-actions
      @refresh="load"
    >
      <template #body-cell-url="cell">
        <q-td :props="cell">
          <a class="text-primary cursor-pointer text-weight-medium" @click="onManage(cell.row)">{{ cell.row.url }}</a>
          <div v-if="cell.row.description" class="text-caption text-grey-6">{{ cell.row.description }}</div>
        </q-td>
      </template>
      <template #body-cell-events="cell">
        <q-td :props="cell">
          <q-badge v-for="e in cell.row.eventTypes" :key="e" outline color="primary" :label="e" class="q-mr-xs q-mb-xs" />
        </q-td>
      </template>
      <template #body-cell-isActive="cell">
        <q-td :props="cell"><q-badge :color="cell.row.isActive ? 'positive' : 'grey'" :label="cell.row.isActive ? 'Active' : 'Paused'" /></q-td>
      </template>
      <template #body-cell-createdOnUtc="cell">
        <q-td :props="cell">{{ formatDate(cell.row.createdOnUtc) }}</q-td>
      </template>
      <template #actions="{ row }">
        <q-btn v-if="canWrite" flat round dense icon="o_tune" @click="onManage(row)"><q-tooltip>Edit</q-tooltip></q-btn>
        <q-btn v-if="canWrite" flat round dense icon="o_delete" color="negative" @click="onDelete(row)"><q-tooltip>Delete</q-tooltip></q-btn>
      </template>
    </AppDataTable>
  </q-page>
</template>

<script setup>
/*
 * Webhooks list page. Create/edit open the full-page webhook detail
 * (`webhook-new` / `webhook-detail`), which also reveals the one-time signing secret and delivery history.
 */
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { getApiErrorMessage } from 'services/api'
import { usePermissions } from 'composables/usePermissions'
import { useNotify } from 'composables/useNotify'
import { deleteConfirmation } from 'dialogs/delete_confirmation'
import { webhookApi } from 'modules/webhooks/api'
import { formatDate } from 'modules/orders/api'
import AppListHeader from 'components/common/AppListHeader.vue'
import AppDataTable from 'components/common/AppDataTable.vue'

const router = useRouter()
const notify = useNotify()
const { has } = usePermissions()
const canWrite = computed(() => has('Webhooks.Write'))

const columns = [
  { name: 'url', label: 'Endpoint', field: 'url', align: 'left' },
  { name: 'events', label: 'Events', field: 'eventTypes', align: 'left' },
  { name: 'isActive', label: 'Status', field: 'isActive', align: 'center' },
  { name: 'createdOnUtc', label: 'Created', field: 'createdOnUtc', align: 'left' }
]

const rows = ref([])
const loading = ref(false)
const pagination = ref({ rowsPerPage: 0 })

async function load () {
  loading.value = true
  try {
    const r = await webhookApi.list()
    rows.value = Array.isArray(r) ? r : r?.items || []
  } catch (e) { rows.value = []; notify.error(getApiErrorMessage(e)) } finally { loading.value = false }
}

function onAdd () { router.push({ name: 'webhook-new' }) }
function onManage (row) { router.push({ name: 'webhook-detail', params: { id: row.id } }) }

async function onDelete (row) {
  if (!(await deleteConfirmation(`the webhook "${row.url}"`))) return
  try { await webhookApi.remove(row.id); notify.success('Webhook deleted'); load() } catch (e) { notify.error(getApiErrorMessage(e)) }
}

onMounted(load)
</script>
