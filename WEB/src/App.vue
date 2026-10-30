<template>
  <router-view />
</template>

<script setup>
/*
 * Root component (WO-94 Step 9). Layouts resolve per-route via the router.
 * On mount it initializes the auth store (re-hydrates permissions from any
 * persisted token). The session refreshes silently in the background via the
 * 401 interceptor, so no session-expiry prompt is shown; the login persists for
 * the refresh-token lifetime (30 days).
 */
import { onMounted } from 'vue'
import { useAuthStore } from 'stores/auth'

const auth = useAuthStore()

onMounted(() => {
  auth.initialize()
})
</script>
