<template>
  <q-page class="q-pa-md storefront-container">
    <div class="row items-center q-mb-md">
      <div class="text-h5 text-weight-bold col">Compare products</div>
      <q-btn
        v-if="products.length"
        flat
        no-caps
        dense
        color="negative"
        icon="o_delete_sweep"
        label="Clear all"
        @click="clearAll"
      />
    </div>

    <q-inner-loading :showing="loading" color="primary" />

    <!-- Empty state -->
    <div v-if="!loading && !products.length" class="column flex-center q-py-xl text-center text-grey-6">
      <q-icon name="o_compare" size="64px" class="q-mb-md" />
      <div class="text-h6 text-grey-8">Nothing to compare yet</div>
      <div class="q-mt-xs">Add products to compare them side by side.</div>
      <q-btn flat no-caps color="primary" label="Browse products" class="q-mt-md" :to="{ name: 'shop-home' }" />
    </div>

    <!-- Comparison table -->
    <q-markup-table v-if="products.length" flat bordered separator="cell" class="compare-table">
      <thead>
        <tr>
          <th class="text-left compare-table__label">Product</th>
          <th v-for="p in products" :key="p.id" class="text-center compare-table__col">
            <div class="column items-center q-gutter-xs q-py-sm">
              <q-img
                v-if="p.primaryImageUrl"
                :src="$media(p.primaryImageUrl)"
                :alt="p.name"
                :ratio="1"
                fit="contain"
                width="120px"
                class="rounded-borders bg-grey-1"
              />
              <div v-else class="compare-table__noimg column flex-center bg-grey-1 rounded-borders text-grey-5">
                <q-icon name="o_image" size="32px" />
              </div>
              <router-link
                :to="{ name: 'shop-product', params: { idOrSlug: p.slug || p.id } }"
                class="text-primary text-weight-medium compare-table__name"
              >
                {{ p.name }}
              </router-link>
              <q-btn
                flat
                dense
                round
                size="sm"
                icon="o_close"
                color="negative"
                @click="remove(p.id)"
              >
                <q-tooltip>Remove</q-tooltip>
              </q-btn>
            </div>
          </th>
        </tr>
      </thead>
      <tbody>
        <tr>
          <td class="text-left text-weight-medium compare-table__label">Price</td>
          <td v-for="p in products" :key="p.id" class="text-center text-primary text-weight-bold">
            {{ formatPrice(p.price) }}
          </td>
        </tr>
        <tr v-for="attr in attributes" :key="attr.specificationAttributeId">
          <td class="text-left text-weight-medium compare-table__label">{{ attr.name }}</td>
          <td v-for="p in products" :key="p.id" class="text-center">
            {{ valueFor(p, attr.specificationAttributeId) }}
          </td>
        </tr>
      </tbody>
    </q-markup-table>
  </q-page>
</template>

<script setup>
/*
 * Storefront product comparison (WO-19, AC-STF-005.2/3). Reads the compare list
 * from the shared localStorage state, resolves it via the compare endpoint and
 * renders a side-by-side table: the union of specification attributes forms the
 * row headers; each product column shows its price and per-attribute values.
 * Products can be removed individually (the list re-resolves) or cleared.
 */
import { ref, watch, onMounted } from 'vue'
import { storefrontApi, formatPrice } from 'modules/storefront/api'
import { useCompare } from 'modules/storefront/composables/useStorefrontStorage'
import { useNotify } from 'composables/useNotify'
import { getApiErrorMessage } from 'services/api'

const notify = useNotify()
const { compareIds, remove: removeFromCompare, clear } = useCompare()

const attributes = ref([])
const products = ref([])
const loading = ref(false)

async function load () {
  if (!compareIds.value.length) {
    attributes.value = []
    products.value = []
    return
  }
  loading.value = true
  try {
    const res = await storefrontApi.compare(compareIds.value)
    attributes.value = Array.isArray(res?.attributes) ? res.attributes : []
    products.value = Array.isArray(res?.products) ? res.products : []
  } catch (err) {
    attributes.value = []
    products.value = []
    notify.error(getApiErrorMessage(err))
  } finally {
    loading.value = false
  }
}

function valueFor (product, attributeId) {
  const values = (product.specificationValues || [])
    .filter((s) => s.specificationAttributeId === attributeId)
    .map((s) => s.value)
  return values.length ? values.join(', ') : '—'
}

function remove (id) {
  removeFromCompare(id)
}

function clearAll () {
  clear()
}

// Re-resolve whenever the compare list changes (add elsewhere, remove, clear).
watch(compareIds, load)
onMounted(load)
</script>

<style scoped lang="scss">
.storefront-container {
  max-width: 1400px;
  margin: 0 auto;
}

.compare-table {
  &__label {
    min-width: 140px;
    white-space: nowrap;
  }

  &__col {
    min-width: 180px;
  }

  &__name {
    text-decoration: none;
    max-width: 160px;
  }

  &__noimg {
    width: 120px;
    height: 120px;
  }
}
</style>
