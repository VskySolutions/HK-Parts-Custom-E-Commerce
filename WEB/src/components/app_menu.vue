<template>
  <q-list padding class="app-menu">
    <template v-for="(section, si) in visibleSections" :key="si">
      <!-- Header-less section (Dashboard): always visible, never collapsible. The
           collapse/expand-all control is merged into the Dashboard row, on the right. -->
      <template v-if="!section.header">
        <q-item
          v-for="(item, ii) in section.items"
          :key="item.to"
          v-ripple
          clickable
          :to="item.to"
          exact
        >
          <q-item-section avatar>
            <q-icon :name="item.icon" />
          </q-item-section>
          <q-item-section>{{ item.label }}</q-item-section>
          <q-item-section v-if="ii === 0" side>
            <q-btn
              flat
              dense
              round
              size="sm"
              color="grey-6"
              :icon="allCollapsed ? 'o_unfold_more' : 'o_unfold_less'"
              :aria-label="allCollapsed ? 'Expand all menu groups' : 'Collapse all menu groups'"
              @click.stop.prevent="toggleAll"
            >
              <q-tooltip anchor="center right" self="center left">
                {{ allCollapsed ? 'Expand all groups' : 'Collapse all groups' }}
              </q-tooltip>
            </q-btn>
          </q-item-section>
        </q-item>
        <q-separator v-if="si < visibleSections.length - 1" class="q-my-sm" />
      </template>

      <!-- Collapsible grouped section: click the header to expand/collapse; state persisted. -->
      <template v-else>
        <q-expansion-item
          :model-value="isOpen(section.header)"
          :label="section.header"
          dense
          dense-toggle
          :duration="180"
          header-class="app-menu__header text-uppercase text-caption text-grey-7"
          expand-icon-class="app-menu__chevron"
          :content-inset-level="0"
          @update:model-value="(val) => setOpen(section.header, val)"
        >
          <q-item
            v-for="item in section.items"
            :key="item.to"
            v-ripple
            clickable
            :to="item.to"
            exact
          >
            <q-item-section avatar>
              <q-icon :name="item.icon" />
            </q-item-section>
            <q-item-section>{{ item.label }}</q-item-section>
          </q-item>
        </q-expansion-item>
        <q-separator v-if="si < visibleSections.length - 1" class="q-my-sm" />
      </template>
    </template>
  </q-list>
</template>

<script setup>
/*
 * AppMenu (WO-94 Step 9; standardized grouping): declarative navigation grouped into
 * priority-ordered categories. Each item may carry a `permissions` array (null = everyone);
 * a section or item may carry a `roles` array to gate by role. Platform-infrastructure lives
 * in a SuperAdmin-only "Platform" section; the store-operations sections are shared by
 * SuperAdmin and the store admin (TenantAdmin). Sections whose items all filter out are dropped.
 *
 * Each headed group is COLLAPSIBLE (q-expansion-item); the open/closed state is persisted per
 * header to LocalStorage so it survives navigation and reloads. The header-less first group
 * (Dashboard) is always visible.
 */
import { computed, reactive } from 'vue'
import { usePermissions, Permissions } from 'composables/usePermissions'
import { useAuthStore } from 'stores/auth'
import { STORAGE_KEYS, getItem, setItem } from 'services/storage'

const { hasAny } = usePermissions()
const auth = useAuthStore()
const userRoles = computed(() => (Array.isArray(auth.roles) ? auth.roles : []))

const sections = [
  {
    header: null,
    items: [
      { label: 'Dashboard', icon: 'o_dashboard', to: '/dashboard', permissions: null },
    ]
  },
  {
    header: 'Store Management',
    items: [
      { label: 'Stores', icon: 'o_store', to: '/stores', permissions: [Permissions.StoresRead] },
      { label: 'Inventory', icon: 'o_warehouse', to: '/catalog/inventory', permissions: [Permissions.CatalogRead] },
      { label: 'Shipping', icon: 'o_local_shipping', to: '/shipping', permissions: [Permissions.StoresRead] },
      { label: 'Tax', icon: 'o_receipt', to: '/tax', permissions: [Permissions.SettingsRead] },
      { label: 'Orders', icon: 'o_receipt_long', to: '/orders', permissions: [Permissions.OrdersRead] },
      { label: 'Returns', icon: 'o_assignment_return', to: '/returns', permissions: [Permissions.OrdersRead] },
      { label: 'Inquiries', icon: 'o_contact_support', to: '/inquiries', permissions: [Permissions.InquiriesRead] },
      { label: 'Subscriptions', icon: 'o_autorenew', to: '/subscriptions', permissions: [Permissions.OrdersRead] },
    ]
  },
  {
    header: 'Catalog',
    items: [
      { label: 'Manufacturers', icon: 'o_factory', to: '/catalog/manufacturers', permissions: [Permissions.CatalogRead] },
      { label: 'Categories', icon: 'o_account_tree', to: '/catalog/categories', permissions: [Permissions.CatalogRead] },
      { label: 'Products', icon: 'o_inventory_2', to: '/catalog/products', permissions: [Permissions.CatalogRead] },
      { label: 'Attributes', icon: 'o_tune', to: '/catalog/attributes', permissions: [Permissions.CatalogRead] },
      { label: 'Promotions', icon: 'o_local_offer', to: '/promotions', permissions: [Permissions.CatalogRead] },
      { label: 'Reviews', icon: 'o_reviews', to: '/catalog/reviews', permissions: [Permissions.CatalogRead] },
      { label: 'Loyalty Points', icon: 'o_loyalty', to: '/loyalty', permissions: [Permissions.CatalogRead] }
    ]
  },
  {
    header: 'CMS',
    items: [
      { label: 'Pages', icon: 'o_description', to: '/cms/pages', permissions: [Permissions.CmsRead] },
      { label: 'Page Groups', icon: 'o_folder', to: '/cms/page-groups', permissions: [Permissions.CmsRead] },
      { label: 'Blog', icon: 'o_article', to: '/cms/blog', permissions: [Permissions.CmsRead] },
      { label: 'Banners', icon: 'o_view_carousel', to: '/cms/banners', permissions: [Permissions.CmsRead] },
      { label: 'Home Sections', icon: 'o_dashboard_customize', to: '/cms/home-sections', permissions: [Permissions.CmsRead] },
      { label: 'Collections', icon: 'o_collections_bookmark', to: '/cms/collections', permissions: [Permissions.CmsRead] },
      { label: 'Featured', icon: 'o_star', to: '/cms/featured', permissions: [Permissions.CmsRead] },
      { label: 'Category Config', icon: 'o_category', to: '/cms/category-config', permissions: [Permissions.CmsRead] },
      { label: 'Search Content', icon: 'o_search', to: '/cms/search-content', permissions: [Permissions.CmsRead] },
      { label: 'Product Q&A', icon: 'o_forum', to: '/cms/product-qa', permissions: [Permissions.CmsRead] },
      { label: 'Newsletter', icon: 'o_email', to: '/cms/newsletter', permissions: [Permissions.CmsRead] },
      { label: 'SEO', icon: 'o_travel_explore', to: '/cms/seo', permissions: [Permissions.CmsRead] }
    ]
  },
  {
    header: 'Customer Management',
    items: [
      { label: 'Customers', icon: 'o_people', to: '/customers', permissions: [Permissions.UsersRead] },
      { label: 'Customer Groups', icon: 'o_sell', to: '/customer-groups', permissions: [Permissions.UsersRead] },
      { label: 'Tax Exemptions', icon: 'o_verified', to: '/tax-exemption-requests', permissions: [Permissions.UsersRead] }
    ]
  },
  {
    header: 'Access Management',
    items: [

      { label: 'Users', icon: 'o_group', to: '/users', permissions: [Permissions.UsersRead] },
      { label: 'Roles', icon: 'o_admin_panel_settings', to: '/roles', permissions: [Permissions.RolesRead] }
    ]
  },
  {
    header: 'Communication',
    items: [
      { label: 'Email Templates', icon: 'o_mail', to: '/email-templates', permissions: [Permissions.EmailTemplatesRead] },
      { label: 'Email Log', icon: 'o_history', to: '/email-log', permissions: [Permissions.EmailLogRead] }
    ]
  },
  {
    header: 'Reports & Logs',
    items: [
      { label: 'Analytics', icon: 'o_insights', to: '/analytics', permissions: [Permissions.DashboardRead] },
      { label: 'Reports', icon: 'o_bar_chart', to: '/operational-reports', permissions: [Permissions.DashboardRead] },
      { label: 'Logs', icon: 'o_bug_report', to: '/logs', permissions: [Permissions.LogsRead] },
      { label: 'Audit Trail', icon: 'o_fact_check', to: '/audit-trail', permissions: [Permissions.AuditTrailRead] }
    ]
  },
  {
    roles: ['SuperAdmin'],
    header: 'Store Setup',
    items: [
      { label: 'Branding', icon: 'o_palette', to: '/branding', permissions: [Permissions.BrandingRead] },
      { label: 'Integrations', icon: 'o_extension', to: '/integrations', permissions: [Permissions.CredentialsRead] },
      { label: 'Currencies', icon: 'o_payments', to: '/currencies', permissions: [Permissions.CurrenciesRead] },
      { label: 'Commerce Mode', icon: 'o_storefront', to: '/settings/commerce', permissions: [Permissions.SettingsRead] },
      { label: 'Admin Alerts', icon: 'o_notification_important', to: '/alerts', permissions: [Permissions.AlertsRead] }
    ]
  },
  // {
  //   header: 'General',
  //   items: [
  //     { label: 'Widgets', icon: 'o_widgets', to: '/widgets', permissions: null }
  //   ]
  // }
]

function hasRole (list) {
  return !Array.isArray(list) || list.length === 0 || list.some((r) => userRoles.value.includes(r))
}

function isVisible (item) {
  return (!item.permissions || hasAny(item.permissions)) && hasRole(item.roles)
}

const visibleSections = computed(() =>
  sections
    .filter((section) => hasRole(section.roles))
    .map((section) => ({ ...section, items: section.items.filter(isVisible) }))
    .filter((section) => section.items.length > 0)
)

// Persisted collapsed state, keyed by section header ({ [header]: true = collapsed }).
// Default: every group expanded, so the menu behaves as before until the user collapses one.
const storedCollapsed = getItem(STORAGE_KEYS.MENU_COLLAPSED, {})
const collapsed = reactive(
  storedCollapsed && typeof storedCollapsed === 'object' && !Array.isArray(storedCollapsed)
    ? { ...storedCollapsed }
    : {}
)

function persist () {
  setItem(STORAGE_KEYS.MENU_COLLAPSED, { ...collapsed })
}

function isOpen (header) {
  return collapsed[header] !== true
}

function setOpen (header, open) {
  collapsed[header] = !open
  persist()
}

// The collapsible (headed) groups currently visible.
const collapsibleHeaders = computed(() =>
  visibleSections.value.filter((s) => s.header).map((s) => s.header)
)

const allCollapsed = computed(() =>
  collapsibleHeaders.value.length > 0 && collapsibleHeaders.value.every((h) => collapsed[h] === true)
)

function toggleAll () {
  const collapse = !allCollapsed.value
  for (const h of collapsibleHeaders.value) collapsed[h] = collapse
  persist()
}
</script>

<style scoped lang="scss">
// Keep the collapsible group header visually identical to the previous static caption header
// (compact, uppercase, muted) — just now clickable with a chevron.
.app-menu :deep(.app-menu__header) {
  min-height: 34px;
  letter-spacing: 0.4px;
  padding-right: 8px;
}

.app-menu :deep(.app-menu__chevron) {
  color: var(--q-grey-6, #9e9e9e);
}
</style>
