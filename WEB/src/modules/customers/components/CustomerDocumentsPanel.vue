<template>
  <div class="q-gutter-y-sm">
    <q-table
      flat bordered dense
      class="app-table-card"
      row-key="id"
      :rows="rows"
      :columns="columns"
      :loading="loading"
      :pagination="{ rowsPerPage: 0 }"
      hide-pagination
    >
      <template #top>
        <div class="app-section__title col ellipsis">Documents</div>
        <q-btn v-if="canWrite" color="primary" unelevated no-caps icon="o_upload_file" label="Upload Documents" @click="openUpload" />
      </template>

      <!-- Full-row slot so deleted documents can be rendered muted and read-only. -->
      <template #body="p">
        <q-tr :props="p" :class="{ 'customer-doc--deleted': isDeleted(p.row) }">
          <q-td key="documentType" :props="p">{{ p.row.documentTypeLabel }}</q-td>
          <q-td key="fileName" :props="p">
            <a class="text-primary cursor-pointer" :class="{ 'text-grey-7': isDeleted(p.row) }" @click="view(p.row)">
              <q-icon :name="fileIcon(p.row)" size="16px" class="q-mr-xs" />{{ p.row.originalFileName }}
            </a>
          </q-td>
          <q-td key="uploadedOnUtc" :props="p">{{ formatDateTime(p.row.uploadedOnUtc) }}</q-td>
          <q-td key="uploadedBy" :props="p">{{ p.row.uploadedBy || '—' }}</q-td>
          <q-td key="deletedBy" :props="p">
            <template v-if="isDeleted(p.row)">
              {{ p.row.deletedBy || '—' }}
              <div class="text-caption text-grey-6">{{ formatDateTime(p.row.deletedOnUtc) }}</div>
            </template>
            <span v-else class="text-grey-6">—</span>
          </q-td>
          <q-td key="status" :props="p">
            <q-badge :color="isDeleted(p.row) ? 'grey' : 'positive'" :label="p.row.status" />
          </q-td>
          <q-td key="actions" :props="p" class="text-right">
            <span v-if="isDeleted(p.row)" class="text-grey-6">—</span>
            <template v-else>
              <q-btn flat round dense icon="o_visibility" :loading="viewingId === p.row.id" @click="view(p.row)">
                <q-tooltip>{{ p.row.isInline ? 'View' : 'Download' }}</q-tooltip>
              </q-btn>
              <q-btn v-if="canWrite" flat round dense icon="o_edit" @click="openEdit(p.row)"><q-tooltip>Edit</q-tooltip></q-btn>
              <q-btn v-if="canWrite" flat round dense icon="o_delete" color="negative" :disable="!!deletingId" :loading="deletingId === p.row.id" @click="remove(p.row)">
                <q-tooltip>Delete</q-tooltip>
              </q-btn>
            </template>
          </q-td>
        </q-tr>
      </template>

      <template #no-data>
        <div class="full-width column items-center q-pa-lg text-grey-6">
          <q-icon name="o_folder_open" size="40px" class="q-mb-sm" />
          <div>No documents uploaded yet.</div>
        </div>
      </template>
    </q-table>

    <!-- ============ Upload (multi-row) ============ -->
    <q-dialog v-model="uploadOpen" persistent @hide="resetUpload">
      <q-card style="width: 760px; max-width: 95vw">
        <q-card-section class="text-subtitle1 text-weight-medium">Upload documents</q-card-section>
        <q-separator />
        <q-card-section class="q-gutter-y-md">
          <q-banner dense class="bg-grey-2 rounded-borders text-caption">
            <template #avatar><q-icon name="o_info" color="primary" /></template>
            {{ rules.note }}
          </q-banner>

          <div v-for="(row, i) in uploadRows" :key="row.key" class="customer-doc__row q-pa-sm rounded-borders">
            <div class="row items-center q-mb-xs">
              <div class="text-caption text-weight-medium text-grey-8 col">Document {{ i + 1 }}</div>
              <q-btn v-if="uploadRows.length > 1" flat round dense size="sm" icon="o_close" :disable="uploading" @click="removeRow(i)">
                <q-tooltip>Remove</q-tooltip>
              </q-btn>
            </div>
            <div class="row q-col-gutter-sm">
              <div class="col-12 col-sm-6">
                <AppSelect
                  v-model="row.documentType" label="Document Name" required :options="typeOptions"
                  placeholder="Select document" :disable="uploading"
                  :error="!!row.errors.documentType" :error-message="row.errors.documentType"
                  @update:model-value="row.errors.documentType = ''"
                />
              </div>
              <div class="col-12 col-sm-6">
                <AppFieldLabel label="File" required />
                <q-file
                  v-model="row.file" dense outlined clearable :accept="rules.accept" :disable="uploading"
                  hide-bottom-space :error="!!row.errors.file" :error-message="row.errors.file"
                  @update:model-value="row.errors.file = ''"
                >
                  <template #prepend><q-icon name="o_attach_file" /></template>
                </q-file>
              </div>
              <div class="col-12">
                <AppTextField
                  v-model="row.note" label="Note" type="textarea" autogrow :maxlength="rules.noteMax" :disable="uploading"
                  :error="!!row.errors.note" :error-message="row.errors.note"
                />
              </div>
            </div>
          </div>

          <q-btn
            v-if="uploadRows.length < rules.maxRows" flat dense no-caps color="primary" icon="o_add" label="Add new"
            :disable="uploading" @click="addRow"
          />
          <div v-if="uploadError" class="text-negative text-caption">{{ uploadError }}</div>
        </q-card-section>
        <q-separator />
        <q-card-actions align="right">
          <q-btn flat no-caps label="Cancel" :disable="uploading" v-close-popup />
          <q-btn color="primary" unelevated no-caps icon="o_cloud_upload" label="Upload All" :loading="uploading" @click="submitUpload" />
        </q-card-actions>
      </q-card>
    </q-dialog>

    <!-- ============ Edit ============ -->
    <q-dialog v-model="editOpen" persistent>
      <q-card style="width: 520px; max-width: 95vw">
        <q-card-section class="text-subtitle1 text-weight-medium">Edit document</q-card-section>
        <q-separator />
        <q-card-section class="q-gutter-y-sm">
          <AppSelect
            v-model="editRow.documentType" label="Document Name" required :options="typeOptions" :disable="saving"
            :error="!!editRow.errors.documentType" :error-message="editRow.errors.documentType"
            @update:model-value="editRow.errors.documentType = ''"
          />
          <div>
            <AppFieldLabel label="Replace file (optional)" />
            <q-file
              v-model="editRow.file" dense outlined clearable :accept="rules.accept" :disable="saving"
              :hint="`Current: ${editRow.currentFileName}`"
              :error="!!editRow.errors.file" :error-message="editRow.errors.file"
              @update:model-value="editRow.errors.file = ''"
            >
              <template #prepend><q-icon name="o_attach_file" /></template>
            </q-file>
            <div class="text-caption text-grey-6 q-mt-xs">{{ rules.note }}</div>
          </div>
          <AppTextField
            v-model="editRow.note" label="Note" type="textarea" autogrow :maxlength="rules.noteMax" :disable="saving"
            :error="!!editRow.errors.note" :error-message="editRow.errors.note"
          />
        </q-card-section>
        <q-separator />
        <q-card-actions align="right">
          <q-btn flat no-caps label="Cancel" :disable="saving" v-close-popup />
          <q-btn color="primary" unelevated no-caps icon="o_save" label="Save" :loading="saving" @click="submitEdit" />
        </q-card-actions>
      </q-card>
    </q-dialog>
  </div>
</template>

<script setup>
/*
 * Customer → Documents tab. Lists every document (active + soft-deleted, newest first), bulk upload in a
 * multi-row dialog, edit (type / note / optional replacement file), soft delete, and view/download.
 * Files come from private storage as an authenticated blob — no storage path or public URL is ever used.
 * Emits `count` with the active-document count so the parent updates the tab badge without a reload.
 */
import { ref, reactive, computed, onMounted } from 'vue'
import { getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'
import { deleteConfirmation } from 'dialogs/delete_confirmation'
import { formatDateTime } from 'src/utils/datetime'
import { customerDocumentsApi, customerDocumentRules as rules } from 'modules/customers/api'
import AppSelect from 'components/common/AppSelect.vue'
import AppTextField from 'components/common/AppTextField.vue'
import AppFieldLabel from 'components/common/AppFieldLabel.vue'

const props = defineProps({
  customerId: { type: String, required: true },
  canWrite: { type: Boolean, default: false }
})
const emit = defineEmits(['count'])
const notify = useNotify()

const columns = [
  { name: 'documentType', label: 'Document Name', field: 'documentTypeLabel', align: 'left' },
  { name: 'fileName', label: 'File Name', field: 'originalFileName', align: 'left' },
  { name: 'uploadedOnUtc', label: 'Uploaded', field: 'uploadedOnUtc', align: 'left' },
  { name: 'uploadedBy', label: 'Uploaded By', field: 'uploadedBy', align: 'left' },
  { name: 'deletedBy', label: 'Deleted By', field: 'deletedBy', align: 'left' },
  { name: 'status', label: 'Status', field: 'status', align: 'center' },
  { name: 'actions', label: 'Actions', field: 'id', align: 'right' }
]

const rows = ref([])
const loading = ref(false)
const types = ref([])
const typeOptions = computed(() => types.value.map((t) => ({ label: t.label, value: t.value })))

const isDeleted = (row) => row.status === 'Deleted'
const fileIcon = (row) => row.mimeType === 'application/pdf'
  ? 'o_picture_as_pdf'
  : (row.mimeType || '').startsWith('image/') ? 'o_image' : 'o_description'

function applyList (res) {
  rows.value = Array.isArray(res?.items) ? res.items : []
  emit('count', res?.activeCount ?? rows.value.filter((r) => !isDeleted(r)).length)
}

async function load () {
  loading.value = true
  try { applyList(await customerDocumentsApi.list(props.customerId)) } catch (e) { notify.error(getApiErrorMessage(e)) } finally { loading.value = false }
}

async function loadTypes () {
  try { types.value = await customerDocumentsApi.types() } catch (e) { types.value = [] }
}

// ---------- Client-side file rules (server re-checks the real content type) ----------
function fileError (file, required) {
  if (!file) return required ? 'File is required.' : ''
  const name = (file.name || '').toLowerCase()
  if (!rules.extensions.some((ext) => name.endsWith(ext))) return 'Only PDF, DOC, DOCX, JPG, PNG files are allowed.'
  if (file.size > rules.maxBytes) return 'File exceeds the maximum size of 10 MB.'
  if (file.size === 0) return 'File is empty.'
  return ''
}

// Maps an API 400 (`errors: { "Items[1].File": [...] }` or `{ "File": [...] }`) onto row-level errors.
function serverErrors (e) {
  const errors = e?.response?.status === 400 ? e.response.data?.errors : null
  if (!errors || typeof errors !== 'object') return null
  const out = { rows: {}, general: [] }
  for (const [key, messages] of Object.entries(errors)) {
    const msg = Array.isArray(messages) ? messages[0] : String(messages)
    const m = /^items\[(\d+)\]\.(\w+)$/i.exec(key)
    const field = (m ? m[2] : key).replace(/^\w/, (c) => c.toLowerCase())
    if (m) (out.rows[m[1]] ||= {})[field] = msg
    else if (['documentType', 'file', 'note'].includes(field)) (out.rows.single ||= {})[field] = msg
    else out.general.push(msg)
  }
  return out
}

// ---------- Upload ----------
let rowKey = 0
const newRow = () => ({ key: ++rowKey, documentType: null, file: null, note: '', errors: {} })
const uploadOpen = ref(false)
const uploading = ref(false)
const uploadError = ref('')
const uploadRows = ref([newRow()])

function openUpload () { resetUpload(); uploadOpen.value = true }
function resetUpload () { uploadRows.value = [newRow()]; uploadError.value = '' }
function addRow () { if (uploadRows.value.length < rules.maxRows) uploadRows.value.push(newRow()) }
function removeRow (i) { uploadRows.value.splice(i, 1) }

async function submitUpload () {
  if (uploading.value) return
  uploadError.value = ''
  let ok = true
  for (const row of uploadRows.value) {
    row.errors = {
      documentType: row.documentType ? '' : 'Document Name is required.',
      file: fileError(row.file, true),
      note: (row.note || '').length > rules.noteMax ? `Note must be at most ${rules.noteMax} characters.` : ''
    }
    if (row.errors.documentType || row.errors.file || row.errors.note) ok = false
  }
  if (!ok) return

  uploading.value = true
  try {
    const res = await customerDocumentsApi.upload(props.customerId, uploadRows.value)
    const n = uploadRows.value.length
    applyList(res)
    uploadOpen.value = false
    notify.success(n === 1 ? 'Document uploaded' : `${n} documents uploaded`)
  } catch (e) {
    const mapped = serverErrors(e)
    if (mapped) {
      Object.entries(mapped.rows).forEach(([i, errs]) => { if (uploadRows.value[i]) uploadRows.value[i].errors = { ...uploadRows.value[i].errors, ...errs } })
      uploadError.value = mapped.general.join(' ') || 'Nothing was uploaded. Fix the highlighted rows and try again.'
    } else notify.error(getApiErrorMessage(e))
  } finally { uploading.value = false }
}

// ---------- Edit ----------
const editOpen = ref(false)
const saving = ref(false)
const editRow = reactive({ id: null, documentType: null, note: '', file: null, currentFileName: '', errors: {} })

function openEdit (row) {
  Object.assign(editRow, { id: row.id, documentType: row.documentType, note: row.note || '', file: null, currentFileName: row.originalFileName, errors: {} })
  editOpen.value = true
}

async function submitEdit () {
  if (saving.value) return
  editRow.errors = {
    documentType: editRow.documentType ? '' : 'Document Name is required.',
    file: fileError(editRow.file, false)
  }
  if (editRow.errors.documentType || editRow.errors.file) return
  saving.value = true
  try {
    await customerDocumentsApi.update(props.customerId, editRow.id, editRow)
    editOpen.value = false
    notify.success('Document updated')
    await load()
  } catch (e) {
    const mapped = serverErrors(e)
    if (mapped?.rows.single) editRow.errors = { ...editRow.errors, ...mapped.rows.single }
    else notify.error(getApiErrorMessage(e))
  } finally { saving.value = false }
}

// ---------- Delete (soft) ----------
const deletingId = ref(null)
async function remove (row) {
  if (deletingId.value) return
  const ok = await deleteConfirmation(`"${row.documentTypeLabel}" (${row.originalFileName})`, {
    title: 'Delete document',
    message: `Delete "${row.documentTypeLabel}" (${row.originalFileName})? It will be marked as deleted and kept for audit; it can't be edited or restored afterwards.`
  })
  if (!ok) return
  deletingId.value = row.id
  try {
    await customerDocumentsApi.remove(props.customerId, row.id)
    notify.success('Document deleted')
    await load()
  } catch (e) { notify.error(getApiErrorMessage(e)) } finally { deletingId.value = null }
}

// ---------- View / download (authenticated blob; never a storage URL) ----------
const viewingId = ref(null)
async function view (row) {
  if (viewingId.value) return
  // Open the tab synchronously (inside the click) so popup blockers allow it, then point it at the blob.
  const win = row.isInline ? window.open('', '_blank') : null
  viewingId.value = row.id
  try {
    const blob = await customerDocumentsApi.file(props.customerId, row.id)
    const url = window.URL.createObjectURL(new Blob([blob], { type: row.mimeType }))
    if (win) {
      win.opener = null
      win.location.href = url
    } else {
      const a = document.createElement('a')
      a.href = url
      a.download = row.originalFileName
      document.body.appendChild(a)
      a.click()
      a.remove()
    }
    setTimeout(() => window.URL.revokeObjectURL(url), 60_000)
  } catch (e) {
    if (win) win.close()
    notify.error(getApiErrorMessage(e))
  } finally { viewingId.value = null }
}

onMounted(() => { loadTypes(); load() })
</script>

<style scoped lang="scss">
.customer-doc--deleted {
  opacity: 0.6;
  background: rgba(0, 0, 0, 0.02);
}
.customer-doc__row {
  border: 1px solid rgba(0, 0, 0, 0.12);
}
</style>
