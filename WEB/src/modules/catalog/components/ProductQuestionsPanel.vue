<template>
  <div>
    <!-- Controls: status filter + search -->
    <div class="row items-center q-mb-md q-gutter-sm">
      <div class="text-caption text-grey-7" style="max-width: 360px">
        Customer questions for this product. Publish an answer, then approve it to show it on the storefront.
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
        placeholder="Search questions"
        style="min-width: 200px"
        @update:model-value="reload"
      >
        <template #prepend><q-icon name="o_search" /></template>
        <template v-if="search" #append>
          <q-icon name="o_close" class="cursor-pointer" @click="search = ''; reload()" />
        </template>
      </q-input>
      <q-btn v-if="canWrite" unelevated color="primary" no-caps icon="o_add" label="Add FAQ" @click="faqFormOpen = true" />
    </div>

    <AppDataTable
      page-key="product-questions-panel"
      row-key="id"
      title="Questions"
      :rows="rows"
      :columns="columns"
      :loading="loading"
      :pagination="pagination"
      show-actions
      @request="onRequest"
      @refresh="reload"
    >
      <template #body-cell-askerName="cell">
        <q-td :props="cell">
          <div>{{ cell.row.askerName || '—' }}</div>
          <div v-if="cell.row.askerEmail" class="text-caption text-grey-7">{{ cell.row.askerEmail }}</div>
        </q-td>
      </template>

      <template #body-cell-questionText="cell">
        <q-td :props="cell">
          <div class="text-grey-8">{{ excerpt(cell.row.questionText) }}</div>
          <div v-if="isAnswered(cell.row)" class="text-caption text-positive ellipsis">
            <q-icon name="o_reply" size="14px" /> {{ excerpt(cell.row.answerText, 80) }}
          </div>
        </q-td>
      </template>

      <template #body-cell-status="cell">
        <q-td :props="cell">
          <q-badge :color="statusColor(cell.row.status)" :label="cell.row.status" />
        </q-td>
      </template>

      <template #body-cell-answered="cell">
        <q-td :props="cell" class="text-center">
          <q-badge v-if="isAnswered(cell.row)" color="positive" label="Answered" />
          <q-badge v-else color="grey-4" text-color="grey-8" label="Unanswered" />
        </q-td>
      </template>

      <template #body-cell-createdOnUtc="cell">
        <q-td :props="cell">{{ $datetime(cell.row.createdOnUtc) }}</q-td>
      </template>

      <template #actions="{ row }">
        <q-btn v-if="canWrite" flat round dense icon="o_reply" color="primary" @click="onAnswer(row)">
          <q-tooltip>{{ isAnswered(row) ? 'Edit answer' : 'Answer' }}</q-tooltip>
        </q-btn>
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
      </template>
    </AppDataTable>

    <!-- Answer form -->
    <AppFormDrawer
      v-model="answerOpen"
      :title="answerRow && isAnswered(answerRow) ? 'Edit answer' : 'Answer question'"
      :saving="answering"
      submit-label="Publish answer"
      @submit="onSubmitAnswer"
    >
      <div v-if="answerRow" class="column q-gutter-md">
        <div>
          <div class="text-caption text-grey-7">Asked by</div>
          <div>{{ answerRow.askerName || '—' }}<span v-if="answerRow.askerEmail" class="text-grey-7"> · {{ answerRow.askerEmail }}</span></div>
        </div>
        <div>
          <div class="text-caption text-grey-7">Question</div>
          <div style="white-space: pre-wrap">{{ answerRow.questionText || '—' }}</div>
        </div>
        <q-separator />
        <AppTextField
          v-model="answerText"
          label="Answer"
          required
          type="textarea"
          autogrow
          input-style="min-height: 120px"
          placeholder="Write a public answer to this question"
          :rules="[(val) => (!!val && val.trim().length > 0) || 'An answer is required']"
        />
      </div>
    </AppFormDrawer>

    <!-- Add a pre-answered FAQ for this product -->
    <ProductFaqFormDrawer
      v-model="faqFormOpen"
      :product-id="productId"
      :product-name="productName"
      @created="onFaqCreated"
    />
  </div>
</template>

<script setup>
/*
 * ProductQuestionsPanel: the "FAQ" tab on the product detail page. A product-scoped
 * slice of the WO-58 Q&A moderation queue — questions filtered to this product, with
 * status/search filters, an answered indicator, per-row Approve / Reject, and an
 * Answer drawer. Mirrors modules/product-qa/pages/index.vue but embedded and without
 * the redundant Product column.
 */
import { ref, watch, onMounted } from 'vue'
import { questionApi } from 'modules/product-qa/api'
import { getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'
import ProductFaqFormDrawer from 'modules/product-qa/components/ProductFaqFormDrawer.vue'

const props = defineProps({
  productId: { type: String, required: true },
  productName: { type: String, default: '' },
  canWrite: { type: Boolean, default: false }
})
const emit = defineEmits(['count'])

const notify = useNotify()

const columns = [
  { name: 'askerName', label: 'Asker', field: 'askerName', align: 'left', sortable: true },
  { name: 'questionText', label: 'Question', field: 'questionText', align: 'left' },
  { name: 'status', label: 'Status', field: 'status', align: 'center', sortable: true },
  { name: 'answered', label: 'Answered', field: 'answered', align: 'center' },
  { name: 'createdOnUtc', label: 'Date', field: 'createdOnUtc', align: 'left', sortable: true }
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

const answerOpen = ref(false)
const answerRow = ref(null)
const answerText = ref('')
const answering = ref(false)

const faqFormOpen = ref(false)

function statusColor (status) {
  if (status === 'Approved') return 'positive'
  if (status === 'Rejected') return 'negative'
  return 'orange'
}

function isAnswered (row) {
  return !!(row && ((row.answerText && String(row.answerText).trim()) || row.answeredOnUtc))
}

function excerpt (text, len = 110) {
  if (!text) return '—'
  const clean = String(text).replace(/\s+/g, ' ').trim()
  return clean.length > len ? `${clean.slice(0, len)}…` : clean
}

async function fetch (reqProps) {
  const p = reqProps?.pagination || pagination.value
  loading.value = true
  try {
    const result = await questionApi.list({
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

function onAnswer (row) {
  answerRow.value = row
  answerText.value = row.answerText || ''
  answerOpen.value = true
}

async function onSubmitAnswer () {
  if (!answerRow.value) return
  const text = (answerText.value || '').trim()
  if (!text) return
  answering.value = true
  try {
    await questionApi.answer(answerRow.value.id, text)
    notify.success('Answer published')
    answerOpen.value = false
    reload()
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    answering.value = false
  }
}

async function onModerate (row, status) {
  moderatingId.value = row.id
  try {
    await questionApi.moderate(row.id, status)
    notify.success(`Question ${status.toLowerCase()}`)
    reload()
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    moderatingId.value = null
  }
}

function onFaqCreated () {
  // The new FAQ may be published (Approved) or a draft (Pending) — clear any status/search filter so
  // it's visible either way, then refresh.
  statusFilter.value = null
  search.value = ''
  reload()
}

// Reload when the panel is pointed at a different product (e.g. route change on the same instance).
watch(() => props.productId, () => { statusFilter.value = null; search.value = ''; reload() })

onMounted(() => fetch())
</script>
