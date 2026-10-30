<template>
  <q-btn-dropdown
    flat
    no-caps
    :label="label"
    icon="o_account_circle"
    class="user-info"
  >
    <q-list style="min-width: 220px">
      <q-item-label header class="text-caption">
        {{ auth.user?.email || 'Not signed in' }}
      </q-item-label>

      <q-item v-if="auth.role" dense>
        <q-item-section avatar>
          <q-icon name="o_badge" />
        </q-item-section>
        <q-item-section>
          <q-item-label caption>Role</q-item-label>
          <q-item-label>{{ auth.role }}</q-item-label>
        </q-item-section>
      </q-item>

      <q-separator />

      <q-item v-close-popup clickable @click="onLogout">
        <q-item-section avatar>
          <q-icon name="o_logout" />
        </q-item-section>
        <q-item-section>Sign out</q-item-section>
      </q-item>
    </q-list>
  </q-btn-dropdown>
</template>

<script setup>
import { computed } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from 'stores/auth'
import { useTenantStore } from 'stores/tenant'
import { useNotify } from 'composables/useNotify'

const auth = useAuthStore()
const tenant = useTenantStore()
const router = useRouter()
const notify = useNotify()

const label = computed(() => auth.user?.fullName || auth.user?.email || 'Account')

async function onLogout () {
  await auth.logout()
  tenant.clear()
  notify.info('You have been signed out')
  router.push('/auth/login')
}
</script>
