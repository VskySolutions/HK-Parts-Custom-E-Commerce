<template>
  <div>
    <PanelHeader :item="item">
      <template #actions>
        <q-btn flat no-caps color="primary" icon="o_refresh" label="Reload" :loading="loading" @click="load" />
        <q-btn v-if="canWrite" unelevated no-caps color="primary" icon="o_add" label="New account" @click="onAdd" />
      </template>
    </PanelHeader>

    <q-banner
      v-for="cat in uncoveredCategories"
      :key="cat"
      dense
      rounded
      class="bg-orange-1 text-orange-9 q-mb-sm"
    >
      <template #avatar><q-icon name="o_warning" color="orange-9" /></template>
      No enabled SMTP account is assigned to the <strong>{{ cat }}</strong> category — those emails cannot be sent.
    </q-banner>

    <AppDataTable
      page-key="smtp-accounts"
      row-key="id"
      :rows="rows"
      :columns="columns"
      :loading="loading"
      :pagination="pagination"
      show-actions
      no-data-label="No SMTP accounts configured yet."
      @request="onRequest"
    >
      <template #body-cell-displayName="cell">
        <q-td :props="cell"><a class="text-primary cursor-pointer text-weight-medium" @click="onManage(cell.row)">{{ cell.row.displayName }}</a></q-td>
      </template>

      <template #body-cell-category="cell">
        <q-td :props="cell">
          <q-badge v-if="cell.row.category" :color="categoryColor(cell.row.category)" :label="cell.row.category" />
          <span v-else class="text-grey-6">Unassigned</span>
        </q-td>
      </template>

      <template #body-cell-enabled="cell">
        <q-td :props="cell">
          <q-toggle
            :model-value="cell.row.enabled"
            color="primary"
            :disable="!canWrite || togglingId === cell.row.id"
            @update:model-value="onToggleEnabled(cell.row, $event)"
          />
        </q-td>
      </template>

      <template #actions="{ row }">
        <q-btn flat round dense icon="o_send" @click="onTestSend(row)">
          <q-tooltip>Test send</q-tooltip>
        </q-btn>
        <q-btn v-if="canWrite" flat round dense icon="o_tune" @click="onManage(row)">
          <q-tooltip>Edit</q-tooltip>
        </q-btn>
        <q-btn v-if="canWrite" flat round dense icon="o_delete" color="negative" @click="onDelete(row)">
          <q-tooltip>Delete</q-tooltip>
        </q-btn>
      </template>
    </AppDataTable>

    <!-- Test-send dialog -->
    <q-dialog v-model="testDialogOpen">
      <q-card style="min-width: 380px">
        <q-card-section class="row items-center q-pb-none">
          <div class="text-h6">Send test email</div>
          <q-space />
          <q-btn v-close-popup flat round dense icon="o_close" />
        </q-card-section>
        <q-card-section>
          <div class="text-caption text-grey-7 q-mb-sm">
            Sends a test message through <strong>{{ testTarget && testTarget.displayName }}</strong>.
          </div>
          <AppTextField v-model="testForm.email" label="Recipient email" required :v="vTest$.email" type="email" @keyup.enter="onRunTestSend" />
          <q-banner
            v-if="testResult"
            dense
            rounded
            class="q-mt-md"
            :class="testResult.success ? 'bg-green-1 text-green-9' : 'bg-red-1 text-red-9'"
          >
            <template #avatar><q-icon :name="testResult.success ? 'o_check_circle' : 'o_error'" /></template>
            {{ testResult.message }}
            <div v-if="testResult.testedAtUtc" class="text-caption">{{ $datetime(testResult.testedAtUtc) }}</div>
          </q-banner>
        </q-card-section>
        <q-card-actions align="right" class="q-pa-md q-pt-none">
          <q-btn v-close-popup flat no-caps color="grey-8" label="Close" />
          <q-btn unelevated no-caps color="primary" icon="o_send" label="Send test" :loading="testSending" @click="onRunTestSend" />
        </q-card-actions>
      </q-card>
    </q-dialog>
  </div>
</template>

<script setup>
/*
 * SmtpAccountsPanel: SMTP sending-account CRUD list with a per-row enabled toggle, category badge, a
 * test-send dialog, and a warning when a NotificationCategory has no enabled account. Add/edit uses the
 * routed detail page (smtp-account-new / smtp-account-detail), returning to this section of the hub.
 */
import { ref, reactive, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import useVuelidate from '@vuelidate/core'
import { required, email } from 'validators'
import { smtpAccountApi } from 'modules/integration-credentials/api'
import { getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'
import { usePermissions } from 'composables/usePermissions'
import { deleteConfirmation } from 'dialogs/delete_confirmation'
import { useAuthStore } from 'stores/auth'
import PanelHeader from 'modules/integration-credentials/components/PanelHeader.vue'

defineProps({ item: { type: Object, required: true } })

const router = useRouter()
const notify = useNotify()
const auth = useAuthStore()
const { has } = usePermissions()
const canWrite = computed(() => has('SmtpAccounts.Write'))

const NOTIFICATION_CATEGORIES = ['Transactional', 'Marketing']

const columns = [
  { name: 'displayName', label: 'Name', field: 'displayName', align: 'left', sortable: true },
  { name: 'host', label: 'Host', field: 'host', align: 'left' },
  { name: 'port', label: 'Port', field: 'port', align: 'left' },
  { name: 'fromEmail', label: 'From', field: 'fromEmail', align: 'left' },
  { name: 'category', label: 'Category', field: 'category', align: 'left' },
  { name: 'enabled', label: 'Enabled', field: 'enabled', align: 'center' }
]

const rows = ref([])
const loading = ref(false)
const togglingId = ref(null)
const pagination = ref({ page: 1, rowsPerPage: 10, sortBy: 'displayName', descending: false })

const uncoveredCategories = computed(() =>
  NOTIFICATION_CATEGORIES.filter((cat) => !rows.value.some((a) => a.enabled && a.category === cat))
)

function categoryColor (cat) {
  if (cat === 'Transactional') return 'blue-7'
  if (cat === 'Marketing') return 'purple-6'
  return 'grey'
}

function onRequest (p) {
  if (p && p.pagination) pagination.value = p.pagination
}

async function load () {
  loading.value = true
  try {
    const result = await smtpAccountApi.list()
    rows.value = Array.isArray(result) ? result : result?.items || result?.data || []
  } catch (err) {
    rows.value = []
    notify.error(getApiErrorMessage(err))
  } finally {
    loading.value = false
  }
}

function onAdd () { router.push({ name: 'smtp-account-new' }) }
function onManage (row) { router.push({ name: 'smtp-account-detail', params: { id: row.id } }) }

async function onToggleEnabled (row, value) {
  togglingId.value = row.id
  try {
    await smtpAccountApi.update(row.id, {
      displayName: row.displayName,
      host: row.host,
      port: row.port,
      username: row.username || null,
      password: null, // omit -> API keeps the stored password
      fromName: row.fromName,
      fromEmail: row.fromEmail,
      encryptionMode: row.encryptionMode,
      authMethod: row.authMethod,
      category: row.category || null,
      enabled: value
    })
    notify.success(value ? 'Account enabled' : 'Account disabled')
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    togglingId.value = null
    // Re-fetch: enabling one account disables any other in the same category.
    load()
  }
}

async function onDelete (row) {
  if (!(await deleteConfirmation(`the "${row.displayName}" SMTP account`))) return
  try {
    await smtpAccountApi.remove(row.id)
    notify.success('SMTP account deleted')
    load()
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  }
}

// ---- Test-send dialog -------------------------------------------------------
const testDialogOpen = ref(false)
const testTarget = ref(null)
const testSending = ref(false)
const testResult = ref(null)
const testForm = reactive({ email: '' })
const vTest$ = useVuelidate({ email: { required, email } }, testForm)

function onTestSend (row) {
  testTarget.value = row
  testForm.email = auth.user?.email || ''
  testResult.value = null
  vTest$.value.$reset()
  testDialogOpen.value = true
}

async function onRunTestSend () {
  const ok = await vTest$.value.$validate()
  if (!ok) return
  testSending.value = true
  testResult.value = null
  try {
    const result = await smtpAccountApi.testSend(testTarget.value.id, testForm.email.trim())
    testResult.value = result
    if (result && result.success) notify.success(result.message || 'Test email sent')
    else notify.error((result && result.message) || 'Test send failed')
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    testSending.value = false
  }
}

onMounted(load)
</script>
