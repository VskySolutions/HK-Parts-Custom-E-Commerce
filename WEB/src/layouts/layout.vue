<template>
  <q-layout view="lHh Lpr lFf">
    <q-header elevated>
      <q-toolbar>
        <q-btn
          flat
          dense
          round
          icon="o_menu"
          aria-label="Menu"
          @click="toggleDrawer"
        />

        <q-btn flat no-caps class="text-weight-medium q-ml-xs" @click="goHome">
          <q-icon name="o_storefront" size="22px" class="q-mr-sm" />
          {{ tenant.brandName }}
        </q-btn>

        <q-space />

        <q-btn
          flat
          no-caps
          class="visit-store-btn q-mr-sm"
          type="a"
          :href="storeHref"
          target="_blank"
          rel="noopener"
        >
          <q-icon name="o_storefront" size="20px" />
          <span class="gt-xs q-ml-xs">Visit store</span>
          <q-tooltip>Open the storefront in a new tab</q-tooltip>
        </q-btn>

        <UserInfo />
      </q-toolbar>
    </q-header>

    <q-drawer
      v-model="leftDrawerOpen"
      show-if-above
      bordered
      :width="292"
      :breakpoint="900"
    >
      <div class="column full-height no-wrap">
        <AsideHeader />
        <q-scroll-area class="col">
          <AppMenu />
        </q-scroll-area>
      </div>
    </q-drawer>

    <q-page-container>
      <router-view />
    </q-page-container>
  </q-layout>
</template>

<script setup>
/*
 * Authenticated application shell (WO-94 Step 9). Header with menu toggle,
 * brand button, Visit store and the user menu. A 292px left drawer (persisted
 * to LocalStorage["leftDrawerOpen"]) hosts the aside header + navigation menu.
 */
import { ref, watch, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useTenantStore } from 'stores/tenant'
import { STORAGE_KEYS, getItem, setItem } from 'services/storage'
import AppMenu from 'components/app_menu.vue'
import AsideHeader from 'shared/aside_header.vue'
import UserInfo from 'shared/user_info.vue'

const tenant = useTenantStore()
const router = useRouter()

// Absolute href to the public storefront (respects router base/history mode); opened in a new tab.
const storeHref = router.resolve({ name: 'shop-home' }).href

const leftDrawerOpen = ref(getItem(STORAGE_KEYS.LEFT_DRAWER_OPEN, true) !== false)
watch(leftDrawerOpen, (val) => setItem(STORAGE_KEYS.LEFT_DRAWER_OPEN, val))

function toggleDrawer () {
  leftDrawerOpen.value = !leftDrawerOpen.value
}

function goHome () {
  router.push('/dashboard')
}

onMounted(() => {
  tenant.loadBranding().catch(() => {})
})
</script>
