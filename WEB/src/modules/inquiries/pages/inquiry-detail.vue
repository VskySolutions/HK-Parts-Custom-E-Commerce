<template>
  <q-page class="app-page">
    <AppDetailHeader
      :breadcrumbs="[
        { label: 'Home', icon: 'o_home', to: '/dashboard' },
        { label: 'Inquiries', to: '/inquiries' },
        { label: inquiry ? inquiry.referenceNumber : 'Inquiry' }
      ]"
      :status="inquiry ? humanStatus(inquiry.inquiryStatus) : ''"
      :status-color="inquiry ? inquiryStatusColor(inquiry.inquiryStatus) : 'grey'"
      @back="$router.push({ name: 'admin-inquiries' })"
    >
      <template #actions>
        <q-btn
          v-if="canWrite && !isConverted"
          outline
          color="grey-8"
          no-caps
          icon="o_mail"
          label="Send quote"
          class="q-mr-xs"
          @click="openQuote"
        />
        <q-btn
          v-if="canWrite && !isConverted && !isInquiryOnlyTenant"
          unelevated
          color="primary"
          no-caps
          icon="o_shopping_cart_checkout"
          label="Convert to order"
          @click="openConvert"
        />
      </template>
    </AppDetailHeader>

    <q-inner-loading :showing="loading" />

    <div v-if="inquiry" class="row q-col-gutter-md">
      <div class="col-12 col-md-8">
        <!-- Customer information is the deliverable of an inquiry, so it leads. -->
        <AppSection title="Customer information">
          <div class="row q-col-gutter-md text-body2">
            <div class="col-12 col-sm-6">
              <div class="text-caption text-grey-7">Name</div>
              <div class="text-weight-medium">{{ inquiry.contactName || '—' }}</div>
              <div v-if="inquiry.companyName" class="text-grey-8">{{ inquiry.companyName }}</div>
            </div>
            <div class="col-12 col-sm-6">
              <div class="text-caption text-grey-7">Contact</div>
              <div>
                <a v-if="inquiry.contactEmail" :href="`mailto:${inquiry.contactEmail}`" class="text-primary">
                  {{ inquiry.contactEmail }}
                </a>
                <span v-else>—</span>
              </div>
              <div v-if="inquiry.contactPhone">
                <a :href="`tel:${inquiry.contactPhone}`" class="text-primary">{{ inquiry.contactPhone }}</a>
                <span v-if="inquiry.preferredContact" class="text-grey-7">
                  · prefers {{ inquiry.preferredContact }}
                </span>
              </div>
            </div>
            <div class="col-12 col-sm-6">
              <div class="text-caption text-grey-7">Submitted</div>
              <div>{{ formatDate(inquiry.submittedOnUtc) }}</div>
            </div>
            <div class="col-12 col-sm-6">
              <div class="text-caption text-grey-7">Needed by</div>
              <div>{{ inquiry.requiredByUtc ? formatDate(inquiry.requiredByUtc) : 'Not specified' }}</div>
            </div>
            <div v-if="fullAddress" class="col-12">
              <div class="text-caption text-grey-7">Address</div>
              <div>{{ fullAddress }}</div>
            </div>
            <div class="col-12">
              <div class="text-caption text-grey-7">Message</div>
              <div class="inq-message">{{ inquiry.message || 'No message was left.' }}</div>
            </div>
          </div>
        </AppSection>

        <AppSection title="Requested items">
          <q-markup-table flat dense wrap-cells>
            <thead>
              <tr>
                <th class="text-left">Product</th>
                <th class="text-left">SKU</th>
                <th class="text-center">Qty</th>
                <th class="text-right">Unit</th>
                <th class="text-right">Line</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="line in inquiry.lines" :key="line.id">
                <td class="text-left">
                  {{ line.productName }}
                  <AppLineCustomAttributes :attributes="line.customAttributes" />
                </td>
                <td class="text-left text-grey-8">{{ line.sku || '—' }}</td>
                <td class="text-center">{{ line.quantity }}</td>
                <td class="text-right">{{ formatMoney(line.unitPrice) }}</td>
                <td class="text-right">{{ formatMoney(line.lineTotal) }}</td>
              </tr>
            </tbody>
          </q-markup-table>

          <q-separator class="q-my-md" />

          <div class="row justify-end text-body2">
            <div style="min-width: 260px">
              <div class="row justify-between q-mb-xs">
                <span class="text-grey-8">Subtotal</span>
                <span>{{ formatMoney(inquiry.subtotal) }}</span>
              </div>
              <div v-if="inquiry.discountTotal > 0" class="row justify-between q-mb-xs">
                <span class="text-grey-8">Discounts</span>
                <span>-{{ formatMoney(inquiry.discountTotal) }}</span>
              </div>
              <div class="row justify-between text-weight-bold">
                <span>Indicative value</span>
                <span>{{ formatMoney(inquiry.estimatedValue) }}</span>
              </div>
              <div class="text-caption text-muted q-mt-xs">
                Shipping and tax are not included — neither was calculated for this request.
              </div>
            </div>
          </div>
        </AppSection>
      </div>

      <div class="col-12 col-md-4">
        <AppSection title="Pipeline">
          <AppSelect
            v-model="statusDraft"
            label="Status"
            :options="selectableStatusOptions"
            :disable="!canWrite || isConverted"
          />
          <div v-if="inquiry.quotedOnUtc" class="text-caption text-muted q-mb-md">
            Quote sent {{ formatDate(inquiry.quotedOnUtc) }}
          </div>

          <q-input
            v-model="notesDraft"
            outlined
            type="textarea"
            autogrow
            label="Internal notes"
            hint="Never shown to the buyer."
            maxlength="4000"
            :disable="!canWrite || isConverted"
            class="q-mb-md"
          />

          <q-btn
            unelevated
            color="primary"
            no-caps
            class="full-width"
            label="Save"
            :loading="saving"
            :disable="!canWrite || isConverted || !dirty"
            @click="save"
          />
        </AppSection>

        <AppSection title="Assignment">
          <div class="text-body2">
            <div class="text-caption text-grey-7">Store</div>
            <div>{{ inquiry.assignedStoreName || 'Not assigned' }}</div>
            <div v-if="!inquiry.assignedStoreId" class="text-caption text-orange-9 q-mt-xs">
              No store was matched. Set a default inquiry store under Commerce Mode so requests always
              reach someone.
            </div>
          </div>
        </AppSection>

        <AppSection v-if="isConverted" title="Converted">
          <div class="text-body2">
            This inquiry became an order.
            <q-btn
              flat
              dense
              no-caps
              color="primary"
              label="Open the order"
              :to="{ name: 'admin-order-detail', params: { id: inquiry.id } }"
            />
          </div>
        </AppSection>
      </div>
    </div>

    <!-- Send quote -->
    <q-dialog v-model="quoteOpen">
      <q-card style="min-width: 380px">
        <q-card-section class="text-subtitle1 text-weight-bold">Send a quote</q-card-section>
        <q-card-section class="q-gutter-md">
          <AppTextField
            v-model="quoteAmount"
            label="Quoted amount"
            type="number"
            :hint="`In ${inquiry ? inquiry.currencyCode : ''}. The buyer sees this figure, not the indicative value.`"
          />
          <q-input
            v-model="quoteNote"
            outlined
            type="textarea"
            autogrow
            label="Note to the buyer"
            maxlength="2000"
          />
        </q-card-section>
        <q-card-actions align="right">
          <q-btn flat no-caps label="Cancel" @click="quoteOpen = false" />
          <q-btn
            unelevated
            color="primary"
            no-caps
            label="Send quote"
            :loading="quoting"
            :disable="quoteAmount === '' || quoteAmount === null"
            @click="sendQuote"
          />
        </q-card-actions>
      </q-card>
    </q-dialog>

    <!-- Convert to order -->
    <q-dialog v-model="convertOpen">
      <q-card style="min-width: 380px">
        <q-card-section class="text-subtitle1 text-weight-bold">Convert to an order</q-card-section>
        <q-card-section class="q-gutter-md">
          <div class="text-body2 text-muted">
            The request becomes a payable order awaiting payment, keeping its reference, items and
            history. Stock is committed when the order is paid, as with any other order.
          </div>
          <AppTextField
            v-model="convertTotal"
            label="Agreed total"
            type="number"
            hint="Leave as-is to honour the quoted figure."
          />
          <q-input v-model="convertNote" outlined type="textarea" autogrow label="Note (optional)" maxlength="1000" />
        </q-card-section>
        <q-card-actions align="right">
          <q-btn flat no-caps label="Cancel" @click="convertOpen = false" />
          <q-btn
            unelevated
            color="primary"
            no-caps
            label="Convert"
            :loading="converting"
            @click="convert"
          />
        </q-card-actions>
      </q-card>
    </q-dialog>

    <AppRecordMeta entity-type="order" :record-id="inquiry?.id" />
  </q-page>
</template>

<script setup>
/*
 * Admin inquiry detail (REQ-INQ-001).
 *
 * Reads as a lead, not an order: who asked, how to reach them, what they want, and where the request
 * stands. Sending a quote emails the buyer and stamps the pipeline; converting turns the request into
 * a payable order in place (Standard mode only — an inquiry-only tenant has no order to convert into,
 * so that action is hidden).
 */
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'
import { usePermissions, Permissions } from 'composables/usePermissions'
import { commerceApi } from 'modules/settings/api'
import { formatMoney, formatDate } from 'modules/orders/api'
import { inquiryApi, inquiryStatusOptions, inquiryStatusColor, humanStatus } from 'modules/inquiries/api'

const route = useRoute()
const router = useRouter()
const notify = useNotify()
const { has } = usePermissions()
const canWrite = computed(() => has(Permissions.InquiriesWrite))

const inquiry = ref(null)
const loading = ref(false)
const saving = ref(false)
const statusDraft = ref(null)
const notesDraft = ref('')
const isInquiryOnlyTenant = ref(false)

const quoteOpen = ref(false)
const quoting = ref(false)
const quoteAmount = ref('')
const quoteNote = ref('')

const convertOpen = ref(false)
const converting = ref(false)
const convertTotal = ref('')
const convertNote = ref('')

const isConverted = computed(() => inquiry.value?.inquiryStatus === 'Converted')

// Converted is reached only through the convert action, so it is never offered as a manual choice.
const selectableStatusOptions = computed(() =>
  inquiryStatusOptions.filter((o) => o.value !== 'Converted')
)

const dirty = computed(() =>
  !!inquiry.value &&
  (statusDraft.value !== inquiry.value.inquiryStatus ||
    (notesDraft.value || '') !== (inquiry.value.internalNotes || ''))
)

const fullAddress = computed(() => {
  const i = inquiry.value
  if (!i) return ''
  return [i.addressLine1, i.addressLine2, i.landmark, i.city, i.stateProvince, i.postalCode, i.countryCode]
    .filter(Boolean)
    .join(', ')
})

function apply (dto) {
  inquiry.value = dto
  statusDraft.value = dto.inquiryStatus
  notesDraft.value = dto.internalNotes || ''
}

async function load () {
  loading.value = true
  try {
    apply(await inquiryApi.get(route.params.id))
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    loading.value = false
  }
}

async function save () {
  saving.value = true
  try {
    apply(await inquiryApi.update(route.params.id, {
      inquiryStatus: statusDraft.value,
      internalNotes: notesDraft.value
    }))
    notify.success('Inquiry updated.')
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    saving.value = false
  }
}

function openQuote () {
  quoteAmount.value = inquiry.value ? inquiry.value.estimatedValue : ''
  quoteNote.value = ''
  quoteOpen.value = true
}

async function sendQuote () {
  quoting.value = true
  try {
    apply(await inquiryApi.sendQuote(route.params.id, {
      amount: Number(quoteAmount.value),
      note: quoteNote.value || null
    }))
    quoteOpen.value = false
    notify.success('Quote sent to the buyer.')
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    quoting.value = false
  }
}

function openConvert () {
  convertTotal.value = inquiry.value ? inquiry.value.estimatedValue : ''
  convertNote.value = ''
  convertOpen.value = true
}

async function convert () {
  converting.value = true
  try {
    await inquiryApi.convert(route.params.id, {
      totalOverride: convertTotal.value === '' ? null : Number(convertTotal.value),
      note: convertNote.value || null
    })
    convertOpen.value = false
    notify.success('Inquiry converted to an order.')
    router.push({ name: 'admin-order-detail', params: { id: route.params.id } })
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    converting.value = false
  }
}

onMounted(async () => {
  // Convert is meaningless in inquiry-only mode; hide the action rather than let it 409.
  isInquiryOnlyTenant.value = await commerceApi.getMode()
    .then((m) => m?.mode === 'InquiryOnly')
    .catch(() => false)
  load()
})
</script>

<style scoped>
.inq-message {
  white-space: pre-wrap;
}
</style>
