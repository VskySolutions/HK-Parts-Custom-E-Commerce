<template>
  <q-card flat bordered class="stat-card">
    <q-card-section class="row items-center no-wrap q-gutter-sm">
      <div class="col">
        <div class="stat-card__label">{{ label }}</div>
        <div class="stat-card__value">
          <q-skeleton v-if="loading" type="text" width="90px" />
          <template v-else>{{ display }}</template>
        </div>
      </div>
      <div v-if="icon" class="stat-card__icon">
        <q-icon :name="icon" size="24px" />
      </div>
    </q-card-section>
  </q-card>
</template>

<script setup>
/*
 * StatCard (WO-59): a single KPI tile — a big number/value with a muted label and an
 * optional accent icon. Shows a skeleton while loading and an em dash for empty values.
 */
import { computed } from 'vue'

const props = defineProps({
  label: { type: String, default: '' },
  value: { type: [String, Number], default: null },
  icon: { type: String, default: '' },
  loading: { type: Boolean, default: false }
})

const display = computed(() =>
  props.value === null || props.value === undefined || props.value === '' ? '—' : props.value
)
</script>

<style scoped lang="scss">
.stat-card {
  border-radius: 10px;
  height: 100%;
}
.stat-card__label {
  font-size: 12px;
  font-weight: 500;
  color: #6b6c76;
  text-transform: uppercase;
  letter-spacing: 0.3px;
}
.stat-card__value {
  font-size: 26px;
  font-weight: 700;
  line-height: 1.2;
  color: #1d1d1f;
  margin-top: 6px;
}
.stat-card__icon {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 42px;
  height: 42px;
  border-radius: 10px;
  color: var(--q-primary);
  background: rgba(25, 118, 210, 0.08);
}
</style>
