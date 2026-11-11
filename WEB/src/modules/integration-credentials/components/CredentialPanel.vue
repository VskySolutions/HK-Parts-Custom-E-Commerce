<template>
  <div>
    <PanelHeader :item="item">
      <template #actions>
        <q-btn flat color="primary" icon="o_refresh" no-caps :loading="loading" @click="load" />
        <q-btn
          v-if="canWrite"
          unelevated
          color="primary"
          icon="o_add"
          :label="`Add ${item.label}`"
          no-caps
          @click="onAdd"
        />
      </template>
    </PanelHeader>

    <AppDataTable
      :page-key="`integration-credentials-${item.key}`"
      row-key="id"
      :rows="rows"
      :columns="columns"
      :loading="loading"
      :pagination="pagination"
      show-actions
      :no-data-label="`No ${item.label} credentials yet.`"
      @request="onRequest"
    >
      <template #body-cell-name="cell">
        <q-td :props="cell">
          <a class="text-primary cursor-pointer text-weight-medium" @click="onEdit(cell.row)">{{ cell.row.name }}</a>
        </q-td>
      </template>

      <template #body-cell-active="cell">
        <q-td :props="cell" class="text-center">
          <q-badge :color="cell.row.active ? 'positive' : 'grey-5'" :label="cell.row.active ? 'Active' : 'Inactive'" />
        </q-td>
      </template>

      <template #body-cell-isProduction="cell">
        <q-td :props="cell" class="text-center">
          <q-badge
            :color="cell.row.isProduction ? 'blue-7' : 'orange-7'"
            :label="cell.row.isProduction ? 'Production' : 'Sandbox'"
            outline
          />
        </q-td>
      </template>

      <template #body-cell-transactionFeePercent="cell">
        <q-td :props="cell" class="text-right">
          {{ cell.row.transactionFeePercent != null ? cell.row.transactionFeePercent + '%' : '—' }}
        </q-td>
      </template>

      <template #body-cell-updatedOnUtc="cell">
        <q-td :props="cell">{{ $datetime(cell.row.updatedOnUtc) }}</q-td>
      </template>

      <template #actions="{ row }">
        <q-btn
          v-if="canWrite"
          flat round dense
          :icon="row.active ? 'o_toggle_on' : 'o_toggle_off'"
          :color="row.active ? 'positive' : 'grey-6'"
          :loading="togglingId === row.id"
          @click="onToggleActive(row)"
        >
          <q-tooltip>{{ row.active ? 'Set inactive' : 'Set active' }}</q-tooltip>
        </q-btn>
        <q-btn v-if="canWrite && item.testable" flat round dense icon="o_science" :loading="testingId === row.id" @click="onTest(row)">
          <q-tooltip>{{ item.testLabel || 'Test rates' }}</q-tooltip>
        </q-btn>
        <q-btn v-if="canWrite" flat round dense icon="o_tune" @click="onEdit(row)">
          <q-tooltip>Edit</q-tooltip>
        </q-btn>
        <q-btn v-if="canWrite" flat round dense icon="o_delete" color="negative" @click="onDelete(row)">
          <q-tooltip>Delete</q-tooltip>
        </q-btn>
      </template>
    </AppDataTable>

    <AppFormDrawer
      v-model="drawer"
      :title="`${editingId ? 'Edit' : 'Add'} ${item.label} credential`"
      :saving="saving"
      submit-label="Save"
      :default-width="item.drawerWidth || 460"
      :max-width="Math.max(720, item.drawerWidth || 0)"
      :width-key="item.drawerWidth ? `formDrawerWidth.${item.key}` : undefined"
      @submit="onSave"
    >
      <div class="q-gutter-y-sm">
        <AppTextField
          v-model="form.name"
          label="Name"
          required
          :rules="[requiredRule]"
          placeholder="e.g. Live account"
          maxlength="200"
        />

        <div class="row q-col-gutter-md">
          <div class="col-12 col-sm-6">
            <AppFieldLabel label="Active" />
            <q-toggle v-model="form.active" :label="form.active ? 'Used at runtime' : 'Not used'" color="positive" />
          </div>
          <div class="col-12 col-sm-6">
            <AppFieldLabel label="Environment" />
            <q-toggle v-model="form.isProduction" :label="form.isProduction ? 'Production' : 'Sandbox'" color="blue-7" />
          </div>
        </div>

        <q-separator class="q-my-sm" />

        <!-- Fields with `col: 'half' | 'third'` share a row on a wide drawer (short numbers, selects). -->
        <div class="row q-col-gutter-md">
        <div v-for="field in item.fields" :key="field.key" :class="fieldCol(field)">
          <template v-if="field.type === 'toggle'">
            <q-toggle v-model="form.fields[field.key]" :label="field.label" color="primary" />
          </template>
          <AppSelect
            v-else-if="field.type === 'select' || field.type === 'multiselect'"
            v-model="form.fields[field.key]"
            :label="field.label"
            :options="fieldOptions(field)"
            :multiple="field.type === 'multiselect'"
            :use-chips="field.type === 'multiselect'"
            :required="!!field.required || (!!field.requiredWhenActive && form.active)"
            :error="!!fieldError(field)"
            :error-message="fieldError(field) || undefined"
          />
          <AppSelect
            v-else-if="field.type === 'tags'"
            v-model="form.fields[field.key]"
            :label="field.label"
            multiple
            use-chips
            use-input
            hide-dropdown-icon
            new-value-mode="add-unique"
            input-debounce="0"
            :placeholder="field.placeholder || ''"
            @new-value="(val, done) => done(String(val).trim().toUpperCase(), 'add-unique')"
          />
          <AppTextField
            v-else
            v-model="form.fields[field.key]"
            :label="field.label"
            :required="isRequired(field)"
            :rules="fieldRules(field)"
            :type="fieldInputType(field)"
            :placeholder="writeOnlyPlaceholder(field)"
            autocomplete="new-password"
          >
            <template v-if="field.secret" #append>
              <q-icon
                :name="reveal[field.key] ? 'o_visibility_off' : 'o_visibility'"
                class="cursor-pointer"
                @click="reveal[field.key] = !reveal[field.key]"
              />
            </template>
          </AppTextField>

          <!-- One line per environment: these are URLs to read and copy, and a single run-on line of them
               is neither. -->
          <div v-if="field.writeOnly && editingId" class="text-caption text-grey-7 q-mt-xs">
            {{ masked[field.key]
              ? `Stored: ${masked[field.key]}. Leave blank to keep it.`
              : (field.required ? 'Not stored, or unreadable on this server. Enter it to save.' : 'Not set.') }}
          </div>
          <div v-if="field.hints?.length" class="text-caption text-grey-6 q-mt-xs">
            <div v-for="hint in field.hints" :key="hint">{{ hint }}</div>
          </div>
        </div>
        </div>

        <div class="text-caption text-grey-6 q-mt-sm">
          Setting a credential Active deactivates any other {{ item.label }} credential — the runtime uses the single active row.
        </div>
      </div>
    </AppFormDrawer>

    <q-dialog v-model="testDialog">
      <q-card style="min-width: 360px; max-width: 560px; width: 100%">
        <q-card-section class="text-h6">{{ item.label }} test — {{ testResult?.name }}</q-card-section>
        <!-- Payment gateway test (e.g. Authorize.Net authenticateTestRequest): connected, or the gateway's error. -->
        <q-card-section v-if="testResult && 'connected' in testResult" class="q-pt-none">
          <div v-if="testResult.connected" class="text-positive text-weight-medium">
            Connected<span v-if="testResult.environment"> to {{ testResult.environment }}</span>
          </div>
          <div v-else class="text-negative">
            {{ testResult.code ? `${testResult.code}: ` : '' }}{{ testResult.message || 'Authentication failed.' }}
          </div>
          <div class="text-caption text-grey-7 q-mt-xs">{{ testResult.elapsedMs }} ms</div>
        </q-card-section>
        <q-card-section v-else-if="testResult" class="q-pt-none">
          <div v-if="!testResult.tokenOk" class="text-negative">
            Authentication failed: {{ testResult.tokenError }}
          </div>
          <template v-else>
            <div v-if="testResult.tokenValidUntil" class="text-caption text-positive q-mb-sm">
              Token OK — valid until {{ $datetime(testResult.tokenValidUntil) }}
            </div>
            <div v-for="group in testGroups" :key="group.title" class="q-mb-md">
              <div class="text-subtitle2 q-mb-xs">{{ group.title }}</div>
              <div v-if="!group.lines.length" class="text-grey-7">No services allowed.</div>
              <div v-for="(line, i) in group.lines" :key="i" class="row no-wrap q-py-xs">
                <div class="col">{{ line.service }}</div>
                <div v-if="line.price != null" class="text-weight-medium">${{ Number(line.price).toFixed(2) }}</div>
                <div v-else class="text-negative text-caption" style="max-width: 60%">{{ line.error }}</div>
              </div>
            </div>
          </template>
        </q-card-section>
        <q-card-actions align="right">
          <q-btn flat no-caps label="Close" color="primary" v-close-popup />
        </q-card-actions>
      </q-card>
    </q-dialog>
  </div>
</template>

<script setup>
/*
 * CredentialPanel: the right-hand pane for a credential-kind integration. Lists that integration's rows
 * (secrets never shown in the grid) and edits them via a form drawer whose fields come from the item
 * metadata. Reloads whenever the selected integration changes.
 */
import { ref, reactive, computed, watch } from 'vue'
import { integrationCredentialApi } from 'modules/integration-credentials/api'
import { customerGroupApi } from 'modules/customer-groups/api'
import { getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'
import { usePermissions, Permissions } from 'composables/usePermissions'
import { deleteConfirmation } from 'dialogs/delete_confirmation'
import AppDataTable from 'components/common/AppDataTable.vue'
import AppFormDrawer from 'components/common/AppFormDrawer.vue'
import AppTextField from 'components/common/AppTextField.vue'
import AppFieldLabel from 'components/common/AppFieldLabel.vue'
import AppSelect from 'components/common/AppSelect.vue'
import PanelHeader from 'modules/integration-credentials/components/PanelHeader.vue'

const props = defineProps({
  item: { type: Object, required: true }
})

const notify = useNotify()
const { has } = usePermissions()
const canWrite = computed(() => has(Permissions.CredentialsWrite))

// Only the integrations that actually have an endpoint to choose declare a baseUrl field — the rest would
// show a column they can never fill.
const hasBaseUrl = computed(() => (props.item.fields || []).some((f) => f.key === 'baseUrl'))

// Payment gateways carry a transaction-fee field — surface it as a grid column so the configured fee is
// visible at a glance across the integration's rows.
const hasFee = computed(() => (props.item.fields || []).some((f) => f.key === 'transactionFeePercent'))

// With a sandbox and a live row side by side, the URL is what tells them apart: Environment says which one
// it claims to be, this says where it actually goes.
const columns = computed(() => [
  { name: 'name', label: 'Name', field: 'name', align: 'left', sortable: true },
  ...(hasBaseUrl.value
    ? [{ name: 'baseUrl', label: 'URL', field: 'baseUrl', align: 'left', sortable: true }]
    : []),
  { name: 'active', label: 'Active', field: 'active', align: 'center' },
  { name: 'isProduction', label: 'Environment', field: 'isProduction', align: 'center' },
  ...(hasFee.value
    ? [{ name: 'transactionFeePercent', label: 'Fee %', field: 'transactionFeePercent', align: 'right' }]
    : []),
  { name: 'updatedOnUtc', label: 'Updated', field: 'updatedOnUtc', align: 'left', sortable: true }
])

const rows = ref([])
const loading = ref(false)
const togglingId = ref(null)
const pagination = ref({ page: 1, rowsPerPage: 10, sortBy: 'name', descending: false })

const drawer = ref(false)
const saving = ref(false)
const editingId = ref(null)
const form = reactive({ name: '', active: false, isProduction: false, fields: {} })
const reveal = reactive({})

const requiredRule = (val) => (!!val && String(val).trim().length > 0) || 'Required'

// Write-only secrets (`writeOnly: true`, e.g. the Authorize.Net Transaction Key): the API never returns the
// value, only `<key>Masked` ("••••1234"). On edit the field starts empty, is optional, and blank keeps it.
const masked = reactive({})
function isRequired (field) {
  // On edit a write-only secret is optional only when one is actually stored (blank keeps it). With none
  // stored, or one this server cannot decrypt (reported as not set), it must be entered again.
  if (field.writeOnly && editingId.value && masked[field.key]) return false
  return !!field.required
}
function writeOnlyPlaceholder (field) {
  if (field.writeOnly && editingId.value && masked[field.key]) return 'Leave blank to keep the stored value'
  return field.placeholder || ''
}

// Options loaded from the API (`optionsSource`), e.g. the customer-group picker.
const dynamicOptions = reactive({})
function fieldOptions (field) {
  return field.optionsSource ? (dynamicOptions[field.optionsSource] || []) : field.options
}
async function loadOptionSources () {
  const sources = new Set((props.item.fields || []).map((f) => f.optionsSource).filter(Boolean))
  if (sources.has('customerGroups') && !dynamicOptions.customerGroups) {
    try {
      const groups = await customerGroupApi.list({ activeOnly: true })
      dynamicOptions.customerGroups = (Array.isArray(groups) ? groups : groups?.items || [])
        .map((g) => ({ label: g.name, value: g.id }))
    } catch {
      dynamicOptions.customerGroups = []
    }
  }
}

// A percent/number field (e.g. a gateway transaction fee) renders as a numeric input and is sent as a
// real number, not a string — so it binds to the decimal? on the command.
function isNumericField (field) {
  return field.type === 'percent' || field.type === 'number'
}
function fieldInputType (field) {
  if (field.secret && !reveal[field.key]) return 'password'
  return isNumericField(field) ? 'number' : 'text'
}
function fieldRules (field) {
  const rules = isRequired(field) ? [requiredRule] : []
  if (field.type === 'percent') {
    rules.push((v) => v == null || v === '' || (Number(v) >= 0 && Number(v) <= 100) || 'Enter a value between 0 and 100')
  }
  if (field.type === 'number' && field.min != null) {
    rules.push((v) => v == null || v === '' || Number(v) >= field.min || `Must be ${field.min} or more`)
  }
  return rules
}

// List fields (select/multiselect/tags/toggle) are not q-input based, so their one rule — "pick at least
// one while active" — is checked here and blocks the save (the API refuses it too: US-T2).
const triedSave = ref(false)
function fieldError (field) {
  if (!triedSave.value) return null
  if (field.type === 'multiselect' && field.requiredWhenActive && form.active) {
    const v = form.fields[field.key]
    if (!Array.isArray(v) || v.length === 0) return 'Choose at least one while active'
  }
  return null
}
const listFieldsInvalid = computed(() =>
  (props.item.fields || []).some((f) =>
    f.type === 'multiselect' && f.requiredWhenActive && form.active &&
    !(Array.isArray(form.fields[f.key]) && form.fields[f.key].length)))

function isListField (field) {
  return field.type === 'multiselect' || field.type === 'tags'
}

// Grid width of a field: full row by default; 'half' / 'third' share a row from the sm breakpoint up.
function fieldCol (field) {
  if (field.col === 'half') return 'col-12 col-sm-6'
  if (field.col === 'third') return 'col-12 col-sm-4'
  return 'col-12'
}

// ---- Test (US-40) ----
const testDialog = ref(false)
const testingId = ref(null)
const testResult = ref(null)
// The API names each sample it rated (carrier-neutral); a sample with no label was not run.
const testGroups = computed(() => {
  const r = testResult.value
  if (!r) return []
  return [
    { title: r.domesticLabel || 'Domestic', lines: r.domestic || [], show: true },
    { title: r.internationalLabel, lines: r.international || [], show: !!r.internationalLabel }
  ].filter((g) => g.show)
})

async function onTest (row) {
  testingId.value = row.id
  try {
    const result = await integrationCredentialApi.test(props.item.key, row.id)
    testResult.value = { ...result, name: row.name }
    testDialog.value = true
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    testingId.value = null
  }
}

function onRequest (p) {
  if (p && p.pagination) pagination.value = p.pagination
}

async function load () {
  loading.value = true
  try {
    const result = await integrationCredentialApi.list(props.item.key)
    rows.value = Array.isArray(result) ? result : result?.items || []
  } catch (err) {
    rows.value = []
    notify.error(getApiErrorMessage(err))
  } finally {
    loading.value = false
  }
}

function openForm (dto) {
  editingId.value = dto ? dto.id : null
  const fields = {}
  // On a new credential, seed any field with a `prefill` (e.g. Stripe returnUrl → storefront base URL).
  for (const f of props.item.fields) {
    fields[f.key] = dto && dto[f.key] != null
      ? (Array.isArray(dto[f.key]) ? [...dto[f.key]] : dto[f.key])
      : (!dto && f.prefill ? prefillValue(f.prefill) : emptyValue(f))
  }
  triedSave.value = false
  form.name = dto?.name || ''
  form.active = dto ? !!dto.active : false
  form.isProduction = dto ? !!dto.isProduction : false
  form.fields = fields
  Object.keys(reveal).forEach((k) => delete reveal[k])
  Object.keys(masked).forEach((k) => delete masked[k])
  for (const f of props.item.fields) {
    if (f.writeOnly) {
      masked[f.key] = dto ? dto[`${f.key}Masked`] || null : null
      fields[f.key] = ''
    }
  }
  loadOptionSources()
  drawer.value = true
}

function prefillValue (prefill) {
  if (Array.isArray(prefill)) return [...prefill]
  return typeof prefill === 'string' ? prefill.replace('{origin}', window.location.origin) : prefill
}
function emptyValue (field) {
  if (isListField(field)) return []
  if (field.type === 'toggle') return false
  if (field.type === 'select') return null
  return ''
}

function onAdd () { openForm(null) }

async function onEdit (row) {
  try {
    const dto = await integrationCredentialApi.get(props.item.key, row.id)
    openForm(dto)
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  }
}

function buildPayload () {
  const payload = { name: form.name.trim(), active: form.active, isProduction: form.isProduction }
  for (const f of props.item.fields) {
    const v = form.fields[f.key]
    if (isListField(f)) {
      payload[f.key] = Array.isArray(v) ? v : []
    } else if (f.type === 'toggle') {
      payload[f.key] = !!v
    } else if (isNumericField(f)) {
      payload[f.key] = v === '' || v == null ? null : Number(v)
    } else {
      payload[f.key] = v == null || String(v).trim() === '' ? null : String(v).trim()
    }
  }
  return payload
}

async function onSave () {
  triedSave.value = true
  if (listFieldsInvalid.value) return
  saving.value = true
  try {
    const payload = buildPayload()
    if (editingId.value) {
      await integrationCredentialApi.update(props.item.key, editingId.value, payload)
    } else {
      await integrationCredentialApi.create(props.item.key, payload)
    }
    notify.success(`${props.item.label} credential saved`)
    drawer.value = false
    await load()
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    saving.value = false
  }
}

async function onToggleActive (row) {
  const next = !row.active
  togglingId.value = row.id
  try {
    await integrationCredentialApi.setActive(props.item.key, row.id, next)
    notify.success(next ? `${props.item.label} credential activated` : `${props.item.label} credential deactivated`)
    await load()
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    togglingId.value = null
  }
}

async function onDelete (row) {
  if (!(await deleteConfirmation(`the "${row.name}" ${props.item.label} credential`))) return
  try {
    await integrationCredentialApi.remove(props.item.key, row.id)
    notify.success('Credential deleted')
    load()
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  }
}

watch(() => props.item.key, () => {
  drawer.value = false
  pagination.value = { ...pagination.value, page: 1 }
  load()
}, { immediate: true })
</script>
