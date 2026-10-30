<template>
  <div class="filter-panel">
    <div class="row items-center q-mb-sm">
      <div class="text-subtitle1 col">Filters</div>
      <q-btn
        v-if="hasActiveFilters"
        flat
        dense
        no-caps
        size="sm"
        color="primary"
        label="Clear all"
        @click="clearAll"
      />
    </div>

    <!-- Price range (search page) -->
    <template v-if="showPrice">
      <div class="text-caption text-grey-7 q-mb-xs">Price</div>
      <div class="row q-col-gutter-sm q-mb-md">
        <div class="col-6">
          <q-input
            :model-value="minPrice"
            dense
            outlined
            type="number"
            min="0"
            label="Min"
            debounce="500"
            @update:model-value="(v) => emit('update:minPrice', numberOrNull(v))"
          />
        </div>
        <div class="col-6">
          <q-input
            :model-value="maxPrice"
            dense
            outlined
            type="number"
            min="0"
            label="Max"
            debounce="500"
            @update:model-value="(v) => emit('update:maxPrice', numberOrNull(v))"
          />
        </div>
      </div>
    </template>

    <!-- Manufacturer (only when the caller supplies a list) -->
    <template v-if="manufacturers.length">
      <div class="text-caption text-grey-7 q-mb-xs">Manufacturer</div>
      <q-select
        :model-value="manufacturerId"
        dense
        outlined
        clearable
        emit-value
        map-options
        :options="manufacturers"
        option-value="id"
        option-label="name"
        label="Any"
        class="q-mb-md"
        @update:model-value="(v) => emit('update:manufacturerId', v || null)"
      />
    </template>

    <div v-if="!facets.length && !showPrice && !manufacturers.length" class="text-grey-6 text-caption">
      No filters available.
    </div>

    <q-list>
      <q-expansion-item
        v-for="group in facets"
        :key="group.attributeId"
        :label="group.name"
        default-opened
        dense
        header-class="text-weight-medium q-px-none"
      >
        <div class="q-pl-sm">
          <q-item
            v-for="opt in group.options"
            :key="opt.optionId"
            dense
            class="q-px-none"
          >
            <q-item-section>
              <q-checkbox
                :model-value="selected.includes(opt.optionId)"
                :label="opt.value"
                dense
                @update:model-value="() => toggleOption(opt.optionId)"
              />
            </q-item-section>
            <q-item-section v-if="opt.count != null" side>
              <span class="text-caption text-grey-6">{{ opt.count }}</span>
            </q-item-section>
          </q-item>
        </div>
      </q-expansion-item>
    </q-list>
  </div>
</template>

<script setup>
/*
 * Faceted filter panel (WO-19, AC-STF-003). Renders normalised spec-attribute
 * facet groups (see normalizeFacets in the module api) as value checkboxes,
 * emitting the selected option-id array via v-model. Optional price-range inputs
 * (search) and a manufacturer select (when a list is provided) are surfaced too.
 * Selecting any option emits immediately so the parent can re-query.
 */
import { computed } from 'vue'

const props = defineProps({
  // normalized: [{ attributeId, name, options:[{ optionId, value, count|null }] }]
  facets: { type: Array, default: () => [] },
  // selected specification option ids
  modelValue: { type: Array, default: () => [] },
  showPrice: { type: Boolean, default: false },
  minPrice: { type: [Number, String, null], default: null },
  maxPrice: { type: [Number, String, null], default: null },
  manufacturers: { type: Array, default: () => [] },
  manufacturerId: { type: [String, null], default: null }
})

const emit = defineEmits([
  'update:modelValue',
  'update:minPrice',
  'update:maxPrice',
  'update:manufacturerId',
  'clear'
])

const selected = computed(() => props.modelValue || [])

const hasActiveFilters = computed(
  () =>
    selected.value.length > 0 ||
    props.minPrice != null ||
    props.maxPrice != null ||
    props.manufacturerId != null
)

function toggleOption (optionId) {
  const set = new Set(selected.value)
  if (set.has(optionId)) set.delete(optionId)
  else set.add(optionId)
  emit('update:modelValue', Array.from(set))
}

function numberOrNull (v) {
  if (v === '' || v === null || v === undefined) return null
  const n = Number(v)
  return Number.isNaN(n) ? null : n
}

function clearAll () {
  emit('update:modelValue', [])
  emit('update:minPrice', null)
  emit('update:maxPrice', null)
  emit('update:manufacturerId', null)
  emit('clear')
}
</script>
