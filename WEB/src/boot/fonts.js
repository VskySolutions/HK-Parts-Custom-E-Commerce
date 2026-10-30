/*
 * Self-hosted Poppins — the admin portal's default font-family (matches the app's offline-first
 * font strategy, like Roboto via quasar `extras`). Weights used across the UI: 400 (body),
 * 500 (field labels), 600 (section/page titles). The family itself is set in quasar.variables.scss.
 */
import '@fontsource/poppins/400.css'
import '@fontsource/poppins/500.css'
import '@fontsource/poppins/600.css'

import { boot } from 'quasar/wrappers'

export default boot(() => {})
