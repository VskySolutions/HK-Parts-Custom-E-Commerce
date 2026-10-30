/*
 * Pinia store factory (WO-94 Step 6).
 *
 * Auto-detected by @quasar/app-vite (src/stores/index) and installed via
 * app.use(store) before boot files run. Exported as a plain function (Quasar's
 * defineStore wrapper is an identity helper).
 */
import { createPinia } from 'pinia'

export default function (/* { ssrContext } */) {
  const pinia = createPinia()
  // Register Pinia plugins here if needed.
  return pinia
}
