<template>
  <q-page class="app-page">
    <AppListHeader
      title="Integrations"
      subtitle="Credentials and settings for payment, tax, shipping, email, SMS and storage providers."
      :breadcrumbs="[{ label: 'Home', icon: 'o_home', to: '/dashboard' }, { label: 'Integrations' }]"
      show-back
      @back="goBack"
    />

    <div class="row no-wrap ic-layout">
      <!-- Left: integration list -->
      <div class="ic-sidebar">
        <q-list padding>
          <template v-for="group in groups" :key="group.category">
            <q-item-label header class="ic-group-header">
              <q-icon :name="group.icon" size="16px" class="q-mr-xs" />{{ group.category }}
            </q-item-label>
            <q-item
              v-for="p in group.items"
              :key="p.key"
              clickable
              v-ripple
              :active="selected.key === p.key"
              active-class="ic-active"
              @click="select(p)"
            >
              <q-item-section avatar>
                <q-icon :name="p.icon" />
              </q-item-section>
              <q-item-section>{{ p.label }}</q-item-section>
            </q-item>
          </template>
        </q-list>
      </div>

      <!-- Right: the panel for the selected item's kind -->
      <div class="ic-content col">
        <component :is="currentPanel" :key="selected.key" :item="selected" @select="selectByKey" />
      </div>
    </div>
  </q-page>
</template>

<script setup>
/*
 * Integrations hub: a single page for all integration-related work. The left list groups every integration
 * by category; the right pane renders the panel matching the selected item's `kind`:
 *   credential -> CredentialPanel (typed secret CRUD)
 *   storage    -> StorageSettingsPanel (file-storage provider settings)
 *   smtp       -> SmtpAccountsPanel (SMTP sending accounts)
 * A `?section=<key>` query preselects an item (used when returning from the SMTP editor).
 */
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { INTEGRATIONS, INTEGRATION_GROUPS, findIntegration } from 'modules/integration-credentials/providers'
import CredentialPanel from 'modules/integration-credentials/components/CredentialPanel.vue'
import StorageSettingsPanel from 'modules/integration-credentials/components/StorageSettingsPanel.vue'
import SmtpAccountsPanel from 'modules/integration-credentials/components/SmtpAccountsPanel.vue'
import RecaptchaPanel from 'modules/integration-credentials/components/RecaptchaPanel.vue'

const route = useRoute()
const router = useRouter()
const groups = INTEGRATION_GROUPS

// Right-aligned Back button in the breadcrumb bar: previous page, or the dashboard on a direct load.
function goBack () {
  if (window.history.length > 1) router.back()
  else router.push('/dashboard')
}

const PANELS = {
  credential: CredentialPanel,
  storage: StorageSettingsPanel,
  smtp: SmtpAccountsPanel,
  recaptcha: RecaptchaPanel
}

const selected = ref(findIntegration(route.query.section) || INTEGRATIONS[0])
const currentPanel = computed(() => PANELS[selected.value.kind] || CredentialPanel)

function select (integration) {
  if (selected.value.key !== integration.key) selected.value = integration
}

function selectByKey (key) {
  const match = findIntegration(key)
  if (match) selected.value = match
}

onMounted(() => {
  const match = findIntegration(route.query.section)
  if (match) selected.value = match
})
</script>

<style scoped lang="scss">
.ic-layout {
  gap: 16px;
  align-items: flex-start;
}
.ic-sidebar {
  width: 240px;
  min-width: 240px;
  border: 1px solid rgba(0, 0, 0, 0.08);
  border-radius: 8px;
  background: #fff;
  position: sticky;
  top: 8px;
}
.ic-group-header {
  font-size: 11px;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.4px;
  color: var(--q-primary);
  padding-top: 10px;
  min-height: unset;
  display: flex;
  align-items: center;
}
.ic-active {
  color: var(--q-primary);
  background: rgba(25, 118, 210, 0.08);
  font-weight: 600;
}
.ic-content {
  min-width: 0;
}
@media (max-width: 720px) {
  .ic-layout { flex-wrap: wrap; }
  .ic-sidebar { width: 100%; min-width: 100%; position: static; }
}
</style>
