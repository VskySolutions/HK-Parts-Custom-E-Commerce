/* eslint-env node */

/*
 * Quasar App (app-vite v2) configuration — Vue 3 + Vite, plain JavaScript
 * (ES2022+), SPA mode. Exported as a plain default function (Quasar's
 * `defineConfig` wrapper is only an identity/typing helper).
 *
 * Environment is selected with the QENV variable (default "dev") and loaded
 * from config/env.<QENV>.cjs — see WO-94 Step 2 / Step 3.
 */
import { createRequire } from 'node:module'
import { fileURLToPath } from 'node:url'
import { dirname, resolve } from 'node:path'

const require = createRequire(import.meta.url)
const __dirname = dirname(fileURLToPath(import.meta.url))

// ---- Environment selection --------------------------------------------------
const QENV = process.env.QENV || 'dev'
let env
try {
  env = require(`./config/env.${QENV}.cjs`)
} catch (e) {
  // Fall back to dev so a mistyped QENV never breaks the build.
  console.warn(`[quasar.config] Unknown QENV "${QENV}", falling back to "dev".`)
  env = require('./config/env.dev.cjs')
}

const publicPath = env.IGNORE_PUBLIC_FOLDER ? '/' : (env.BUILD_PUBLIC_PATH || '/')
const distDir = resolve(__dirname, `../publish/spa/${env.PUBLISH_FOLDER || 'dev'}`)

function srcAlias (sub) {
  return resolve(__dirname, 'src', sub)
}

export default function (/* ctx */) {
  return {
    // https://v2.quasar.dev/quasar-cli-vite/boot-files
    boot: ['fonts', 'error', 'frontend-error', 'axios', 'interceptors', 'title', 'components', 'i18n'],

    // https://v2.quasar.dev/quasar-cli-vite/quasar-config-js#css
    // NOTE: quasar.variables.scss is auto-prepended to every SCSS file by the
    // Quasar Vite plugin, so it is intentionally NOT listed here.
    css: ['typography.scss', 'app.scss', 'page.scss', 'custom.scss', 'storefront.scss'],

    // https://github.com/quasarframework/quasar/tree/dev/extras
    extras: [
      'mdi-v5',
      'fontawesome-v6',
      'roboto-font',
      'material-icons-outlined'
    ],

    // https://v2.quasar.dev/quasar-cli-vite/quasar-config-js#build
    build: {
      target: {
        browser: ['es2022', 'firefox115', 'chrome115', 'safari14'],
        node: 'node20'
      },

      vueRouterMode: 'history', // SPA clean URLs; guards redirect to /auth/login

      publicPath,
      distDir,

      // Exposed to the client as process.env.*
      env: {
        API_BASE_URL: env.API_BASE_URL || '',
        QENV
      },

      // Path aliases (WO-94 Step 3). Merged with Quasar's defaults
      // (src, app, components, layouts, pages, assets, boot, stores).
      alias: {
        shared: srcAlias('shared'),
        services: srcAlias('services'),
        validators: srcAlias('validators'),
        modules: srcAlias('modules'),
        dialogs: srcAlias('dialogs'),
        composables: srcAlias('composables')
      },

      // https://v2.quasar.dev/quasar-cli-vite/handling-vite#adding-vite-plugins
      vitePlugins: [
        [
          '@intlify/unplugin-vue-i18n/vite',
          {
            include: [resolve(__dirname, './src/i18n/**')],
            runtimeOnly: false,
            strictMessage: false,
            escapeHtml: false
          }
        ]
      ],

      extendViteConf (viteConf) {
        viteConf.build = viteConf.build || {}
        viteConf.build.rollupOptions = viteConf.build.rollupOptions || {}
        const existingOutput = viteConf.build.rollupOptions.output || {}
        viteConf.build.rollupOptions.output = {
          ...existingOutput,
          manualChunks (id) {
            if (!id.includes('node_modules')) return undefined
            if (/[\\/]node_modules[\\/](@vue|vue|vue-router|vue-i18n|pinia|@vueuse|@intlify)[\\/]/.test(id)) {
              return 'vendor-vue'
            }
            if (/[\\/]node_modules[\\/](quasar|@quasar)[\\/]/.test(id)) {
              return 'vendor-quasar'
            }
            return 'vendor'
          }
        }
      }
    },

    // https://v2.quasar.dev/quasar-cli-vite/quasar-config-js#devserver
    devServer: {
      // Launch the default browser at http://localhost:9000 once the dev server is ready.
      open: true,
      port: 9000,
      // Plain HTTP in dev. NOTE: the Authorize.Net Accept.js and Square Web Payments SDKs refuse to run on an
      // http:// page, so card tokenization for those two gateways cannot be exercised locally while this is false.
      https: false
    },

    // https://v2.quasar.dev/quasar-cli-vite/quasar-config-js#framework
    framework: {
      config: {
        notify: {},
        loading: {}
      },

      iconSet: 'mdi-v5',
      lang: 'en-US',

      // Quasar plugins (WO-94 Step 3)
      plugins: [
        'LocalStorage',
        'Notify',
        'Loading',
        'Dialog',
        'Meta',
        'Cookies',
        'BottomSheet'
      ]
    },

    // https://v2.quasar.dev/options/animations
    animations: []
  }
}
