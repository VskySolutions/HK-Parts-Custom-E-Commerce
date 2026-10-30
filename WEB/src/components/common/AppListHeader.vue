<template>
  <q-card flat bordered class="app-toolbar q-px-md q-py-sm q-mb-md">
    <div class="row items-center q-gutter-sm">
      <div class="col">
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
        <div v-if="title && !breadcrumbs.length" class="app-page-title">{{ title }}</div>
      </div>

      <slot name="actions" />

      <slot name="add">
        <q-btn
          v-if="showAdd"
          unelevated
          color="primary"
          icon="o_add"
          :label="addLabel"
          no-caps
          @click="$emit('add')"
        />
      </slot>

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
 * AppListHeader (WO-94 Step 10; standardized): a bordered toolbar bar carrying the
 * breadcrumb + page title on the left and the action cluster (search/filters via the
 * `actions` slot, a primary add button, and a right-aligned outlined Back button) on
 * the right — the portal-wide list-page header standard.
 */
defineProps({
  title: { type: String, default: '' },
  subtitle: { type: String, default: '' },
  breadcrumbs: { type: Array, default: () => [] },
  showAdd: { type: Boolean, default: false },
  addLabel: { type: String, default: 'Add' },
  showBack: { type: Boolean, default: false }
})

defineEmits(['add', 'back'])
</script>
