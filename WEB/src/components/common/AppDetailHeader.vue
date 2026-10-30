<template>
  <q-card flat bordered class="app-toolbar q-px-md q-py-sm q-mb-md">
    <div class="row items-center q-gutter-sm">
      <div class="col flex">
        <q-breadcrumbs
          v-if="breadcrumbs.length"
          class="text-grey-7"
          active-color="primary"
          gutter="xs"
        >
          <q-breadcrumbs-el
            v-for="(crumb, i) in breadcrumbs"
            :key="i"
            :label="crumb.label"
            :icon="crumb.icon"
            :to="crumb.to"
          />
        </q-breadcrumbs>
      </div>

      <slot name="actions" />

      <div v-if="(title && !breadcrumbs.length) || status" class="q-ml-sm">
        <span v-if="title && !breadcrumbs.length" class="app-page-title">{{ title }}</span>
        <q-badge v-if="status" :color="statusColor" :label="status" class="q-px-sm" />
      </div>

      <slot name="back">
        <q-btn
          v-if="showBack"
          outline
          color="primary"
          icon="o_arrow_back"
          label="Back"
          no-caps
          @click="$emit('back')"
        />
      </slot>
    </div>
  </q-card>
</template>

<script setup>
/*
 * AppDetailHeader (WO-94 Step 10; standardized): the record-detail header — breadcrumb +
 * title + optional status badge on the left, actions + a right-aligned outlined Back button
 * on the right, in the same bordered toolbar as AppListHeader for a consistent portal look.
 */
defineProps({
  title: { type: String, default: '' },
  subtitle: { type: String, default: '' },
  breadcrumbs: { type: Array, default: () => [] },
  status: { type: String, default: '' },
  statusColor: { type: String, default: 'primary' },
  showBack: { type: Boolean, default: true }
})

defineEmits(['back'])
</script>
