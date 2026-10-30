/*
 * usePermissions() + the frozen Permissions catalog (WO-94 Step 8).
 *
 * Permissions are the canonical <Resource>.<Action> strings the UI gates on.
 * The actual check delegates to the auth store, which applies the role-based
 * graceful-degrade rule for the current permission-less backend.
 */
import { useAuthStore } from 'stores/auth'

export const Permissions = Object.freeze({
  TenantsRead: 'Tenants.Read',
  TenantsWrite: 'Tenants.Write',
  UsersRead: 'Users.Read',
  UsersWrite: 'Users.Write',
  RolesRead: 'Roles.Read',
  RolesWrite: 'Roles.Write',
  StoresRead: 'Stores.Read',
  StoresWrite: 'Stores.Write',
  CurrenciesRead: 'Currencies.Read',
  CurrenciesWrite: 'Currencies.Write',
  EmailTemplatesRead: 'EmailTemplates.Read',
  EmailTemplatesWrite: 'EmailTemplates.Write',
  SettingsRead: 'Settings.Read',
  SettingsWrite: 'Settings.Write',
  CredentialsRead: 'Credentials.Read',
  CredentialsWrite: 'Credentials.Write',
  BrandingRead: 'Branding.Read',
  BrandingWrite: 'Branding.Write',
  CatalogRead: 'Catalog.Read',
  CatalogWrite: 'Catalog.Write',
  StorageRead: 'Storage.Read',
  StorageWrite: 'Storage.Write',
  SmtpAccountsRead: 'SmtpAccounts.Read',
  SmtpAccountsWrite: 'SmtpAccounts.Write',
  EmailLogRead: 'EmailLog.Read',
  EmailLogWrite: 'EmailLog.Write',
  OrdersRead: 'Orders.Read',
  OrdersWrite: 'Orders.Write',
  WebhooksRead: 'Webhooks.Read',
  WebhooksWrite: 'Webhooks.Write',
  AlertsRead: 'Alerts.Read',
  AlertsWrite: 'Alerts.Write',
  CmsRead: 'Cms.Read',
  CmsWrite: 'Cms.Write',
  DashboardRead: 'Dashboard.Read',
  LogsRead: 'Logs.Read',
  AuditTrailRead: 'AuditTrail.Read',
  InquiriesRead: 'Inquiries.Read',
  InquiriesWrite: 'Inquiries.Write'
})

export function usePermissions () {
  const auth = useAuthStore()

  function has (permission) {
    return auth.hasPermission(permission)
  }

  function hasAny (list) {
    return auth.hasAnyPermission(list)
  }

  return { has, hasAny, Permissions }
}

export default usePermissions
