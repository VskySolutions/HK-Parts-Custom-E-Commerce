<template>
  <div>
    <!-- Controls: per-product reviews toggle + status filter + search -->
    <div class="row items-center q-mb-md q-gutter-sm">
      <q-toggle
        :model-value="enabled"
        :disable="!canWrite || togglingEnabled"
        color="primary"
        label="Reviews enabled"
        @update:model-value="onToggleEnabled"
      />
      <q-spinner v-if="togglingEnabled" size="16px" color="primary" />
      <div class="text-caption text-grey-7" style="max-width: 340px">
        When off, the storefront hides this product's reviews section and new submissions are refused.
      </div>
      <q-space />
      <q-select
        v-model="statusFilter"
        :options="statusOptions"
        dense outlined emit-value map-options
        label="Status"
        style="min-width: 150px"
        @update:model-value="reload"
      />
      <q-input
        v-model="search"
        dense outlined debounce="400"
        placeholder="Search reviews"
        style="min-width: 200px"
        @update:model-value="reload"
      >
        <template #prepend><q-icon name="o_search" /></template>
        <template v-if="search" #append>
          <q-icon name="o_close" class="cursor-pointer" @click="search = ''; reload()" />
        </template>
      </q-input>
    </div>

    <AppDataTable
      page-key="product-reviews-panel"
      row-key="id"
      title="Reviews"
      :rows="rows"
      :columns="columns"
      :loading="loading"
      :pagination="pagination"
      show-actions
      @request="onRequest"
      @refresh="reload"
    >
      <template #body-cell-rating="cell">
        <q-td :props="cell">
          <div class="row items-center no-wrap text-amber-8">
            <q-icon
              v-for="n in 5"
              :key="n"
              :name="n <= (cell.row.rating || 0) ? 'o_star' : 'o_star_border'"
              size="16px"
            />
          </div>
        </q-td>
      </template>

      <template #body-cell-body="cell">
        <q-td :props="cell">
          <div v-if="cell.row.title" class="text-weight-medium">{{ cell.row.title }}</div>
          <div class="text-grey-7">{{ excerpt(cell.row.body) }}</div>
        </q-td>
      </template>

      <template #body-cell-createdOnUtc="cell">
        <q-td :props="cell">{{ $datetime(cell.row.createdOnUtc) }}</q-td>
      </template>

      <template #body-cell-status="cell">
        <q-td :props="cell">
          <q-badge :color="statusColor(cell.row.status)" :label="cell.row.status" />
        </q-td>
      </template>

      <template #actions="{ row }">
        <q-btn
          v-if="canWrite"
          flat round dense icon="o_check" color="positive"
          :disable="row.status === 'Approved' || moderatingId === row.id"
          :loading="moderatingId === row.id"
          @click="onModerate(row, 'Approved')"
        >
          <q-tooltip>Approve</q-tooltip>
        </q-btn>
        <q-btn
          v-if="canWrite"
          flat round dense icon="o_block" color="negative"
          :disable="row.status === 'Rejected' || moderatingId === row.id"
          @click="onModerate(row, 'Rejected')"
        >
          <q-tooltip>Reject</q-tooltip>
        </q-btn>
        <q-btn flat round dense icon="o_visibility" color="primary" @click="onView(row)">
          <q-tooltip>View full review</q-tooltip>
        </q-btn>
      </template>
    </AppDataTable>

    <!-- Full review viewer -->
    <AppViewDrawer v-model="viewOpen" :title="viewRow ? (viewRow.reviewerName || 'Review') : 'Review'">
      <div v-if="viewRow" class="column q-gutter-md">
        <div class="row q-col-gutter-md">
          <div class="col">
            <div class="text-caption text-grey-7">Reviewer</div>
            <div>{{ viewRow.reviewerName || '—' }}</div>
          </div>
          <div class="col">
            <div class="text-caption text-grey-7">Date</div>
            <div>{{ $datetime(viewRow.createdOnUtc) }}</div>
          </div>
        </div>
        <div>
          <div class="text-caption text-grey-7">Rating</div>
          <div class="row items-center no-wrap text-amber-8">
            <q-icon
              v-for="n in 5"
              :key="n"
              :name="n <= (viewRow.rating || 0) ? 'o_star' : 'o_star_border'"
              size="20px"
            />
          </div>
        </div>
        <div>
          <div class="text-caption text-grey-7">Status</div>
          <q-badge :color="statusColor(viewRow.status)" :label="viewRow.status" />
        </div>
        <q-separator />
        <div>
          <div class="text-subtitle1 text-weight-medium">{{ viewRow.title || '(no title)' }}</div>
          <div class="q-mt-sm" style="white-space: pre-wrap">{{ viewRow.body || '—' }}</div>
        </div>
      </div>

      <template #footer="{ close }">
        <q-btn
          v-if="canWrite && viewRow && viewRow.status !== 'Rejected'"
          flat color="negative" no-caps icon="o_block" label="Reject"
          @click="onModerate(viewRow, 'Rejected'); close()"
        />
        <q-btn
          v-if="canWrite && viewRow && viewRow.status !== 'Approved'"
          unelevated color="positive" no-caps icon="o_check" label="Approve"
          @click="onModerate(viewRow, 'Approved'); close()"
        />
        <q-btn unelevated color="grey-8" label="Close" @click="close" />
      </template>
    </AppViewDrawer>
  </div>
</template>

<script setup>
/*
 * ProductReviewsPanel: the "Reviews" tab on the product detail page. A product-scoped
 * slice of the WO-14 moderation queue — reviews filtered to this product, with a
 * per-product "Reviews enabled" toggle, status/search filters, and per-row
 * Approve / Reject / View. Mirrors modules/reviews/pages/index.vue but embedded and
 * without the redundant Product column.
 */
import { ref, watch, onMounted } from 'vue'
import { reviewApi } from 'modules/reviews/api'
import { getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'

const props = defineProps({
  productId: { type: String, required: true },
  reviewsEnabled: { type: Boolean, default: false },
  canWrite: { type: Boolean, default: false }
})
const emit = defineEmits(['count', 'update:reviews-enabled'])

const notify = useNotify()

const columns = [
  { name: 'rating', label: 'Rating', field: 'rating', align: 'left', sortable: true },
  { name: 'reviewerName', label: 'Reviewer', field: 'reviewerName', align: 'left', sortable: true },
  { name: 'body', label: 'Review', field: 'body', align: 'left' },
  { name: 'createdOnUtc', label: 'Date', field: 'createdOnUtc', align: 'left', sortable: true },
  { name: 'status', label: 'Status', field: 'status', align: 'center', sortable: true }
]

const statusOptions = [
  { label: 'All statuses', value: null },
  { label: 'Pending', value: 'Pending' },
  { label: 'Approved', value: 'Approved' },
  { label: 'Rejected', value: 'Rejected' }
]

const rows = ref([])
const loading = ref(false)
const search = ref('')
const statusFilter = ref(null)
const pagination = ref({ page: 1, rowsPerPage: 10, rowsNumber: 0, sortBy: 'createdOnUtc', descending: true })
const moderatingId = ref(null)

const viewOpen = ref(false)
const viewRow = ref(null)

// Per-product reviews toggle mirrors the prop; kept local so the switch reflects instantly.
const enabled = ref(props.reviewsEnabled)
const togglingEnabled = ref(false)
watch(() => props.reviewsEnabled, (v) => { enabled.value = v })

function statusColor (status) {
  if (status === 'Approved') return 'positive'
  if (status === 'Rejected') return 'negative'
  return 'orange'
}

function excerpt (text, len = 90) {
  if (!text) return '—'
  const clean = String(text).replace(/\s+/g, ' ').trim()
  return clean.length > len ? `${clean.slice(0, len)}…` : clean
}

async function fetch (reqProps) {
  const p = reqProps?.pagination || pagination.value
  loading.value = true
  try {
    const result = await reviewApi.list({
      page: p.page,
      pageSize: p.rowsPerPage,
      productId: props.productId,
      search: search.value || undefined,
      status: statusFilter.value || undefined,
      sortBy: p.sortBy || undefined,
      sortDescending: !!p.descending
    })
    const items = Array.isArray(result) ? result : result?.items || result?.data || []
    const total = Array.isArray(result) ? result.length : result?.totalCount ?? result?.total ?? items.length
    rows.value = items
    pagination.value = { ...p, rowsNumber: total }
    // Feed the tab badge with the product's total (only when unfiltered so it stays a true count).
    if (!search.value && statusFilter.value === null) emit('count', total)
  } catch (err) {
    rows.value = []
    pagination.value = { ...p, rowsNumber: 0 }
    notify.error(getApiErrorMessage(err))
  } finally {
    loading.value = false
  }
}

function onRequest (reqProps) { fetch(reqProps) }
function reload () { return fetch({ pagination: { ...pagination.value, page: 1 } }) }

function onView (row) {
  viewRow.value = row
  viewOpen.value = true
}

async function onModerate (row, status) {
  if (!row) return
  moderatingId.value = row.id
  try {
    await reviewApi.moderate(row.id, status)
    notify.success(`Review ${status.toLowerCase()}`)
    await reload()
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    moderatingId.value = null
  }
}

async function onToggleEnabled (val) {
  togglingEnabled.value = true
  try {
    await reviewApi.setReviewsEnabled(props.productId, val)
    enabled.value = val
    emit('update:reviews-enabled', val)
    notify.success(val ? 'Reviews enabled for this product' : 'Reviews disabled for this product')
  } catch (err) {
    enabled.value = props.reviewsEnabled // revert the switch on failure
    notify.error(getApiErrorMessage(err))
  } finally {
    togglingEnabled.value = false
  }
}

// Reload when the panel is pointed at a different product (e.g. route change on the same instance).
watch(() => props.productId, () => { statusFilter.value = null; search.value = ''; reload() })

onMounted(() => fetch())
</script>
