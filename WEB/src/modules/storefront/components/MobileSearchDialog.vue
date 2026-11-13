
<template>
  <q-dialog
    :model-value="modelValue"
    position="top"
    transition-show="fade"
    transition-hide="fade"
    @update:model-value="emit('update:modelValue', $event)"
  >
    <q-card class="mobile-search-dialog">
      <!-- Header -->
      <q-card-section class="row items-center no-wrap q-pa-sm bg-26">
        <div class="text-white">
          Search Products
        </div>

        <q-space />

        <q-btn
          flat
          round
          dense
          icon="o_close"
          aria-label="Close search"
          @click="emit('update:modelValue', false)"
          color="white"
          size="10px"
        />
      </q-card-section>

      <!-- Search Input -->
      <q-card-section>
        <q-input
          :model-value="searchText"
          autofocus
          outlined
          dense
          clearable
          placeholder="Search products..."
          debounce="300"
          class="mobile-search-dialog__input"
          @update:model-value="onInput"
          @keyup.enter="emit('search', searchText)"
        >
          <template #prepend>
            <q-icon name="o_search" />
          </template>

          <template #append>
            <q-btn
              flat
              round
              dense
              icon="o_arrow_forward"
              color="primary"
              aria-label="Submit search"
              @click="emit('search', searchText)"
            />
          </template>
        </q-input>
      </q-card-section>

      <!-- Suggestions -->
      <q-card-section
        v-if="suggestOpen"
        class="q-pt-none"
      >
        <q-list separator>
          <template v-if="suggestions.products.length">
            <q-item-label header>
              Products
            </q-item-label>

            <q-item
              v-for="(name, index) in suggestions.products"
              :key="'product-' + index"
              clickable
              v-ripple
              @click="emit('select', name)"
              class="q-mb-md"
            >
              <q-item-section avatar class="text-grey-6">
                <q-icon name="o_inventory_2" />
              </q-item-section>

              <q-item-section class="text-grey-10">
                {{ name }}
              </q-item-section>
            </q-item>
          </template>

          <template v-if="suggestions.categories.length">
            <q-item-label header>
              Categories
            </q-item-label>

            <q-item
              v-for="(name, index) in suggestions.categories"
              :key="'category-' + index"
              clickable
              v-ripple
              @click="emit('select', name)"
            >
              <q-item-section avatar class="text-grey-6">
                <q-icon name="o_category" />
              </q-item-section>

              <q-item-section class="text-grey-10">
                {{ name }}
              </q-item-section>
            </q-item>
          </template>
        </q-list>
      </q-card-section>
    </q-card>
  </q-dialog>
</template>

<script setup>
const props = defineProps({
  modelValue: {
    type: Boolean,
    default: false
  },
  searchText: {
    type: String,
    default: ''
  },
  suggestions: {
    type: Object,
    default: () => ({
      products: [],
      categories: []
    })
  },
  suggestOpen: {
    type: Boolean,
    default: false
  }
})

const emit = defineEmits([
  'update:modelValue',
  'update:searchText',
  'query-change',
  'search',
  'select'
])

function onInput (value) {
  emit('update:searchText', value || '')
  emit('query-change', value)
}
</script>

<style lang="scss" scoped>
.mobile-search-dialog {
  width: 100%;
  max-width: 100%;
  border-radius: 0 0 18px 18px;
  background: #fff;
  box-shadow: 0 12px 35px rgba(0, 0, 0, 0.2);

  &__input {
    :deep(.q-field__control) {
      border-radius: 10px;
    }
  }
}

:deep(.q-dialog__backdrop) {
  background: rgba(0, 0, 0, 0.82);
  backdrop-filter: blur(5px);
}

.q-card__section--vert {
    padding: 16px;
}
.q-item__label--header {
    padding: 0px;
}
.q-item__section--side > .q-icon {
    font-size: 20px;
}
.q-item__section--avatar {
    min-width: 0px;
}
.q-item__section--side {
    padding-right: 10px;
}
</style>
