<template>
  <q-page class="app-page">
    <AppDetailHeader
      :breadcrumbs="[
        { label: 'Home', icon: 'o_home', to: '/dashboard' },
        { label: 'Customers', to: '/customers' },
        { label: fullName || 'Customer' }
      ]"
      :status="customer ? (customer.emailVerified ? 'Verified' : 'Unverified') : ''"
      :status-color="customer && customer.emailVerified ? 'positive' : 'orange'"
      @back="$router.push('/customers')"
    />

    <q-inner-loading :showing="loading" />

    <template v-if="customer">
      <div class="row q-col-gutter-md">
        <!-- Left column: profile + account + pricing/tax -->
        <div class="col-12 col-md-5">
          <AppSection title="Profile">
            <div class="text-h6 q-mb-xs">{{ fullName }}</div>
            <q-list dense>
              <q-item class="q-px-none"><q-item-section avatar><q-icon name="o_mail" color="grey-7" /></q-item-section><q-item-section>{{ customer.email || '—' }}</q-item-section></q-item>
              <q-item class="q-px-none"><q-item-section avatar><q-icon name="o_call" color="grey-7" /></q-item-section><q-item-section>{{ customer.phoneNumber || '—' }}</q-item-section></q-item>
              <q-item v-if="customer.whatsAppPhoneNumber" class="q-px-none"><q-item-section avatar><q-icon name="o_chat" color="green-7" /></q-item-section><q-item-section>{{ customer.whatsAppPhoneNumber }} <span class="text-caption text-grey-6">(WhatsApp{{ customer.whatsAppOptIn ? ', opted-in' : '' }})</span></q-item-section></q-item>
              <q-item class="q-px-none"><q-item-section avatar><q-icon name="o_event" color="grey-7" /></q-item-section><q-item-section>Joined {{ formatDate(customer.createdOnUtc) }}</q-item-section></q-item>
            </q-list>
          </AppSection>

          <AppSection title="Account">
            <div class="row items-center justify-between no-wrap">
              <div class="col">
                <div class="text-body2">Account active</div>
                <div class="text-caption text-grey-6">{{ customer.isActive ? 'Customer can sign in and place orders.' : 'Sign-in is disabled for this customer.' }}</div>
              </div>
              <q-toggle
                :model-value="customer.isActive"
                :disable="!canWrite || activeSaving"
                color="primary"
                @update:model-value="onToggleActive"
              />
            </div>
          </AppSection>

          <AppSection title="Pricing group">
            <AppSelect
              :model-value="groupId"
              label="Customer group"
              :options="groupOptions"
              :disable="!canWrite || groupSaving"
              :loading="groupSaving"
              clearable
              placeholder="Base pricing (no group)"
              hint="Assign a single pricing group, or clear for base pricing."
              @update:model-value="onChangeGroup"
            />
            <div v-if="groupRuleHint" class="row items-center text-caption text-grey-7 q-mt-xs">
              <q-icon name="o_sell" size="14px" class="q-mr-xs" />{{ groupRuleHint }}
            </div>
          </AppSection>

          <AppSection title="Tax exemption">
            <div class="row items-center q-gutter-sm q-mb-xs">
              <q-chip square dense :color="taxStatus.color" text-color="white" :label="taxStatus.label" />
              <router-link
                v-if="customer.latestTaxExemptionRequestId"
                :to="{ name: 'admin-tax-exemption-requests' }"
                class="text-primary text-caption"
              >View request</router-link>
            </div>
            <q-list v-if="customer.isTaxExempt" dense>
              <q-item v-if="customer.taxExemptionCertificate" class="q-px-none">
                <q-item-section avatar><q-icon name="o_verified" color="grey-7" /></q-item-section>
                <q-item-section>
                  <q-item-label caption>Exemption certificate</q-item-label>
                  <q-item-label>{{ customer.taxExemptionCertificate }}</q-item-label>
                </q-item-section>
              </q-item>
              <q-item v-if="customer.vatId" class="q-px-none">
                <q-item-section avatar><q-icon name="o_badge" color="grey-7" /></q-item-section>
                <q-item-section>
                  <q-item-label caption>VAT ID</q-item-label>
                  <q-item-label>{{ customer.vatId }}</q-item-label>
                </q-item-section>
              </q-item>
            </q-list>
            <div class="row items-center text-caption text-grey-6 q-mt-sm">
              <q-icon name="o_info" size="14px" class="q-mr-xs" />Managed in Tax Exemptions
            </div>
          </AppSection>

          <AppSection title="Store credit">
            <template #actions>
              <q-btn v-if="canWrite" color="primary" outline dense no-caps icon="o_add" label="Grant" @click="grantDialog = true" />
            </template>
            <div class="row items-baseline q-gutter-sm q-mb-sm">
              <div class="text-h5 text-primary">{{ formatMoney(storeCredit.balance) }}</div>
              <div class="text-caption text-grey-6">{{ storeCredit.currencyCode }} balance</div>
            </div>
            <q-list v-if="storeCredit.transactions.length" dense separator bordered class="rounded-borders">
              <q-item v-for="t in storeCredit.transactions.slice(0, 6)" :key="t.id">
                <q-item-section>
                  <q-item-label>{{ t.reason || t.type }}</q-item-label>
                  <q-item-label caption>{{ formatDate(t.createdOnUtc) }} · {{ t.type }}</q-item-label>
                </q-item-section>
                <q-item-section side :class="t.amount < 0 ? 'text-negative' : 'text-positive'">
                  {{ t.amount < 0 ? '' : '+' }}{{ formatMoney(t.amount) }}
                </q-item-section>
              </q-item>
            </q-list>
            <div v-else class="text-grey-6 text-caption">No store-credit activity.</div>
          </AppSection>
        </div>

        <!-- Right column: stats + orders + addresses -->
        <div class="col-12 col-md-7">
          <div class="row q-col-gutter-md q-mb-md">
            <div class="col-6">
              <q-card flat bordered class="app-section text-center q-pa-md">
                <div class="text-caption text-grey-7">Orders</div>
                <div class="text-h5 text-primary">{{ customer.orderCount }}</div>
              </q-card>
            </div>
            <div class="col-6">
              <q-card flat bordered class="app-section text-center q-pa-md">
                <div class="text-caption text-grey-7">Lifetime spend</div>
                <div class="text-h5 text-primary">{{ formatMoney(customer.totalSpent) }}</div>
              </q-card>
            </div>
          </div>

          <AppSection title="Recent orders">
            <q-markup-table flat dense v-if="customer.recentOrders.length">
              <thead>
                <tr>
                  <th class="text-left">Order #</th>
                  <th class="text-left">Placed</th>
                  <th class="text-left">Status</th>
                  <th class="text-left">Payment</th>
                  <th class="text-right">Total</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="o in customer.recentOrders" :key="o.id" class="cursor-pointer" @click="openOrder(o)">
                  <td class="text-left text-primary">{{ o.orderNumber }}</td>
                  <td class="text-left">{{ formatDate(o.placedOnUtc) }}</td>
                  <td class="text-left"><q-badge :color="statusColor(o.status)" :label="o.status" /></td>
                  <td class="text-left">{{ o.paymentStatus }}</td>
                  <td class="text-right">{{ o.currencyCode }} {{ formatMoney(o.totalAmount) }}</td>
                </tr>
              </tbody>
            </q-markup-table>
            <div v-else class="text-grey-6 text-caption">No orders placed yet.</div>
          </AppSection>

          <AppSection title="Address book">
            <div v-if="!customer.addresses.length" class="text-grey-6 text-caption">No saved addresses.</div>
            <div v-else class="row q-col-gutter-sm">
              <div v-for="a in customer.addresses" :key="a.id" class="col-12 col-sm-6">
                <q-card flat bordered class="q-pa-sm full-height">
                  <div class="row items-center q-gutter-xs q-mb-xs">
                    <q-badge :color="a.addressType === 'Billing' ? 'purple' : 'teal'" :label="a.addressType" />
                    <q-badge v-if="a.isDefault" color="primary" outline label="Default" />
                  </div>
                  <div class="text-body2">{{ a.firstName }} {{ a.lastName }}</div>
                  <div v-if="a.company" class="text-caption text-grey-7">{{ a.company }}</div>
                  <div class="text-caption">{{ a.addressLine1 }}<template v-if="a.addressLine2">, {{ a.addressLine2 }}</template></div>
                  <div class="text-caption">{{ [a.city, a.stateProvince, a.postalCode].filter(Boolean).join(', ') }}</div>
                  <div class="text-caption">{{ a.countryCode }}<template v-if="a.phoneNumber"> · {{ a.phoneNumber }}</template></div>
                </q-card>
              </div>
            </div>
          </AppSection>
        </div>
      </div>

      <!-- Grant store credit -->
      <q-dialog v-model="grantDialog">
        <q-card style="min-width: 380px">
          <q-card-section class="text-subtitle1 text-weight-medium">Grant store credit</q-card-section>
          <q-separator />
          <q-card-section class="q-gutter-sm">
            <AppTextField v-model="grantForm.amount" label="Amount" type="number" step="0.01" placeholder="e.g. 25.00" />
            <AppTextField v-model="grantForm.reason" label="Reason" placeholder="e.g. Goodwill credit" />
          </q-card-section>
          <q-separator />
          <q-card-actions align="right">
            <q-btn flat no-caps label="Cancel" v-close-popup />
            <q-btn color="primary" unelevated no-caps label="Grant" :loading="granting" @click="grant" />
          </q-card-actions>
        </q-card>
      </q-dialog>
    </template>

    <AppRecordMeta entity-type="customer" :record-id="customer?.id" />
  </q-page>
</template>

<script setup>
/*
 * Customer detail (WO-117): full admin view of a single customer — profile, account
 * activate/deactivate, single Customer Group (pricing) assignment, read-only tax-exemption
 * status, lifetime order stats, recent order history (links to the order detail), the saved
 * address book and store credit. Tax-exemption review lives in its own module (WO-126).
 */
import { ref, reactive, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { getApiErrorMessage } from 'services/api'
import { usePermissions } from 'composables/usePermissions'
import { useNotify } from 'composables/useNotify'
import { customerAdminApi, customerGroupOptionsApi } from 'modules/customers/api'
import { formatMoney, formatDate, orderStatusColor as statusColor } from 'modules/orders/api'
import AppDetailHeader from 'components/common/AppDetailHeader.vue'
import AppSection from 'components/common/AppSection.vue'
import AppTextField from 'components/common/AppTextField.vue'

const route = useRoute()
const router = useRouter()
const notify = useNotify()
const { has } = usePermissions()
const canWrite = computed(() => has('Users.Write'))

const customer = ref(null)
const loading = ref(false)
const activeSaving = ref(false)

// Single Customer Group (pricing) assignment.
const groups = ref([]) // active groups: [{ id, name, pricingRuleType, discountPercent }]
const groupId = ref(null)
const groupSaving = ref(false)

const storeCredit = ref({ balance: 0, currencyCode: 'USD', transactions: [] })
const grantDialog = ref(false)
const granting = ref(false)
const grantForm = reactive({ amount: null, reason: '' })

const fullName = computed(() => customer.value ? `${customer.value.firstName} ${customer.value.lastName}`.trim() : '')

// Read-only tax-exemption status chip mapping.
const TAX_STATUS = {
  NotSubmitted: { label: 'Not submitted', color: 'grey' },
  PendingReview: { label: 'Pending review', color: 'orange' },
  Approved: { label: 'Approved', color: 'positive' },
  Rejected: { label: 'Rejected', color: 'negative' }
}
const taxStatus = computed(() =>
  TAX_STATUS[customer.value?.taxExemptionStatus] || { label: customer.value?.taxExemptionStatus || 'Not submitted', color: 'grey' }
)

// Options include the customer's current group even if it is no longer active, so the select
// never shows a blank for an assigned-but-inactive group.
const groupOptions = computed(() => {
  const opts = groups.value.map((g) => ({ label: g.name, value: g.id }))
  const cur = customer.value?.customerGroup
  if (cur && cur.id && !opts.some((o) => o.value === cur.id)) {
    opts.unshift({ label: `${cur.name} (inactive)`, value: cur.id })
  }
  return opts
})

const currentGroup = computed(() => {
  if (!groupId.value) return null
  return groups.value.find((g) => g.id === groupId.value) ||
    (customer.value?.customerGroup?.id === groupId.value ? customer.value.customerGroup : null)
})
const groupRuleHint = computed(() => {
  const g = currentGroup.value
  if (!g) return ''
  if (g.pricingRuleType === 'PercentageDiscount') return `${g.name} — ${g.discountPercent || 0}% off`
  if (g.pricingRuleType === 'FixedGroupPrice') return `${g.name} — fixed group prices`
  return `${g.name} — base price`
})

async function load () {
  loading.value = true
  try {
    const c = await customerAdminApi.get(route.params.id)
    customer.value = c
    groupId.value = c.customerGroup?.id || null
    loadStoreCredit()
  } catch (e) { notify.error(getApiErrorMessage(e)) } finally { loading.value = false }
}

async function loadGroups () {
  try {
    const r = await customerGroupOptionsApi.listActive()
    groups.value = Array.isArray(r) ? r : r?.items || []
  } catch (e) { groups.value = [] }
}

async function onToggleActive (val) {
  activeSaving.value = true
  try {
    customer.value = await customerAdminApi.setActive(customer.value.id, val)
    notify.success(val ? 'Account activated' : 'Account deactivated')
  } catch (e) { notify.error(getApiErrorMessage(e)) } finally { activeSaving.value = false }
}

async function onChangeGroup (val) {
  const prev = groupId.value
  const next = val || null
  if (next === prev) return
  groupId.value = next // optimistic; reverted on failure
  groupSaving.value = true
  try {
    await customerAdminApi.setGroup(customer.value.id, next)
    if (customer.value) {
      customer.value.customerGroup = next ? (groups.value.find((g) => g.id === next) || customer.value.customerGroup) : null
    }
    notify.success(next ? 'Pricing group updated' : 'Pricing group cleared — base pricing applies')
  } catch (e) {
    groupId.value = prev
    notify.error(getApiErrorMessage(e))
  } finally { groupSaving.value = false }
}

async function loadStoreCredit () {
  try {
    storeCredit.value = await customerAdminApi.getStoreCredit(route.params.id)
  } catch (e) { storeCredit.value = { balance: 0, currencyCode: 'USD', transactions: [] } }
}

async function grant () {
  const amount = Number(grantForm.amount)
  if (!amount || amount <= 0) { notify.warning('Enter an amount greater than zero'); return }
  granting.value = true
  try {
    storeCredit.value = await customerAdminApi.issueStoreCredit(route.params.id, { amount, reason: grantForm.reason || null })
    notify.success('Store credit granted')
    grantDialog.value = false
    grantForm.amount = null
    grantForm.reason = ''
  } catch (e) { notify.error(getApiErrorMessage(e)) } finally { granting.value = false }
}

function openOrder (o) { router.push({ name: 'admin-order-detail', params: { id: o.id } }) }

onMounted(() => { load(); loadGroups() })
</script>
