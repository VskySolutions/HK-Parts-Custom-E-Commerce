<template>
  <q-page class="app-page">
    <AppDetailHeader
      :title="isNew ? 'New customer' : (fullName || 'Customer')"
      :breadcrumbs="[
        { label: 'Home', icon: 'o_home', to: '/dashboard' },
        { label: 'Customers', to: '/customers' },
        { label: isNew ? 'New customer' : (fullName || 'Customer') }
      ]"
      :status="customer && !isNew ? (customer.emailVerified ? 'Verified' : 'Unverified') : ''"
      :status-color="customer && customer.emailVerified ? 'positive' : 'orange'"
      show-back
      @back="$router.push('/customers')"
    />

    <q-inner-loading :showing="loading" color="primary" />

    <q-banner v-if="!isNew && !loading && !customer" class="bg-grey-2 rounded-borders">
      Customer not found.
    </q-banner>

    <template v-if="isNew || customer">
      <q-card flat bordered class="app-section">
        <q-tabs
          v-model="tab"
          align="left"
          active-color="primary"
          indicator-color="primary"
          class="text-grey-7 app-customer-tabs"
          no-caps
          inline-label
          mobile-arrows
        >
          <q-tab name="general" icon="o_info" label="General" />
          <q-tab name="orders" :disable="isNew">
            <div class="row items-center no-wrap">
              <q-icon name="o_receipt_long" class="app-customer-tabs__icon" />Orders
              <q-badge v-if="customer?.orderCount" color="grey-5" class="q-ml-xs">{{ customer.orderCount }}</q-badge>
            </div>
          </q-tab>
          <q-tab name="wishlist" :disable="isNew">
            <div class="row items-center no-wrap">
              <q-icon name="o_favorite_border" class="app-customer-tabs__icon" />Wishlist
              <q-badge v-if="customer?.wishlistItemCount" color="grey-5" class="q-ml-xs">{{ customer.wishlistItemCount }}</q-badge>
            </div>
          </q-tab>
          <q-tab name="carts" :disable="isNew" icon="o_remove_shopping_cart" label="Abandoned carts" />
          <q-tab name="addresses" :disable="isNew">
            <div class="row items-center no-wrap">
              <q-icon name="o_location_on" class="app-customer-tabs__icon" />Addresses
              <q-badge v-if="customer?.addresses?.length" color="grey-5" class="q-ml-xs">{{ customer.addresses.length }}</q-badge>
            </div>
          </q-tab>
          <q-tab name="documents" :disable="isNew">
            <div class="row items-center no-wrap">
              <q-icon name="o_folder_open" class="app-customer-tabs__icon" />Documents
              <q-badge v-if="customer?.activeDocumentCount" color="grey-5" class="q-ml-xs">{{ customer.activeDocumentCount }}</q-badge>
            </div>
          </q-tab>
        </q-tabs>

        <q-separator />

        <q-tab-panels v-model="tab" animated keep-alive>
          <!-- ============ GENERAL ============ -->
          <q-tab-panel name="general" class="q-gutter-y-sm">
            <!-- ===== ADD MODE: new-customer form ===== -->
            <template v-if="isNew">
              <div class="row q-col-gutter-md">
                <!-- Left: Profile + Custom information (same arrangement as the edit view). -->
                <div class="col-12 col-md-6">
                  <AppSection title="Profile">
                    <div class="row q-col-gutter-sm">
                      <div class="col-12 col-sm-6">
                        <AppTextField v-model="form.firstName" label="First name" required />
                      </div>
                      <div class="col-12 col-sm-6">
                        <AppTextField v-model="form.lastName" label="Last name" />
                      </div>
                      <div class="col-12 col-sm-6">
                        <AppTextField v-model="form.email" label="Email" type="email" required />
                      </div>
                      <div class="col-12 col-sm-6">
                        <AppTextField v-model="form.phoneNumber" label="Phone number" />
                      </div>
                    </div>
                  </AppSection>
                  <AppSection title="Custom information">
                    <div class="q-gutter-y-sm">
                      <AppTextField v-model="form.longText" label="Long" type="textarea" autogrow :maxlength="LONG_TEXT_MAX" counter />
                      <AppSelect v-model="form.allowRestrictedCapacityPurchases" label="Allow Restricted Capacity Purchases" :options="yesNoOptions" />
                    </div>
                  </AppSection>
                </div>
                <!-- Right: Account + Pricing group. -->
                <div class="col-12 col-md-6">
                  <AppSection title="Account">
                    <div class="q-gutter-y-sm">
                      <AppTextField
                        v-model="form.password"
                        label="Password"
                        :type="showPassword ? 'text' : 'password'"
                        required
                        hint="Min. 8 characters. Share it with the customer, or they can use Forgot password."
                      />
                      <div class="row items-center justify-between">
                        <q-checkbox v-model="showPassword" dense label="Show password" />
                        <q-btn flat dense no-caps color="primary" icon="o_autorenew" label="Generate" @click="generatePassword" />
                      </div>
                      <q-toggle v-model="form.isActive" color="primary" label="Account active" />
                      <q-toggle v-model="form.emailVerified" color="primary" label="Mark email as verified" />
                      <div v-if="!form.emailVerified" class="text-caption text-grey-6">
                        A verification email will be sent to the customer.
                      </div>
                    </div>
                  </AppSection>
                  <AppSection title="Pricing group">
                    <AppSelect
                      v-model="form.customerGroupId"
                      label="Customer group"
                      :options="groupOptions"
                      clearable
                      placeholder="Base pricing (no group)"
                    />
                  </AppSection>
                </div>
              </div>
              <div class="row items-center justify-between q-pt-sm">
                <div class="text-caption text-grey-6">
                  Orders, Wishlist, Abandoned carts, Addresses and Documents are enabled after the customer is created.
                </div>
                <div class="q-gutter-sm">
                  <q-btn flat no-caps label="Cancel" @click="$router.push('/customers')" />
                  <q-btn color="primary" unelevated no-caps icon="o_save" label="Create customer" :loading="creating" @click="createCustomer" />
                </div>
              </div>
            </template>

            <!-- ===== EDIT MODE: existing General view ===== -->
            <template v-else>
            <!-- Stats -->
            <div class="row q-col-gutter-sm q-mb-md">
              <div class="col-6 col-md-3">
                <q-card flat bordered class="text-center q-pa-md">
                  <div class="text-caption text-grey-7">Orders</div>
                  <div class="text-h5 text-primary">{{ customer.orderCount }}</div>
                </q-card>
              </div>
              <div class="col-6 col-md-3">
                <q-card flat bordered class="text-center q-pa-md">
                  <div class="text-caption text-grey-7">Lifetime spend</div>
                  <div class="text-h5 text-primary">{{ formatMoney(customer.totalSpent) }}</div>
                </q-card>
              </div>
              <div class="col-6 col-md-3">
                <q-card flat bordered class="text-center q-pa-md">
                  <div class="text-caption text-grey-7">Store credit</div>
                  <div class="text-h5 text-primary">{{ formatMoney(storeCredit.balance) }}</div>
                </q-card>
              </div>
              <div class="col-6 col-md-3">
                <q-card flat bordered class="text-center q-pa-md">
                  <div class="text-caption text-grey-7">Joined</div>
                  <div class="text-h6 text-primary">{{ formatDate(customer.createdOnUtc) }}</div>
                </q-card>
              </div>
            </div>

            <div class="row q-col-gutter-md">
              <!-- Left: the fields saved by the Save button below (Profile + Custom information). -->
              <div class="col-12 col-md-6">
                <!-- Profile: always shown as fields. Email stays read-only. -->
                <AppSection title="Profile">
                  <div class="row q-col-gutter-sm">
                    <div class="col-12 col-sm-6">
                      <AppTextField v-model="editForm.firstName" label="First name" required maxlength="200" :disable="!canWrite || saving" />
                    </div>
                    <div class="col-12 col-sm-6">
                      <AppTextField v-model="editForm.lastName" label="Last name" maxlength="200" :disable="!canWrite || saving" />
                    </div>
                    <div class="col-12 col-sm-6">
                      <AppTextField :model-value="customer.email" label="Email" readonly disable hint="Email can't be changed." />
                    </div>
                    <div class="col-12 col-sm-6">
                      <AppTextField v-model="editForm.phoneNumber" label="Phone number" maxlength="50" :disable="!canWrite || saving" />
                    </div>
                  </div>
                </AppSection>

                <AppSection title="Custom information">
                  <div class="q-gutter-y-sm">
                    <AppTextField v-model="editForm.longText" label="Long" type="textarea" autogrow :maxlength="LONG_TEXT_MAX" counter :disable="!canWrite || saving" />
                    <AppSelect v-model="editForm.allowRestrictedCapacityPurchases" label="Allow Restricted Capacity Purchases" :options="yesNoOptions" :disable="!canWrite || saving" />
                  </div>
                </AppSection>

                <!-- Admin password reset: its own form + action, independent of the profile Save below. -->
                <AppSection v-if="canWrite" title="Reset password" danger>
                  <q-form ref="resetFormRef" greedy @submit.prevent="resetPassword">
                    <div class="row q-col-gutter-sm">
                      <div class="col-12 col-sm-6">
                        <AppPasswordField
                          v-model="resetForm.newPassword"
                          label="New password"
                          required
                          strength
                          :rules="newPasswordRules"
                          :disable="resetting"
                        />
                      </div>
                      <div class="col-12 col-sm-6">
                        <AppPasswordField
                          v-model="resetForm.confirmPassword"
                          label="Confirm new password"
                          required
                          :rules="confirmPasswordRules"
                          :disable="resetting"
                        />
                      </div>
                    </div>
                    <div class="row justify-end q-pt-sm">
                      <q-btn type="submit" color="negative" unelevated no-caps icon="o_lock_reset" label="Reset password" :loading="resetting" />
                    </div>
                  </q-form>
                </AppSection>
              </div>

              <!-- Right: settings that save immediately, then read-only tax status. -->
              <div class="col-12 col-md-6">
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
              </div>
            </div>

            <!-- Same action row as the Add customer page. Save applies the Profile and Custom information
                 fields; account, pricing group and store credit still save on their own. -->
            <div v-if="canWrite" class="row items-center justify-end q-pt-sm">
              <div class="q-gutter-sm">
                <q-btn flat no-caps label="Cancel" :disable="saving" @click="$router.push('/customers')" />
                <q-btn color="primary" unelevated no-caps icon="o_save" label="Save" :loading="saving" @click="saveProfile" />
              </div>
            </div>
            </template>
          </q-tab-panel>

          <!-- ============ ORDERS ============ -->
          <q-tab-panel name="orders" class="q-gutter-y-sm">
            <q-table
              v-if="customer.recentOrders.length"
              flat bordered dense
              class="app-table-card"
              row-key="id"
              :rows="customer.recentOrders"
              :columns="orderColumns"
              :filter="orderSearch"
              v-model:pagination="orderPagination"
              :rows-per-page-options="[10, 25, 50, 0]"
              binary-state-sort
              @row-click="(_, row) => openOrder(row)"
            >
              <template #top>
                <div class="app-section__title col ellipsis">Orders</div>
                <q-input
                  v-model="orderSearch"
                  dense outlined debounce="300"
                  placeholder="Search order number or status"
                  style="min-width: 240px"
                >
                  <template #prepend><q-icon name="o_search" /></template>
                  <template v-if="orderSearch" #append>
                    <q-icon name="o_close" class="cursor-pointer" @click="orderSearch = ''" />
                  </template>
                </q-input>
              </template>
              <template #body-cell-orderNumber="p">
                <q-td :props="p" class="text-primary cursor-pointer">{{ p.row.orderNumber }}</q-td>
              </template>
              <template #body-cell-placedOnUtc="p">
                <q-td :props="p">{{ formatDate(p.row.placedOnUtc) }}</q-td>
              </template>
              <template #body-cell-status="p">
                <q-td :props="p"><q-badge :color="statusColor(p.row.status)" :label="p.row.status" /></q-td>
              </template>
              <template #body-cell-totalAmount="p">
                <q-td :props="p" class="text-right">{{ p.row.currencyCode }} {{ formatMoney(p.row.totalAmount) }}</q-td>
              </template>
            </q-table>
            <div v-else class="text-grey-6 text-caption">No orders placed yet.</div>
          </q-tab-panel>

          <!-- ============ WISHLIST ============ -->
          <q-tab-panel name="wishlist">
            <CustomerWishlistPanel
              v-if="customer"
              :key="customer.id"
              :customer-id="customer.id"
              @count="(n) => { if (customer) customer.wishlistItemCount = n }"
            />
          </q-tab-panel>

          <!-- ============ ABANDONED CARTS ============ -->
          <q-tab-panel name="carts">
            <div class="text-grey-6 text-caption">Abandoned carts — coming soon.</div>
          </q-tab-panel>

          <!-- ============ ADDRESSES ============ -->
          <q-tab-panel name="addresses" class="q-gutter-y-sm">
            <div v-if="!customer.addresses.length" class="text-grey-6 text-caption">No saved addresses.</div>
            <div v-else class="row q-col-gutter-sm">
              <div v-for="a in customer.addresses" :key="a.id" class="col-12 col-sm-6 col-lg-4">
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
          </q-tab-panel>

          <!-- ============ DOCUMENTS ============ -->
          <q-tab-panel name="documents">
            <CustomerDocumentsPanel
              v-if="customer"
              :key="customer.id"
              :customer-id="customer.id"
              :can-write="canWrite"
              @count="(n) => { if (customer) customer.activeDocumentCount = n }"
            />
          </q-tab-panel>
        </q-tab-panels>
      </q-card>
    </template>

    <AppRecordMeta v-if="!isNew" entity-type="customer" :record-id="customer?.id" />
  </q-page>
</template>

<script setup>
/*
 * Customer detail: tabbed admin view (same pattern as the Product page) —
 *   General · Orders · Wishlist · Abandoned carts · Addresses · Documents.
 * General keeps profile, account toggle, pricing group and tax-exemption status (store credit shows only as a stat tile).
 * Profile is shown as fields (editable for Users.Write) and saved with the bottom Save button, like Add mode;
 * email stays read-only.
 * Wishlist: CustomerWishlistPanel (read-only, server-paged image / name / SKU); badge = item count in active wishlists.
 * Abandoned carts is a tab placeholder only (no implementation yet).
 * Documents: CustomerDocumentsPanel (private-storage uploads, view/edit/soft-delete); badge = active count.
 * One page for Add + Edit: route /customers/new (no :id) = Add mode, only General enabled with a create form;
 * after "Create customer" it opens /customers/:id (Edit mode) with every tab enabled.
 */
import { ref, reactive, computed, watch, onMounted, nextTick } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { getApiErrorMessage } from 'services/api'
import { usePermissions } from 'composables/usePermissions'
import { useNotify } from 'composables/useNotify'
import { useConfirm } from 'composables/useConfirm'
import { passwordRules, matchRule } from 'validators'
import { customerAdminApi, customerGroupOptionsApi } from 'modules/customers/api'
import { formatMoney, formatDate, orderStatusColor as statusColor } from 'modules/orders/api'
import AppDetailHeader from 'components/common/AppDetailHeader.vue'
import AppSection from 'components/common/AppSection.vue'
import AppTextField from 'components/common/AppTextField.vue'
import AppSelect from 'components/common/AppSelect.vue'
import AppPasswordField from 'components/common/AppPasswordField.vue'
import CustomerDocumentsPanel from 'modules/customers/components/CustomerDocumentsPanel.vue'
import CustomerWishlistPanel from 'modules/customers/components/CustomerWishlistPanel.vue'

const route = useRoute()
const router = useRouter()
const notify = useNotify()
const { has } = usePermissions()
const canWrite = computed(() => has('Users.Write'))

const tab = ref('general')

// Add mode = no :id in the route (/customers/new). Edit mode = /customers/:id.
const isNew = computed(() => !route.params.id)
const creating = ref(false)
const showPassword = ref(false)
const form = reactive({
  firstName: '',
  lastName: '',
  email: '',
  phoneNumber: '',
  password: '',
  isActive: true,
  emailVerified: true,
  customerGroupId: null,
  longText: '',
  allowRestrictedCapacityPurchases: false
})

const customer = ref(null)
const loading = ref(false)
const activeSaving = ref(false)

// Profile fields (General tab), seeded from the loaded record. Email is read-only, so it is not part of the form.
const saving = ref(false)
const editForm = reactive({ firstName: '', lastName: '', phoneNumber: '', longText: '', allowRestrictedCapacityPurchases: false })

// ---------- Custom information ----------
// "Allow Restricted Capacity Purchases" reuses the No / Yes choices of the existing product attribute
// "Restrict Non High Capacity Mags States" (db-scripts/2_..._Seed_PhpProductAttributes.sql); stored as a boolean.
const yesNoOptions = [
  { label: 'No', value: false },
  { label: 'Yes', value: true }
]
const LONG_TEXT_MAX = 4000 // matches Customers.LongText nvarchar(4000)
// Mirrors UpdateCustomerCommandValidator.PhonePattern on the API.
const PHONE_PATTERN = /^\+?[0-9\s.()-]{5,50}$/

// Single Customer Group (pricing) assignment.
const groups = ref([])
const groupId = ref(null)
const groupSaving = ref(false)

// Balance for the "Store credit" stat tile (the Store credit section / Grant action were removed).
const storeCredit = ref({ balance: 0, currencyCode: 'USD', transactions: [] })

const fullName = computed(() => customer.value ? `${customer.value.firstName} ${customer.value.lastName}`.trim() : '')

// ---------- Tax exemption ----------
const TAX_STATUS = {
  NotSubmitted: { label: 'Not submitted', color: 'grey' },
  PendingReview: { label: 'Pending review', color: 'orange' },
  Approved: { label: 'Approved', color: 'positive' },
  Rejected: { label: 'Rejected', color: 'negative' }
}
const taxStatus = computed(() =>
  TAX_STATUS[customer.value?.taxExemptionStatus] || { label: customer.value?.taxExemptionStatus || 'Not submitted', color: 'grey' }
)

// ---------- Pricing group ----------
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

// ---------- Orders tab ----------
const orderSearch = ref('')
const orderPagination = ref({ sortBy: 'placedOnUtc', descending: true, page: 1, rowsPerPage: 10 })
const orderColumns = [
  { name: 'orderNumber', label: 'Order #', field: 'orderNumber', align: 'left', sortable: true },
  { name: 'placedOnUtc', label: 'Placed', field: 'placedOnUtc', align: 'left', sortable: true },
  { name: 'status', label: 'Status', field: 'status', align: 'left', sortable: true },
  { name: 'paymentStatus', label: 'Payment', field: 'paymentStatus', align: 'left', sortable: true },
  { name: 'totalAmount', label: 'Total', field: 'totalAmount', align: 'right', sortable: true }
]

// ---------- Loaders ----------
async function load () {
  loading.value = true
  try {
    const c = await customerAdminApi.get(route.params.id)
    customer.value = c
    fillEditForm()
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

async function loadStoreCredit () {
  try {
    storeCredit.value = await customerAdminApi.getStoreCredit(route.params.id)
  } catch (e) { storeCredit.value = { balance: 0, currencyCode: 'USD', transactions: [] } }
}

// ---------- Add customer ----------
function generatePassword () {
  const chars = 'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789'
  const sym = '!@#$%'
  const rnd = (n) => crypto.getRandomValues(new Uint32Array(1))[0] % n
  let pwd = ''
  for (let i = 0; i < 10; i++) pwd += chars[rnd(chars.length)]
  pwd += sym[rnd(sym.length)] + (rnd(9) + 1)
  form.password = pwd
  showPassword.value = true
}

async function createCustomer () {
  if (!form.firstName.trim()) { notify.warning('First name is required'); return }
  if (!/^\S+@\S+\.\S+$/.test(form.email.trim())) { notify.warning('Enter a valid email address'); return }
  if (!form.password || form.password.length < 8) { notify.warning('Password must be at least 8 characters'); return }
  creating.value = true
  try {
    // POST /admin/customers → returns the new customer id.
    const res = await customerAdminApi.create({
      firstName: form.firstName.trim(),
      lastName: form.lastName.trim(),
      email: form.email.trim(),
      phoneNumber: form.phoneNumber.trim() || null,
      password: form.password,
      isActive: form.isActive,
      emailVerified: form.emailVerified,
      longText: form.longText.trim() || null,
      allowRestrictedCapacityPurchases: !!form.allowRestrictedCapacityPurchases
    })
    const id = res?.id || res
    if (form.customerGroupId) {
      try { await customerAdminApi.setGroup(id, form.customerGroupId) } catch (e) { notify.warning('Customer created, but the pricing group could not be set') }
    }
    notify.success('Customer created')
    // Same component instance is reused; the route watcher below loads the record and enables all tabs.
    router.replace({ name: 'admin-customer-detail', params: { id } })
  } catch (e) { notify.error(getApiErrorMessage(e)) } finally { creating.value = false }
}

// ---------- Edit profile ----------
function fillEditForm () {
  const c = customer.value || {}
  editForm.firstName = c.firstName || ''
  editForm.lastName = c.lastName || ''
  editForm.phoneNumber = c.phoneNumber || ''
  editForm.longText = c.longText || ''
  editForm.allowRestrictedCapacityPurchases = !!c.allowRestrictedCapacityPurchases
}

async function saveProfile () {
  if (saving.value) return
  const phone = editForm.phoneNumber.trim()
  if (!editForm.firstName.trim()) { notify.warning('First name is required'); return }
  if (phone && !PHONE_PATTERN.test(phone)) { notify.warning('Enter a valid phone number (digits, spaces and + - . ( ) only)'); return }
  saving.value = true
  try {
    // PUT /admin/customers/:id → returns the refreshed detail.
    customer.value = await customerAdminApi.update(customer.value.id, {
      firstName: editForm.firstName.trim(),
      lastName: editForm.lastName.trim(),
      phoneNumber: phone || null,
      longText: editForm.longText.trim() || null,
      allowRestrictedCapacityPurchases: !!editForm.allowRestrictedCapacityPurchases
    })
    fillEditForm()
    notify.success('Customer updated')
  } catch (e) { notify.error(getApiErrorMessage(e)) } finally { saving.value = false }
}

// ---------- Reset password ----------
// Shared password policy (validators/index.js ↔ API PasswordRules.cs). Values are never logged or kept after success.
const confirm = useConfirm()
const resetFormRef = ref(null)
const resetting = ref(false)
const resetForm = reactive({ newPassword: '', confirmPassword: '' })
const newPasswordRules = [(v) => !!v || 'New password is required', ...passwordRules()]
const confirmPasswordRules = [
  (v) => !!v || 'Please confirm the new password',
  matchRule(() => resetForm.newPassword)
]

async function resetPassword () {
  if (resetting.value) return
  const valid = await resetFormRef.value?.validate()
  if (!valid) return
  const ok = await confirm({
    title: 'Reset password',
    message: `Set a new password for "${fullName.value || customer.value.email}"? They will be signed out of all devices.`,
    okLabel: 'Reset password',
    color: 'negative'
  })
  if (!ok) return
  resetting.value = true
  try {
    await customerAdminApi.resetPassword(customer.value.id, {
      newPassword: resetForm.newPassword,
      confirmPassword: resetForm.confirmPassword
    })
    resetForm.newPassword = ''
    resetForm.confirmPassword = ''
    await nextTick()
    resetFormRef.value?.resetValidation()
    notify.success('Password reset')
  } catch (e) { notify.error(getApiErrorMessage(e)) } finally { resetting.value = false }
}

// ---------- Actions ----------
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
  groupId.value = next
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

function openOrder (o) { router.push({ name: 'admin-order-detail', params: { id: o.id } }) }

// Add → Edit switch (same component, new :id): load the record and return to General.
watch(() => route.params.id, (id) => { if (id) { tab.value = 'general'; load() } })

onMounted(() => { if (!isNew.value) load(); loadGroups() })
</script>

<style scoped lang="scss">
.app-customer-tabs {
  :deep(.q-tab) { min-height: 44px; }

  // One icon size for every tab: the built-in `icon` prop (General, Abandoned carts) and the
  // inline icons used by tabs that carry a count badge (Orders, Wishlist, Addresses, Documents).
  $tab-icon-size: 20px;
  :deep(.q-tab__icon),
  :deep(.app-customer-tabs__icon) {
    font-size: $tab-icon-size;
    width: $tab-icon-size;
    height: $tab-icon-size;
  }
  :deep(.app-customer-tabs__icon) { margin-right: 6px; }
}
</style>