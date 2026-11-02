<template>
  <!-- Standalone mobile shell for the barcode inventory scanner (no admin sidebar/header), matching the
       PHP admin's /echo/inventory pages: dark top bar, grey page, optional bottom tab bar. -->
  <div class="inv-shell" :class="{ 'inv-shell--tabs': showTabs }">
    <header v-if="!hideChrome" class="inv-topbar">
      <router-link class="inv-topbar__back" :to="backTo" aria-label="Back">
        <q-icon name="o_arrow_back" size="22px" />
      </router-link>
      <h1 class="inv-topbar__title">{{ title }}</h1>
      <span class="inv-topbar__spacer" />
    </header>

    <main class="inv-content">
      <router-view />
    </main>

    <nav v-if="showTabs" class="inv-tabbar">
      <router-link :to="{ name: 'dashboard' }" class="inv-tabbar__item">
        <q-icon name="o_bar_chart" size="22px" /><span>Dashboard</span>
      </router-link>
      <router-link :to="{ name: 'inventory-scanner-scan' }" class="inv-tabbar__item">
        <q-icon name="o_photo_camera" size="22px" /><span>Scan</span>
      </router-link>
    </nav>
  </div>
</template>

<script setup>
/*
 * Scanner layout. Per-route meta drives it:
 *   scannerTitle — top-bar title (falls back to meta.title)
 *   scannerBack  — route location for the ← button (default: the scanner dashboard)
 *   scannerTabs  — show the Dashboard / Scan bottom tab bar (the PHP dashboard has it)
 *   scannerBare  — no top bar (the full-screen camera page draws its own)
 * "Dashboard" in the tab bar is the admin dashboard, as in PHP (/echo/dashboard).
 */
import { computed } from 'vue'
import { useRoute } from 'vue-router'

const route = useRoute()
const meta = computed(() => route.meta || {})
const title = computed(() => meta.value.scannerTitle || meta.value.title || 'Update Inventory')
const backTo = computed(() => meta.value.scannerBack || { name: 'inventory-scanner' })
const showTabs = computed(() => !!meta.value.scannerTabs)
const hideChrome = computed(() => !!meta.value.scannerBare)
</script>

<style lang="scss">
// Unscoped: the scanner pages share these PHP-parity building blocks (all prefixed .inv-).
$inv-green: #7ac143;
$inv-green-dark: #4c9a2a;
$inv-ink: #161718;
$inv-red: #c0392b;

.inv-shell {
  min-height: 100vh;
  background: #f4f5f7;
  color: #222;
  font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Arial, sans-serif;
  &--tabs { padding-bottom: 70px; }
}
.inv-topbar {
  background: $inv-ink;
  color: #fff;
  padding: 14px 16px;
  display: flex;
  align-items: center;
  gap: 12px;
  position: sticky;
  top: 0;
  z-index: 10;
  &__back { color: #fff; display: flex; text-decoration: none; }
  &__title { font-size: 17px; line-height: 1.3; margin: 0; font-weight: 600; flex: 1; }
  &__spacer { width: 22px; }
}
.inv-content {
  padding: 16px;
  max-width: 760px;
  margin: 0 auto;
}
.inv-tabbar {
  position: fixed;
  left: 0;
  right: 0;
  bottom: 0;
  z-index: 10;
  height: 62px;
  background: #fff;
  border-top: 1px solid #e6e6e6;
  display: flex;
  &__item {
    flex: 1;
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    gap: 2px;
    font-size: 12px;
    color: #999;
    text-decoration: none;
    &.router-link-exact-active { color: $inv-green; font-weight: 700; }
  }
}

// ---- Shared blocks ----
.inv-card {
  background: #fff;
  border-radius: 12px;
  padding: 18px;
  box-shadow: 0 1px 4px rgba(0, 0, 0, 0.08);
  margin-bottom: 16px;
}
.inv-btn {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  width: 100%;
  padding: 15px;
  border-radius: 10px;
  font-size: 15px;
  font-weight: 600;
  border: none;
  cursor: pointer;
  text-decoration: none;
  font-family: inherit;
  &:disabled { cursor: not-allowed; opacity: 0.6; }
  &--green { background: $inv-green; color: #fff; box-shadow: 0 2px 8px rgba(122, 193, 67, 0.35); }
  &--green:disabled { background: #c8e6b0; box-shadow: none; opacity: 1; }
  &--dark { background: $inv-ink; color: #fff; }
  &--light { background: #fff; color: #333; border: 1px solid #ddd; }
  &--grey { background: #f4f5f7; color: #333; }
  & + & { margin-top: 10px; }
}
.inv-section-title {
  font-size: 14px;
  font-weight: 700;
  color: #444;
  margin: 4px 0 10px;
  text-transform: uppercase;
  letter-spacing: 0.03em;
}
.inv-label {
  display: block;
  font-size: 12px;
  color: #888;
  margin-bottom: 6px;
}
.inv-input {
  width: 100%;
  padding: 12px;
  font-size: 15px;
  border: 1px solid #ddd;
  border-radius: 8px;
  font-family: inherit;
  background: #fff;
  &:focus { outline: none; border-color: $inv-green; box-shadow: 0 0 0 2px rgba(122, 193, 67, 0.2); }
  &:disabled { background: #f4f5f7; color: #999; }
}
.inv-error {
  background: #fbe9e7;
  color: $inv-red;
  padding: 10px 14px;
  border-radius: 8px;
  font-size: 13px;
  margin-bottom: 12px;
}
.inv-badge {
  display: inline-block;
  font-size: 11px;
  font-weight: 600;
  padding: 4px 10px;
  border-radius: 20px;
  &--active { background: #eaf7e0; color: $inv-green-dark; }
  &--inactive { background: #fbe9e7; color: $inv-red; }
}
.inv-product-image {
  height: 180px;
  border-radius: 8px;
  background: #f0f0f0;
  display: flex;
  align-items: center;
  justify-content: center;
  color: #bbb;
  font-size: 13px;
  margin-bottom: 12px;
  overflow: hidden;
  img { width: 100%; height: 100%; object-fit: contain; }
}
.inv-qty-up { color: $inv-green; }
.inv-qty-down { color: $inv-red; }
</style>
