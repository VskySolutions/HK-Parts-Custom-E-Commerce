<template>
  <div class="relative-position">
    <q-input v-model="filter" dense outlined debounce="400" placeholder="Search eBay categories" clearable class="q-mb-sm">
      <template #prepend><q-icon name="o_search" /></template>
    </q-input>

    <q-banner v-if="!loading && !roots.length && !filter" class="bg-grey-2 rounded-borders">
      No eBay categories loaded yet. Use <b>Refresh eBay Categories</b> in eBay Settings.
    </q-banner>

    <!-- Search: a flat list with breadcrumbs. Only leaves are selectable. -->
    <q-list v-if="filter" bordered separator class="rounded-borders tree-box">
      <q-item v-if="!loading && !results.length">
        <q-item-section class="text-grey-7">No matching categories</q-item-section>
      </q-item>
      <q-item
        v-for="c in results"
        :key="c.categoryId"
        :clickable="c.isLeaf"
        :active="c.categoryId === modelValue"
        active-class="bg-blue-1 text-primary"
        @click="c.isLeaf && pick(c)"
      >
        <q-item-section>
          <q-item-label :class="c.isLeaf ? '' : 'text-grey-6'">{{ c.name }}</q-item-label>
          <q-item-label caption>{{ c.path }} · #{{ c.categoryId }}</q-item-label>
        </q-item-section>
        <q-item-section side>
          <q-badge v-if="c.isLeaf" outline color="positive" label="Leaf" />
          <span v-else class="text-caption text-grey-6">has sub-categories</span>
        </q-item-section>
      </q-item>
    </q-list>

    <!-- Browse: lazy tree. Only leaves are selectable. -->
    <div v-else-if="roots.length" class="tree-box rounded-borders">
      <q-tree
        v-model:selected="selectedKey"
        :nodes="roots"
        node-key="categoryId"
        label-key="name"
        children-key="children"
        dense
        @lazy-load="onLazyLoad"
      >
        <template #default-header="{ node }">
          <span :class="node.isLeaf ? 'text-weight-medium' : 'text-grey-8'">{{ node.name }}</span>
          <q-badge v-if="node.isLeaf" outline color="positive" label="Leaf" class="q-ml-sm" />
        </template>
      </q-tree>
    </div>

    <q-inner-loading :showing="loading" />
  </div>
</template>

<script setup>
/*
 * Lazy eBay category picker. Browsing loads children on expand; typing searches the whole tree on the
 * server. eBay lists only in leaf categories, so non-leaf nodes are not selectable. v-model is the category
 * id; `select` also emits the category object (for its name).
 */
import { ref, watch, onMounted } from 'vue'
import { ebayApi } from 'modules/ebay/api'
import { useNotify } from 'composables/useNotify'
import { getApiErrorMessage } from 'services/api'

const props = defineProps({ modelValue: { type: String, default: null } })
const emit = defineEmits(['update:modelValue', 'select'])
const notify = useNotify()

const roots = ref([])
const results = ref([])
const filter = ref('')
const loading = ref(false)
const selectedKey = ref(props.modelValue)
const known = new Map()

function toNode (c) {
  const node = { ...c, lazy: !c.isLeaf, selectable: c.isLeaf, icon: c.isLeaf ? 'o_label' : 'o_folder' }
  known.set(c.categoryId, node)
  return node
}

function pick (category) {
  selectedKey.value = category.categoryId
  emit('update:modelValue', category.categoryId)
  emit('select', category)
}

watch(selectedKey, (key) => {
  if (key && key !== props.modelValue && known.get(key)?.isLeaf) pick(known.get(key))
})
watch(() => props.modelValue, (v) => { selectedKey.value = v })

async function onLazyLoad ({ node, done, fail }) {
  try {
    done((await ebayApi.categories({ parentId: node.categoryId })).map(toNode))
  } catch (err) {
    notify.error(getApiErrorMessage(err))
    fail()
  }
}

watch(filter, async (term) => {
  if (!term) { results.value = []; return }
  loading.value = true
  try {
    results.value = await ebayApi.categories({ search: term })
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    loading.value = false
  }
})

onMounted(async () => {
  loading.value = true
  try {
    roots.value = (await ebayApi.categories()).map(toNode)
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    loading.value = false
  }
})
</script>

<style scoped lang="scss">
.tree-box {
  border: 1px solid rgba(0, 0, 0, 0.12);
  max-height: 420px;
  overflow: auto;
  padding: 4px 8px;
}
</style>
