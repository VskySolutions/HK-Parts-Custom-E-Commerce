<template>
  <q-select
    ref="selectRef"
    v-model="model"
    dense
    outlined
    use-input
    clearable
    input-debounce="300"
    :label="label"
    :placeholder="placeholder"
    :options="options"
    option-label="name"
    option-value="id"
    :loading="loading"
    :disable="disable"
    hide-dropdown-icon
    @filter="onFilter"
    @update:model-value="onPick"
  >
    <template #prepend><q-icon name="o_search" /></template>

    <template #option="scope">
      <q-item v-bind="scope.itemProps">
        <q-item-section v-if="showThumb" avatar>
          <q-avatar rounded size="34px" color="grey-2" text-color="grey-6">
            <img v-if="scope.opt.imageUrl" :src="$media(scope.opt.imageUrl)" :alt="scope.opt.name">
            <q-icon v-else name="o_image" size="18px" />
          </q-avatar>
        </q-item-section>
        <q-item-section>
          <q-item-label lines="1">{{ scope.opt.name }}</q-item-label>
          <q-item-label v-if="scope.opt.sku" caption>SKU: {{ scope.opt.sku }}</q-item-label>
        </q-item-section>
      </q-item>
    </template>

    <template #no-option>
      <q-item>
        <q-item-section class="text-grey-6">
          {{ loading ? 'Searching…' : 'Type to search products by name or SKU' }}
        </q-item-section>
      </q-item>
    </template>
  </q-select>
</template>

<script setup>
/*
 * ProductPicker — a lightweight search-as-you-type product selector reused across the CMS content
 * managers (collection items, featured products, pinned products). The app ships no shared
 * product-picker, so this wraps a q-select in `use-input` + `@filter` mode over
 * GET /api/admin/products (?search=). It intentionally never holds a value: on pick it emits
 * `select(product)` and immediately clears, so the same field adds many products in a row. Products
 * already added are hidden via `exclude-ids`.
 */
import { ref, nextTick } from 'vue'
import { productApi } from 'modules/catalog/api'

const props = defineProps({
  label: { type: String, default: '' },
  placeholder: { type: String, default: 'Search products by name or SKU…' },
  disable: { type: Boolean, default: false },
  excludeIds: { type: Array, default: () => [] },
  showThumb: { type: Boolean, default: true }
})

const emit = defineEmits(['select'])

const selectRef = ref(null)
const model = ref(null)
const options = ref([])
const loading = ref(false)

async function onFilter (val, update) {
  loading.value = true
  try {
    const result = await productApi.list({ search: val || undefined, page: 1, pageSize: 20 })
    const items = Array.isArray(result) ? result : result?.items || result?.data || []
    update(() => {
      const exclude = new Set(props.excludeIds)
      options.value = items.filter((p) => !exclude.has(p.id))
    })
  } catch {
    update(() => { options.value = [] })
  } finally {
    loading.value = false
  }
}

function onPick (val) {
  if (val) emit('select', val)
  // Never keep a selection — clear the model and the typed text so the next add starts fresh.
  nextTick(() => {
    model.value = null
    if (selectRef.value && typeof selectRef.value.updateInputValue === 'function') {
      selectRef.value.updateInputValue('', false)
    }
  })
}
</script>
