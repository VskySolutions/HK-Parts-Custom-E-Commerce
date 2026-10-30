<template>
  <q-page class="app-page">
    <AppListHeader title="Settings" subtitle="Platform and tenant configuration." />

    <q-card flat bordered>
      <q-list separator>
        <q-item>
          <q-item-section>
            <q-item-label>Tenant time zone</q-item-label>
            <q-item-label caption>{{ tenant.timeZone }}</q-item-label>
          </q-item-section>
        </q-item>
        <q-item>
          <q-item-section>
            <q-item-label>Write access</q-item-label>
            <q-item-label caption>{{ canWrite ? 'Yes' : 'Read-only' }}</q-item-label>
          </q-item-section>
        </q-item>
      </q-list>
    </q-card>
  </q-page>
</template>

<script setup>
import { computed } from 'vue'
import { useTenantStore } from 'stores/tenant'
import { usePermissions, Permissions } from 'composables/usePermissions'

const tenant = useTenantStore()
const { has } = usePermissions()
const canWrite = computed(() => has(Permissions.SettingsWrite))
</script>
