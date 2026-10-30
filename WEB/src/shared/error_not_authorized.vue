<template>
  <div class="fullscreen-center bg-grey-2">
    <div class="text-center q-pa-lg">
      <q-icon name="o_lock" size="64px" color="warning" />
      <div class="text-h4 q-mt-md">403</div>
      <div class="text-subtitle1 text-grey-8 q-mb-md">
        You are not authorized to view this page.
      </div>
      <q-btn unelevated color="primary" label="Back to dashboard" to="/dashboard" no-caps />
    </div>
  </div>
</template>

<script setup>
/* Not-authorized page (WO-94 Step 7). */
</script>

<style scoped>
.fullscreen-center {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
}
</style>
