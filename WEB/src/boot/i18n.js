/*
 * vue-i18n boot file (WO-94 Step 4 / Step 11).
 *
 * Composition API mode (legacy: false). Messages live under src/i18n and are
 * additionally processed by @intlify/unplugin-vue-i18n (see quasar.config.js).
 */
import { createI18n } from 'vue-i18n'
import messages from 'src/i18n'

const DEFAULT_LOCALE = 'en-US'

export const i18n = createI18n({
  legacy: false,
  globalInjection: true,
  locale: DEFAULT_LOCALE,
  fallbackLocale: DEFAULT_LOCALE,
  messages
})

export default ({ app }) => {
  app.use(i18n)
}
