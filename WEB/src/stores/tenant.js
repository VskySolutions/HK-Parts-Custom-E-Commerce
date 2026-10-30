/*
 * Tenant store (Pinia, setup style) — WO-94 Step 6.
 *
 * "Tenant" is the single brand/deployment this app is configured for — there is one per
 * deployment and it is not switchable at runtime (stores, not tenants, are the multiple).
 * Owns that brand config: fetches GET /api/tenant/branding, applies the colour palette as
 * CSS custom properties, exposes logo + font + display timezone to the UI, and tracks a
 * global "unsaved form" flag.
 */
import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { setCssVar } from 'quasar'
import { api } from 'services/api'
import { setActiveTimeZone } from 'src/utils/datetime'

function defaultTimeZone () {
  try {
    return Intl.DateTimeFormat().resolvedOptions().timeZone || 'UTC'
  } catch (e) {
    return 'UTC'
  }
}

const DEFAULT_BRANDING = Object.freeze({
  brandName: 'HK Parts E-Commerce',
  primaryColor: null,
  secondaryColor: null,
  accentColor: null,
  logoUrl: null,
  fontFamily: null
})

export const useTenantStore = defineStore('tenant', () => {
  // ---- State ---------------------------------------------------------------
  const hasUnsavedForm = ref(false)
  const branding = ref({ ...DEFAULT_BRANDING })
  const timeZone = ref(defaultTimeZone())

  // ---- Getters -------------------------------------------------------------
  const brandName = computed(() => branding.value.brandName || DEFAULT_BRANDING.brandName)
  const logoUrl = computed(() => branding.value.logoUrl || null)
  const fontFamily = computed(() => branding.value.fontFamily || null)

  // ---- Internal ------------------------------------------------------------
  function applyBranding (b) {
    if (typeof document === 'undefined' || !b) return
    const root = document.documentElement
    // Map the tenant branding palette onto Quasar's brand tokens so every color="primary" /
    // text-primary / bg-primary across the app follows the branding table, and keep the raw
    // --brand-* custom properties for any bespoke usage.
    if (b.primaryColor) {
      root.style.setProperty('--brand-primary', b.primaryColor)
      setCssVar('primary', b.primaryColor)
    }
    if (b.secondaryColor) {
      root.style.setProperty('--brand-secondary', b.secondaryColor)
      setCssVar('secondary', b.secondaryColor)
    }
    if (b.accentColor) {
      root.style.setProperty('--brand-accent', b.accentColor)
      setCssVar('accent', b.accentColor)
    }
    if (b.fontFamily) root.style.setProperty('--brand-font-family', b.fontFamily)
  }

  // ---- Actions -------------------------------------------------------------
  async function loadBranding () {
    try {
      const { data } = await api.get('/api/tenant/branding')
      branding.value = { ...DEFAULT_BRANDING, ...(data || {}) }
    } catch (e) {
      branding.value = { ...DEFAULT_BRANDING }
    }
    applyBranding(branding.value)
    // Admin follows the tenant display timezone (whole-app standard).
    if (branding.value.displayTimeZone) {
      timeZone.value = branding.value.displayTimeZone
      setActiveTimeZone(branding.value.displayTimeZone)
    }
    return branding.value
  }

  function setUnsavedForm (value) {
    hasUnsavedForm.value = !!value
  }

  function clear () {
    hasUnsavedForm.value = false
    branding.value = { ...DEFAULT_BRANDING }
  }

  return {
    // state
    hasUnsavedForm,
    branding,
    timeZone,
    // getters
    brandName,
    logoUrl,
    fontFamily,
    // actions
    loadBranding,
    setUnsavedForm,
    clear
  }
})
