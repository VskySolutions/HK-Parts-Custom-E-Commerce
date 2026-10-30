<template>
  <q-page class="app-page">
    <AppListHeader
      :breadcrumbs="[
        { label: 'Home', icon: 'o_home', to: '/dashboard' },
        { label: 'Catalog' },
        { label: 'Reviews' }
      ]"
    >
      <template #actions>
        <!-- Pending / approved summary (from /stats) -->
        <q-chip dense square color="orange-1" text-color="orange-9" icon="o_pending_actions">
          Pending: {{ statsLabel(stats.pendingCount) }}
        </q-chip>
        <q-chip dense square color="green-1" text-color="green-9" icon="o_check_circle">
          Approved: {{ statsLabel(stats.approvedCount) }}
        </q-chip>

        <!-- Bulk moderation (visible while rows are selected) -->
        <template v-if="selected.length">
          <q-separator vertical class="q-mx-sm" />
          <q-btn
            unelevated
            color="positive"
            no-caps
            icon="o_check"
            :label="`Approve (${selected.length})`"
            :loading="bulkLoading === 'Approved'"
            :disable="!!bulkLoading"
            @click="onBulk('Approved')"
          />
          <q-btn
            unelevated
            color="negative"
            no-caps
            icon="o_block"
            :label="`Reject (${selected.length})`"
            :loading="bulkLoading === 'Rejected'"
            :disable="!!bulkLoading"
            @click="onBulk('Rejected')"
          />
        </template>

        <q-input
          v-model="search"
          dense
          outlined
          debounce="400"
          placeholder="Search"
          style="min-width: 220px"
          @update:model-value="reload"
        >
          <template #prepend><q-icon name="o_search" /></template>
          <template v-if="search" #append>
            <q-icon name="o_close" class="cursor-pointer" @click="search = ''; reload()" />
          </template>
        </q-input>
        <q-btn outline color="primary" no-caps icon="o_tune" label="Advanced" class="q-ml-sm" @click="filtersOpen = true">
          <q-badge v-if="activeFilterCount" color="red" floating>{{ activeFilterCount }}</q-badge>
        </q-btn>
      </template>
    </AppListHeader>

    <AppFilterDrawer v-model="filtersOpen" title="Filter reviews" @clear="clearFilters">
      <AppSelect v-model="statusFilter" label="Status" :options="statusOptions" @update:model-value="reload" />
      <AppSelect v-model="ratingFilter" label="Rating" :options="ratingOptions" @update:model-value="reload" />
      <AppTextField
        v-model="productIdFilter"
        label="Product ID"
        placeholder="Filter by product id"
        clearable
        @update:model-value="reloadDebounced"
      />
      <AppDateField v-model="dateFrom" label="From date" @update:model-value="reload" />
      <AppDateField v-model="dateTo" label="To date" @update:model-value="reload" />
    </AppFilterDrawer>

    <AppDataTable
      page-key="product-reviews"
      row-key="id"
      title="All reviews"
      :rows="rows"
      :columns="columns"
      :loading="loading"
      :pagination="pagination"
      selectable
      :selected="selected"
      show-actions
      @update:selected="selected = $event"
      @request="onRequest"
      @refresh="reload"
    >
      <template #body-cell-productName="cell">
        <q-td :props="cell">
          <div class="text-weight-medium">{{ cell.row.productName || '—' }}</div>
        </q-td>
      </template>

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
          flat round dense icon="o_check" color="positive"
          :disable="row.status === 'Approved' || moderatingId === row.id"
          :loading="moderatingId === row.id"
          @click="onModerate(row, 'Approved')"
        >
          <q-tooltip>Approve</q-tooltip>
        </q-btn>
        <q-btn
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
    <AppViewDrawer v-model="viewOpen" :title="viewRow ? (viewRow.productName || 'Review') : 'Review'">
      <div v-if="viewRow" class="column q-gutter-md">
        <div>
          <div class="text-caption text-grey-7">Product</div>
          <div class="text-weight-medium">{{ viewRow.productName || '—' }}</div>
        </div>
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
          v-if="viewRow && viewRow.status !== 'Rejected'"
          flat color="negative" no-caps icon="o_block" label="Reject"
          @click="onModerate(viewRow, 'Rejected'); close()"
        />
        <q-btn
          v-if="viewRow && viewRow.status !== 'Approved'"
          unelevated color="positive" no-caps icon="o_check" label="Approve"
          @click="onModerate(viewRow, 'Approved'); close()"
        />
        <q-btn unelevated color="grey-8" label="Close" @click="close" />
      </template>
    </AppViewDrawer>
  </q-page>
</template>

<script setup>
/*
 * Product Reviews moderation queue (WO-14): server-paginated AppDataTable with
 * status/rating/date/product filters, per-row Approve / Reject / View, and
 * multi-select bulk moderation. Pending/approved counts come from /stats.
 */
import { ref, computed, onMounted } from 'vue'
import { reviewApi } from 'modules/reviews/api'
import { getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'

const notify = useNotify()

const columns = [
  { name: 'productName', label: 'Product', field: 'productName', align: 'left', sortable: true },
  { name: 'reviewerName', label: 'Reviewer', field: 'reviewerName', align: 'left', sortable: true },
  { name: 'rating', label: 'Rating', field: 'rating', align: 'left', sortable: true },
  { name: 'body', label: 'Review', field: 'body', align: 'left' },
  { name: 'createdOnUtc', label: 'Date', field: 'createdOnUtc', align: 'left', sortable: true },
  { name: 'status', label: 'Status', field: 'status', align: 'center', sortable: true }
]

const statusOptions = [
  { label: 'All', value: null },
  { label: 'Pending', value: 'Pending' },
  { label: 'Approved', value: 'Approved' },
  { label: 'Rejected', value: 'Rejected' }
]
const ratingOptions = [
  { label: 'All ratings', value: null },
  { label: '5 stars', value: 5 },
  { label: '4 stars', value: 4 },
  { label: '3 stars', value: 3 },
  { label: '2 stars', value: 2 },
  { label: '1 star', value: 1 }
]

const rows = ref([])
const loading = ref(false)
const stats = ref({})
const selected = ref([])
const moderatingId = ref(null)
const bulkLoading = ref(null)

const search = ref('')
const statusFilter = ref(null)
const ratingFilter = ref(null)
const productIdFilter = ref('')
const dateFrom = ref('')
const dateTo = ref('')
const filtersOpen = ref(false)
const pagination = ref({ page: 1, rowsPerPage: 10, rowsNumber: 0, sortBy: 'createdOnUtc', descending: true })

const viewOpen = ref(false)
const viewRow = ref(null)

const activeFilterCount = computed(() =>
  (statusFilter.value !== null ? 1 : 0) +
  (ratingFilter.value !== null ? 1 : 0) +
  (productIdFilter.value ? 1 : 0) +
  (dateFrom.value ? 1 : 0) +
  (dateTo.value ? 1 : 0)
)

function statsLabel (n) {
  return n === undefined || n === null ? '—' : n
}

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

function clearFilters () {
  statusFilter.value = null
  ratingFilter.value = null
  productIdFilter.value = ''
  dateFrom.value = ''
  dateTo.value = ''
  reload()
}

async function fetch (props) {
  const p = props?.pagination || pagination.value
  loading.value = true
  try {
    const result = await reviewApi.list({
      page: p.page,
      pageSize: p.rowsPerPage,
      search: search.value || undefined,
      status: statusFilter.value || undefined,
      rating: ratingFilter.value || undefined,
      productId: productIdFilter.value || undefined,
      dateFrom: dateFrom.value || undefined,
      dateTo: dateTo.value || undefined,
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

async function loadStats () {
  try {
    stats.value = (await reviewApi.stats()) || {}
  } catch (err) {
    // Stats are a non-critical header adornment — never block the queue on them.
    stats.value = {}
  }
}

function onRequest (props) {
  fetch(props)
}

function reload () {
  return fetch({ pagination: { ...pagination.value, page: 1 } })
}

let reloadTimer = null
function reloadDebounced () {
  clearTimeout(reloadTimer)
  reloadTimer = setTimeout(reload, 400)
}

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
    await Promise.all([reload(), loadStats()])
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    moderatingId.value = null
  }
}

async function onBulk (status) {
  const ids = selected.value.map((r) => r.id)
  if (!ids.length) return
  bulkLoading.value = status
  try {
    await reviewApi.bulkModerate(ids, status)
    notify.success(`${ids.length} review${ids.length > 1 ? 's' : ''} ${status.toLowerCase()}`)
    selected.value = []
    await Promise.all([reload(), loadStats()])
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    bulkLoading.value = null
  }
}

onMounted(() => {
  fetch()
  loadStats()
})
</script>
