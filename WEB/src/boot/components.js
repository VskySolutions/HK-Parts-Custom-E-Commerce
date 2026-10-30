/*
 * Global component registration (WO-94 Step 4 / Step 10).
 *
 * Registers the reusable App* building blocks globally so feature modules can
 * drop them into templates without repetitive imports. Add new shared,
 * app-wide components here.
 */
import AppFieldLabel from 'components/common/AppFieldLabel.vue'
import AppTextField from 'components/common/AppTextField.vue'
import AppPasswordField from 'components/common/AppPasswordField.vue'
import AppSelect from 'components/common/AppSelect.vue'
import AppDateField from 'components/common/AppDateField.vue'
import AppListHeader from 'components/common/AppListHeader.vue'
import AppDetailHeader from 'components/common/AppDetailHeader.vue'
import AppSection from 'components/common/AppSection.vue'
import AppDataTable from 'components/common/AppDataTable.vue'
import AppDrawer from 'components/common/AppDrawer.vue'
import AppFormDrawer from 'components/common/AppFormDrawer.vue'
import AppFilterDrawer from 'components/common/AppFilterDrawer.vue'
import AppViewDrawer from 'components/common/AppViewDrawer.vue'
import AppRichText from 'components/common/AppRichText.vue'
import AppFileUpload from 'components/common/AppFileUpload.vue'
import AppRecordMeta from 'components/common/AppRecordMeta.vue'
import AppLineCustomAttributes from 'components/common/AppLineCustomAttributes.vue'
import MediaSeoDialog from 'components/common/MediaSeoDialog.vue'
import ConfirmDialog from 'components/common/ConfirmDialog.vue'
import { mediaUrl } from 'services/api'
import { formatDate, formatDateTime } from 'src/utils/datetime'

// NOTE: AppPhoneInput (libphonenumber-js) and AppAddressFields
// (country-state-city, ~large city dataset) are intentionally NOT registered
// globally so their heavy data deps stay out of the initial bundle and are
// only loaded when a page imports them directly.
const globalComponents = {
  AppFieldLabel,
  AppTextField,
  AppPasswordField,
  AppSelect,
  AppDateField,
  AppListHeader,
  AppDetailHeader,
  AppSection,
  AppDataTable,
  AppDrawer,
  AppFormDrawer,
  AppFilterDrawer,
  AppViewDrawer,
  AppRichText,
  AppFileUpload,
  AppRecordMeta,
  AppLineCustomAttributes,
  MediaSeoDialog,
  ConfirmDialog
}

export default ({ app }) => {
  for (const [name, component] of Object.entries(globalComponents)) {
    app.component(name, component)
  }
  // Global media-URL resolver: `$media(url)` prefixes domain-less local paths with the API origin
  // (blob/CDN/absolute URLs pass through). Available in every template.
  app.config.globalProperties.$media = mediaUrl

  // Global UTC-aware date formatters (app-wide standard MM/DD/YYYY hh:mm AM/PM). Available in every
  // template as `$datetime(value)` (timestamps) and `$date(value)` (calendar dates).
  app.config.globalProperties.$datetime = formatDateTime
  app.config.globalProperties.$date = formatDate
}
