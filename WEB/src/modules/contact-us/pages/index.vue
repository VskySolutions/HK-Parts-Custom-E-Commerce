<template>
  <q-page class="app-page">
    <AppListHeader
      title="Contact Us"
      subtitle="Contact us enquiries submitted by customers through the website."
      :breadcrumbs="[{ label: 'Home', icon: 'o_home', to: '/dashboard' }, { label: 'Contact Us Enquiries' }]"
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
      </template>
    </AppListHeader>

    <AppDataTable
      page-key="contact-us-enquiries"
      row-key="id"
      title="All Contact Us Enquiries"
      :rows="rows"
      :columns="columns"
      :loading="loading"
      :pagination="pagination"
      show-actions
      no-data-label="No contact us enquiries yet."
      @request="onRequest"
      @refresh="reload"
    >
      <template #body-cell-fullName="cell">
        <q-td :props="cell">
          <div class="text-primary text-weight-medium">{{ cell.row.firstName }} {{ cell.row.lastName }}</div>
        </q-td>
      </template>

       <template #body-cell-email="cell">
        <q-td :props="cell">
          <a class="text-primary text-weight-medium">{{ cell.row.email }}</a>
        </q-td>
      </template>

       <template #body-cell-phoneNumber="cell">
        <q-td :props="cell">
          <a class="text-primary text-weight-medium">{{ cell.row.phoneNumber }}</a>
        </q-td>
      </template>

      <template #actions="{ row }">
        <q-btn flat round dense icon="o_visibility" color="primary" @click="onView(row)" >
            <q-tooltip>View</q-tooltip>
        </q-btn>
        <q-btn flat round dense icon="o_delete" color="negative" @click="onDelete(row)">
          <q-tooltip>Delete</q-tooltip>
        </q-btn>
      </template>
    </AppDataTable>
    <q-dialog v-model="showViewDialog">
  <q-card style="width: 550px; max-width: 95vw">
    <q-card-section class="row items-center justify-between">
      <div class="text-h6 text-weight-medium">{{ fullName }}</div>
      <q-btn
        flat
        round
        dense
        icon="o_close"
        v-close-popup
      />
    </q-card-section>

    <q-separator />

    <q-card-section v-if="selectedEnquiry" class="row q-gutter-y-sm">
      <div class="col-md-6 col-sm-6 col-xs-12">
        <div class="text-caption text-grey-7">First Name</div>
        <div class="text-body2 text-weight-medium">
          {{ selectedEnquiry.firstName || '-' }}
        </div>
      </div>

      <div class="col-md-6 col-sm-6 col-xs-12">
        <div class="text-caption text-grey-7">Last Name</div>
        <div class="text-body2 text-weight-medium">
          {{ selectedEnquiry.lastName || '-' }}
        </div>
      </div>

      <div class="col-md-6 col-sm-6 col-xs-12">
        <div class="text-caption text-grey-7">Email</div>
        <div class="text-body2 text-weight-medium">
          {{ selectedEnquiry.email || '-' }}
        </div>
      </div>

      <div class="col-12">
        <div class="text-caption text-grey-7">Phone Number</div>
        <div class="text-body2 text-weight-medium">
          {{ selectedEnquiry.phoneNumber || '-' }}
        </div>
      </div>

      <div class="col-12">
        <div class="text-caption text-grey-7">Comments</div>
        <div class="text-body2 text-weight-medium" style="white-space: pre-wrap">
          {{ selectedEnquiry.comments || '-' }}
        </div>
      </div>
    </q-card-section>

    <q-separator />
  </q-card>
</q-dialog>
  </q-page>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import { contactUsApi, getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'
import { deleteConfirmation } from 'dialogs/delete_confirmation'

const notify = useNotify()

const columns = [
  { name: 'fullName', label: 'Full Name', field: 'fullName', align: 'left', sortable: false },
  { name: 'email', label: 'Email', field: 'email', align: 'left', sortable: true },
  { name: 'phoneNumber', label: 'Phone Number', field: 'phoneNumber', align: 'left', sortable: false }
]

const rows = ref([])
const loading = ref(false)
const search = ref('')
const pagination = ref({ page: 1, rowsPerPage: 10, rowsNumber: 0 })

const showViewDialog = ref(false)
const selectedEnquiry = ref(null)
const fullName = ref("");

async function fetch (props) {
  const p = props?.pagination || pagination.value
  loading.value = true
  try {
    const result = await contactUsApi.list({
      page: p.page,
      pageSize: p.rowsPerPage,
      search: search.value || undefined,
      sortBy: p.sortBy || undefined,
      sortDescending: !!p.descending
    })
    const items = Array.isArray(result) ? result : result?.items || result?.data || []
    const total = Array.isArray(result) ? result.length : result?.totalCount ?? result?.total ?? items.length
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

async function onDelete (row) {
  if (!(await deleteConfirmation(`the enquiry “${row.fullName}”`))) return
  try {
    await contactUsApi.remove(row.id)
    notify.success('Enquiry deleted')
    reload()
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  }
}

function onView (row) {
  selectedEnquiry.value = row
  fullName.value = `${row.firstName} ${row.lastName}`
  showViewDialog.value = true
  console.log('Viewing enquiry:', row)
}

onMounted(() => fetch())
</script>
