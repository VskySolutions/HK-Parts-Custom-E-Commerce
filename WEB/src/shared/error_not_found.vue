<template>
  <div class="fullscreen-center bg-grey-2">
    <div class="text-center q-pa-lg">
      <q-icon name="o_error_outline" size="64px" color="grey-7" />
      <div class="text-h4 q-mt-md">404</div>
      <div class="text-subtitle1 text-grey-8 q-mb-md">
        The page you are looking for does not exist.
      </div>
      <q-btn unelevated color="primary" label="Back to home" to="/" no-caps />
    </div>
  </div>
</template>

<script setup>
/* 404 page (WO-94 Step 7). */
</script>

<style scoped>
.fullscreen-center {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
}
</style>
