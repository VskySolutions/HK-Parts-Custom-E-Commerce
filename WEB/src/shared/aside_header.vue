<template>
  <router-link
    :to="{ name: 'shop-home' }"
    class="aside-header row items-center no-wrap q-px-md q-py-md"
    aria-label="Go to storefront"
  >
    <q-avatar
      v-if="tenant.logoUrl"
      size="36px"
      class="q-mr-sm"
      square
    >
      <img :src="$media(tenant.logoUrl)" alt="logo" />
    </q-avatar>
    <q-icon
      v-else
      name="o_storefront"
      size="30px"
      color="primary"
      class="q-mr-sm"
    />

    <div class="column">
      <div class="text-subtitle1 text-weight-medium ellipsis">
        {{ tenant.brandName }}
      </div>
    </div>

    <q-tooltip anchor="bottom middle" self="top middle">Visit the storefront</q-tooltip>
  </router-link>
</template>

<script setup>
/* Drawer/menu brand header: tenant logo + name. The whole block is a link to the public storefront
 * landing (/shop) so the logo and title are both clickable. */
import { useTenantStore } from 'stores/tenant'

const tenant = useTenantStore()
</script>

<style scoped>
.aside-header {
  min-height: 64px;
  border-bottom: 1px solid rgba(0, 0, 0, 0.08);
  color: inherit;
  text-decoration: none;
  cursor: pointer;
  transition: background-color 0.15s ease;
}
.aside-header:hover {
  background: rgba(0, 0, 0, 0.03);
}
</style>
