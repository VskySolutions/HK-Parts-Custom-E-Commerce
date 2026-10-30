<template>
  <q-page class="app-page">
    <AppListHeader
      title="Email Templates"
      subtitle="Transactional and marketing notification templates."
      :breadcrumbs="[{ label: 'Home', icon: 'o_home', to: '/dashboard' }, { label: 'Email Templates' }]"
    >
      <template #actions>
        <q-input
          v-model="search"
          dense
          outlined
          debounce="400"
          placeholder="Search name or key"
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

    <AppFilterDrawer v-model="filtersOpen" title="Filter templates" @clear="clearFilters">
      <AppSelect v-model="category" label="Category" :options="categoryOptions" @update:model-value="reload" />
      <AppSelect v-model="enabled" label="Status" :options="statusOptions" @update:model-value="reload" />
    </AppFilterDrawer>

    <AppDataTable
      page-key="email-templates"
      row-key="templateKey"
      title="All templates"
      :rows="rows"
      :columns="columns"
      :loading="loading"
      :pagination="tablePagination"
      no-data-label="No templates match your filters."
      show-actions
      @row-click="onRowClick"
      @refresh="reload"
    >
      <template #body-cell-name="cell">
        <q-td :props="cell">
          <div class="row items-center no-wrap q-gutter-xs">
            <span class="text-weight-medium">{{ cell.row.name }}</span>
            <q-icon v-if="cell.row.isCritical" name="o_lock" size="16px" color="orange-8">
              <q-tooltip>Critical template — disabling requires confirmation.</q-tooltip>
            </q-icon>
          </div>
          <div class="text-caption text-grey-6">{{ cell.row.templateKey }}</div>
        </q-td>
      </template>

      <template #body-cell-category="cell">
        <q-td :props="cell">
          <q-badge
            :color="cell.row.category === 'Marketing' ? 'purple' : 'primary'"
            :label="cell.row.category"
            outline
          />
        </q-td>
      </template>

      <template #body-cell-enabled="cell">
        <q-td :props="cell">
          <q-badge
            :color="cell.row.enabled ? 'positive' : 'grey'"
            :label="cell.row.enabled ? 'Enabled' : 'Disabled'"
          />
        </q-td>
      </template>

      <template #body-cell-smtp="cell">
        <q-td :props="cell">
          <div v-if="cell.row.hasSmtpConfigured" class="text-body2 ellipsis">
            {{ cell.row.assignedSmtpAccountName || '—' }}
          </div>
          <q-chip
            v-else
            dense
            square
            color="orange-1"
            text-color="orange-9"
            icon="o_warning"
            class="q-ma-none"
          >
            No SMTP account
            <q-tooltip>
              No enabled SMTP account is assigned to the {{ cell.row.category }} category.
              Test-sends and live delivery for this template will fail until one is configured.
            </q-tooltip>
          </q-chip>
        </q-td>
      </template>

      <template #actions="{ row }">
        <q-btn flat round dense icon="o_tune" @click.stop="openEditor(row)">
          <q-tooltip>Open editor</q-tooltip>
        </q-btn>
      </template>
    </AppDataTable>
  </q-page>
</template>

<script setup>
/*
 * Email template library (WO-80, REQ-ENT-002): a filterable list of the seeded
 * notification templates. Filters (category, enabled state, name/key search) are
 * applied server-side; rows are paginated client-side. Each row opens the editor.
 */
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { emailTemplatesApi } from 'modules/email-templates/api'
import { getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'

const router = useRouter()
const notify = useNotify()

const columns = [
  { name: 'name', label: 'Template', field: 'name', align: 'left', sortable: true },
  { name: 'category', label: 'Category', field: 'category', align: 'left', sortable: true },
  { name: 'enabled', label: 'Status', field: 'enabled', align: 'left', sortable: true },
  { name: 'smtp', label: 'SMTP account', field: 'assignedSmtpAccountName', align: 'left' }
]

const categoryOptions = [
  { label: 'All categories', value: null },
  { label: 'Transactional', value: 'Transactional' },
  { label: 'Marketing', value: 'Marketing' }
]
const statusOptions = [
  { label: 'All statuses', value: null },
  { label: 'Enabled', value: true },
  { label: 'Disabled', value: false }
]

// Stable descriptor -> client-side pagination (no rowsNumber = client mode).
const tablePagination = { sortBy: 'name', rowsPerPage: 25 }

const rows = ref([])
const loading = ref(false)
const category = ref(null)
const enabled = ref(null)
const search = ref('')
const filtersOpen = ref(false)

const activeFilterCount = computed(() => (category.value !== null ? 1 : 0) + (enabled.value !== null ? 1 : 0))

function clearFilters () {
  category.value = null
  enabled.value = null
  reload()
}

async function load () {
  loading.value = true
  try {
    const result = await emailTemplatesApi.list({
      category: category.value ?? undefined,
      enabled: enabled.value ?? undefined,
      search: search.value || undefined
    })
    rows.value = Array.isArray(result) ? result : result?.items || []
  } catch (err) {
    rows.value = []
    notify.error(getApiErrorMessage(err))
  } finally {
    loading.value = false
  }
}

function reload () {
  load()
}

function openEditor (row) {
  router.push({ name: 'email-template-editor', query: { key: row.templateKey } })
}

function onRowClick (evt, row) {
  openEditor(row)
}

onMounted(load)
</script>
