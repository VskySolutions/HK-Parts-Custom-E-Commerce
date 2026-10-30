<template>
  <q-page class="app-page">
    <AppListHeader
      title="Tax Exemptions"
      subtitle="Customer tax-exemption requests awaiting review."
      :breadcrumbs="[{ label: 'Home', icon: 'o_home', to: '/dashboard' }, { label: 'Tax Exemptions' }]"
      :show-add="false"
    >
      <template #actions>
        <q-input
          v-model="search"
          dense
          outlined
          debounce="400"
          placeholder="Search customer, email or certificate"
          style="min-width: 260px"
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

    <AppFilterDrawer v-model="filtersOpen" title="Filter requests" @clear="clearFilters">
      <AppSelect v-model="statusFilter" label="Status" :options="statusOptions" @update:model-value="reload" />
    </AppFilterDrawer>

    <AppDataTable
      page-key="admin-tax-exemption-requests"
      row-key="id"
      title="Tax exemption requests"
      :rows="rows"
      :columns="columns"
      :loading="loading"
      :pagination="pagination"
      show-actions
      no-data-label="No tax-exemption requests match the current filters."
      @request="onRequest"
      @refresh="reload"
    >
      <template #body-cell-customerName="cell">
        <q-td :props="cell">
          <a class="text-primary cursor-pointer text-weight-medium" @click="onReview(cell.row)">{{ cell.row.customerName || '—' }}</a>
          <div v-if="cell.row.customerEmail" class="text-caption text-grey-7">{{ cell.row.customerEmail }}</div>
        </q-td>
      </template>

      <template #body-cell-certificate="cell">
        <q-td :props="cell">{{ cell.row.certificateNumber || cell.row.vatId || '—' }}</q-td>
      </template>

      <template #body-cell-status="cell">
        <q-td :props="cell" class="text-center">
          <q-badge :color="statusColor(cell.row.status)" :label="statusLabel(cell.row.status)" />
        </q-td>
      </template>

      <template #body-cell-submittedOnUtc="cell">
        <q-td :props="cell">{{ $datetime(cell.row.submittedOnUtc) }}</q-td>
      </template>

      <template #body-cell-reviewedOnUtc="cell">
        <q-td :props="cell">{{ $datetime(cell.row.reviewedOnUtc) }}</q-td>
      </template>

      <template #actions="{ row }">
        <q-btn flat round dense icon="o_fact_check" color="primary" @click="onReview(row)">
          <q-tooltip>Review</q-tooltip>
        </q-btn>
      </template>
    </AppDataTable>

    <!-- Review dialog: loads full detail (incl. documents) on open; approve/reject only for PendingReview. -->
    <q-dialog v-model="reviewOpen" @hide="onReviewHide">
      <q-card style="width: 560px; max-width: 95vw">
        <q-card-section class="row items-center q-gutter-sm">
          <q-icon name="o_fact_check" size="22px" color="primary" />
          <div class="text-subtitle1 text-weight-medium col ellipsis">Tax exemption review</div>
          <q-badge v-if="detail" :color="statusColor(detail.status)" :label="statusLabel(detail.status)" />
          <q-btn flat round dense icon="o_close" v-close-popup />
        </q-card-section>
        <q-separator />

        <q-card-section v-if="detailLoading" class="flex flex-center q-py-xl">
          <q-spinner color="primary" size="32px" />
        </q-card-section>

        <q-card-section v-else-if="detail" class="scroll" style="max-height: 72vh">
          <!-- Customer -->
          <div class="text-subtitle1 text-weight-medium">{{ detail.customerName || '—' }}</div>
          <div class="text-caption text-grey-7 q-mb-md">{{ detail.customerEmail || '—' }}</div>

          <!-- Certificate / VAT / dates -->
          <div class="row q-col-gutter-md q-mb-md">
            <div v-if="detail.certificateNumber" class="col-12 col-sm-6">
              <AppFieldLabel label="Certificate number" />
              <div class="text-body2">{{ detail.certificateNumber }}</div>
            </div>
            <div v-if="detail.vatId" class="col-12 col-sm-6">
              <AppFieldLabel label="VAT ID" />
              <div class="text-body2">{{ detail.vatId }}</div>
            </div>
            <div class="col-12 col-sm-6">
              <AppFieldLabel label="Submitted" />
              <div class="text-body2">{{ $datetime(detail.submittedOnUtc) }}</div>
            </div>
            <div v-if="!isPending" class="col-12 col-sm-6">
              <AppFieldLabel label="Reviewed" />
              <div class="text-body2">{{ $datetime(detail.reviewedOnUtc) }}</div>
            </div>
          </div>

          <!-- Documents -->
          <AppFieldLabel label="Documents" />
          <q-list v-if="orderedDocuments.length" bordered separator class="rounded-borders q-mt-xs q-mb-md">
            <q-item
              v-for="doc in orderedDocuments"
              :key="doc.id"
              :clickable="!!doc.url"
              :tag="doc.url ? 'a' : 'div'"
              :href="doc.url ? $media(doc.url) : undefined"
              :target="doc.url ? '_blank' : undefined"
              :rel="doc.url ? 'noopener' : undefined"
            >
              <q-item-section avatar><q-icon name="o_description" color="primary" /></q-item-section>
              <q-item-section class="text-body2">{{ doc.fileName || 'Document' }}</q-item-section>
              <q-item-section v-if="doc.url" side><q-icon name="o_open_in_new" size="16px" color="grey-6" /></q-item-section>
            </q-item>
          </q-list>
          <div v-else class="text-grey-6 text-body2 q-mt-xs q-mb-md">No documents attached.</div>

          <!-- Already reviewed: show the existing note read-only, hide the actions -->
          <template v-if="!isPending">
            <AppFieldLabel label="Administrator note" />
            <div class="text-body2">{{ detail.adminNote || '—' }}</div>
          </template>

          <!-- Pending + reviewer: capture an optional note before approving/rejecting -->
          <AppTextField
            v-else-if="canReview"
            v-model="adminNote"
            label="Administrator note (optional)"
            type="textarea"
            autogrow
            counter
            maxlength="1024"
            placeholder="Add an internal note for this decision…"
          />

          <!-- Pending but no permission -->
          <div v-else class="text-grey-6 text-body2">You don't have permission to approve or reject requests.</div>
        </q-card-section>

        <template v-if="detail && !detailLoading">
          <q-separator />
          <q-card-actions align="right">
            <q-btn flat no-caps label="Close" v-close-popup />
            <template v-if="isPending && canReview">
              <q-btn
                color="negative"
                outline
                no-caps
                icon="o_block"
                label="Reject"
                :loading="submitting === 'reject'"
                :disable="submitting !== null"
                @click="onReject"
              />
              <q-btn
                color="positive"
                unelevated
                no-caps
                icon="o_check"
                label="Approve"
                :loading="submitting === 'approve'"
                :disable="submitting !== null"
                @click="onApprove"
              />
            </template>
          </q-card-actions>
        </template>
      </q-card>
    </q-dialog>
  </q-page>
</template>

<script setup>
/*
 * Tax Exemption review queue (WO-126): AppListHeader + AppDataTable (server pagination) listing
 * customer tax-exemption requests, with an inline review dialog that loads full detail + documents
 * and lets a reviewer approve/reject. Only PendingReview requests are actionable — the backend
 * rejects re-review with a conflict, so we hide the actions once a request has been decided.
 * Backed by AdminTaxExemptionRequestsController (/api/admin/tax-exemption-requests).
 */
import { ref, computed, onMounted } from 'vue'
import { getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'
import { usePermissions } from 'composables/usePermissions'
import { useConfirm } from 'composables/useConfirm'
import { taxExemptionApi, statusOptions, statusColor } from '../api'

const notify = useNotify()
const confirm = useConfirm()
const { has } = usePermissions()
const canReview = computed(() => has('Users.Write'))

const columns = [
  { name: 'customerName', label: 'Customer', field: 'customerName', align: 'left', sortable: true },
  { name: 'certificate', label: 'Certificate / VAT', field: 'certificateNumber', align: 'left' },
  { name: 'status', label: 'Status', field: 'status', align: 'center', sortable: true },
  { name: 'submittedOnUtc', label: 'Submitted', field: 'submittedOnUtc', align: 'left', sortable: true },
  { name: 'reviewedOnUtc', label: 'Reviewed', field: 'reviewedOnUtc', align: 'left', sortable: true }
]

const rows = ref([])
const loading = ref(false)
const search = ref('')
const statusFilter = ref(null)
const filtersOpen = ref(false)
const pagination = ref({ page: 1, rowsPerPage: 20, rowsNumber: 0 })

// Review dialog state
const reviewOpen = ref(false)
const detail = ref(null)
const detailLoading = ref(false)
const adminNote = ref('')
const submitting = ref(null) // null | 'approve' | 'reject'

const activeFilterCount = computed(() => (statusFilter.value !== null ? 1 : 0))
const isPending = computed(() => detail.value?.status === 'PendingReview')
const orderedDocuments = computed(() =>
  [...(detail.value?.documents || [])].sort((a, b) => (a.displayOrder ?? 0) - (b.displayOrder ?? 0))
)

function statusLabel (status) {
  const o = statusOptions.find((x) => x.value === status)
  return o ? o.label : (status || '—')
}

function clearFilters () {
  statusFilter.value = null
  reload()
}

async function fetch (props) {
  const p = props?.pagination || pagination.value
  loading.value = true
  try {
    const r = await taxExemptionApi.list({
      page: p.page,
      pageSize: p.rowsPerPage,
      status: statusFilter.value || undefined,
      search: search.value || undefined,
      sortBy: p.sortBy || undefined,
      sortDescending: !!p.descending
    })
    rows.value = Array.isArray(r?.items) ? r.items : []
    pagination.value = { ...p, rowsNumber: r?.totalCount ?? rows.value.length }
  } catch (e) {
    rows.value = []
    pagination.value = { ...p, rowsNumber: 0 }
    notify.error(getApiErrorMessage(e))
  } finally {
    loading.value = false
  }
}

function onRequest (props) { fetch(props) }
function reload () { fetch({ pagination: { ...pagination.value, page: 1 } }) }

async function onReview (row) {
  reviewOpen.value = true
  detail.value = null
  adminNote.value = ''
  submitting.value = null
  detailLoading.value = true
  try {
    detail.value = await taxExemptionApi.get(row.id)
  } catch (e) {
    notify.error(getApiErrorMessage(e))
    reviewOpen.value = false
  } finally {
    detailLoading.value = false
  }
}

function onReviewHide () {
  detail.value = null
  adminNote.value = ''
  submitting.value = null
}

async function onApprove () {
  if (!detail.value) return
  submitting.value = 'approve'
  try {
    await taxExemptionApi.approve(detail.value.id, adminNote.value.trim() || null)
    notify.success('Tax exemption approved')
    reviewOpen.value = false
    reload()
  } catch (e) {
    notify.error(getApiErrorMessage(e))
  } finally {
    submitting.value = null
  }
}

async function onReject () {
  if (!detail.value) return
  const ok = await confirm({
    title: 'Reject request',
    message: `Reject the tax-exemption request from ${detail.value.customerName || 'this customer'}? Taxes will continue to apply to their orders.`,
    okLabel: 'Reject',
    color: 'negative'
  })
  if (!ok) return
  submitting.value = 'reject'
  try {
    await taxExemptionApi.reject(detail.value.id, adminNote.value.trim() || null)
    notify.success('Tax exemption rejected')
    reviewOpen.value = false
    reload()
  } catch (e) {
    notify.error(getApiErrorMessage(e))
  } finally {
    submitting.value = null
  }
}

onMounted(() => fetch())
</script>
