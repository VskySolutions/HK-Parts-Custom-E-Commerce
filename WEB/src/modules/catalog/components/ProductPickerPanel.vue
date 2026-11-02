<template>
  <div class="product-picker">
    <!-- Selected products -->
    <AppFieldLabel :label="`${selectedLabel} (${modelValue.length})`">
      <template v-if="selectedHint" #hint>{{ selectedHint }}</template>
    </AppFieldLabel>
    <q-list v-if="modelValue.length" bordered separator class="rounded-borders q-mb-md">
      <q-item v-for="id in modelValue" :key="id" dense>
        <q-item-section avatar>
          <q-avatar rounded size="36px" class="bg-grey-2">
            <img v-if="rowFor(id)?.imageUrl" :src="$media(rowFor(id).imageUrl)" alt="">
            <q-icon v-else name="o_image" color="grey-5" />
          </q-avatar>
        </q-item-section>
        <q-item-section>
          <q-item-label>{{ rowFor(id)?.name || (resolving ? 'Loading…' : 'Unknown product') }}</q-item-label>
          <q-item-label caption>{{ rowFor(id)?.sku || '—' }}</q-item-label>
        </q-item-section>
        <q-item-section side>
          <q-btn v-if="!disable" flat round dense icon="o_close" color="negative" @click="toggle(id, false)">
            <q-tooltip>Remove</q-tooltip>
          </q-btn>
        </q-item-section>
      </q-item>
    </q-list>
    <div v-else class="text-caption text-grey-6 q-mb-md">{{ emptyText }}</div>

    <!-- Search + tick to add -->
    <template v-if="!disable">
      <q-input
        v-model="term"
        dense
        outlined
        debounce="400"
        :placeholder="searchPlaceholder"
        :loading="searching"
        clearable
        @update:model-value="search"
      >
        <template #prepend><q-icon name="o_search" /></template>
      </q-input>

      <q-markup-table v-if="results.length" flat bordered dense class="q-mt-sm">
        <thead>
          <tr>
            <th style="width: 40px"></th>
            <th class="text-left" style="width: 52px"></th>
            <th class="text-left">Name</th>
            <th class="text-left">SKU</th>
            <th class="text-right">Qty in stock</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="row in results" :key="row.id" class="cursor-pointer" @click="toggle(row.id, !isSelected(row.id))">
            <td><q-checkbox :model-value="isSelected(row.id)" dense @update:model-value="(v) => toggle(row.id, v)" @click.stop /></td>
            <td>
              <q-avatar rounded size="32px" class="bg-grey-2">
                <img v-if="row.imageUrl" :src="$media(row.imageUrl)" alt="">
                <q-icon v-else name="o_image" color="grey-5" size="18px" />
              </q-avatar>
            </td>
            <td class="text-left">{{ row.name }}</td>
            <td class="text-left">{{ row.sku || '—' }}</td>
            <td class="text-right">{{ row.stockQuantity }}</td>
          </tr>
        </tbody>
      </q-markup-table>
      <div v-else-if="term && !searching" class="text-caption text-grey-6 q-mt-sm">No products match “{{ term }}”.</div>
      <div v-if="hasMore" class="text-caption text-grey-6 q-mt-xs">Showing the first {{ results.length }} matches — refine the search to narrow it down.</div>
    </template>
  </div>
</template>

<script setup>
/*
 * Reusable "search products by SKU / name, tick to select" picker (PHP-parity Related Products tab;
 * also drives a Grouped product's sub products). v-model is an ordered array of product ids — the
 * parent persists it (replace-semantics endpoints). Selected rows are resolved through the admin list
 * endpoint's `ids` filter so names/SKUs/images show without loading full products.
 */
import { ref, reactive, watch } from 'vue'
import { productApi } from 'modules/catalog/api'
import { getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'
import AppFieldLabel from 'components/common/AppFieldLabel.vue'

const props = defineProps({
  modelValue: { type: Array, default: () => [] },
  // The product being edited — never offered as a choice.
  excludeId: { type: String, default: null },
  // Restrict search results (e.g. a Grouped product's members can't themselves be Grouped).
  excludeTypes: { type: Array, default: () => [] },
  selectedLabel: { type: String, default: 'Selected products' },
  selectedHint: { type: String, default: '' },
  emptyText: { type: String, default: 'Nothing selected yet — search below and tick products to add them.' },
  searchPlaceholder: { type: String, default: 'Search product by SKU / name' },
  disable: { type: Boolean, default: false }
})
const emit = defineEmits(['update:modelValue'])
const notify = useNotify()

const PAGE = 25
const term = ref('')
const results = ref([])
const hasMore = ref(false)
const searching = ref(false)
const resolving = ref(false)
const cache = reactive({}) // id -> ProductListItemDto

const rowFor = (id) => cache[id]
const isSelected = (id) => props.modelValue.includes(id)

function remember (rows) {
  for (const r of rows) cache[r.id] = r
}

function toggle (id, on) {
  if (props.disable) return
  const next = props.modelValue.filter((x) => x !== id)
  if (on) next.push(id)
  emit('update:modelValue', next)
}

async function search () {
  const q = (term.value || '').trim()
  if (!q) { results.value = []; hasMore.value = false; return }
  searching.value = true
  try {
    const page = await productApi.list({ page: 1, pageSize: PAGE, search: q, excludeId: props.excludeId || undefined })
    const items = (page?.items || []).filter((r) => !props.excludeTypes.includes(r.productType))
    remember(items)
    results.value = items
    hasMore.value = (page?.totalCount || 0) > PAGE
  } catch (err) {
    results.value = []
    notify.error(getApiErrorMessage(err))
  } finally {
    searching.value = false
  }
}

// Resolve any selected id we haven't seen yet (initial load, or ids set by the parent).
async function resolveMissing (ids) {
  const missing = ids.filter((id) => !cache[id])
  if (!missing.length) return
  resolving.value = true
  try {
    const page = await productApi.list({ page: 1, pageSize: Math.max(missing.length, 1), ids: missing })
    remember(page?.items || [])
  } catch {
    // Names just show as "Unknown product"; the selection itself is unaffected.
  } finally {
    resolving.value = false
  }
}

watch(() => props.modelValue, (ids) => resolveMissing(ids || []), { immediate: true })
</script>
