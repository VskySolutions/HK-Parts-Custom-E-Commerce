<template>
  <q-card
    flat
    bordered
    class="product-card column no-wrap full-height cursor-pointer"
    @click="goToProduct"
  >
    <div class="product-card__media bg-grey-1">
      <q-img
        v-if="image"
        :src="$media(image)"
        :alt="product.name"
        :ratio="1"
        fit="contain"
        loading="lazy"
        no-spinner
      />
      <div v-else class="product-card__placeholder column flex-center text-grey-5">
        <q-icon name="o_image" size="48px" />
      </div>

      <q-btn
        v-if="showCompare"
        round
        dense
        unelevated
        size="sm"
        class="product-card__compare absolute-top-right q-ma-sm"
        :color="inCompare ? 'primary' : 'white'"
        :text-color="inCompare ? 'white' : 'grey-8'"
        :icon="inCompare ? 'o_compare_arrows' : 'o_compare'"
        @click.stop="onToggleCompare"
      >
        <q-tooltip>{{ inCompare ? 'Remove from compare' : 'Add to compare' }}</q-tooltip>
      </q-btn>
    </div>

    <q-card-section class="col column no-wrap q-pb-sm">
      <div class="text-body2 text-weight-medium product-card__name">{{ product.name }}</div>
      <div v-if="product.shortDescription" class="text-caption text-grey-7 product-card__desc q-mt-xs">
        {{ product.shortDescription }}
      </div>
      <div class="col" />
      <div class="text-subtitle1 text-weight-bold text-primary q-mt-sm">
        {{ formatPrice(product.price) }}
      </div>
    </q-card-section>
  </q-card>
</template>

<script setup>
/*
 * Storefront product card (WO-19): image, name, price and a link to the product
 * detail route. Tolerates either the summary DTO (primaryImageUrl) or the search
 * DTO (imageUrl). An optional "add to compare" affordance toggles the shared
 * compare list.
 */
import { computed } from 'vue'
import { useRouter } from 'vue-router'
import { formatPrice, productImage, productRouteParam } from 'modules/storefront/api'
import { useCompare } from 'modules/storefront/composables/useStorefrontStorage'
import { useNotify } from 'composables/useNotify'

const props = defineProps({
  product: { type: Object, required: true },
  showCompare: { type: Boolean, default: true }
})

const router = useRouter()
const notify = useNotify()
const { has, toggle, max } = useCompare()

const image = computed(() => productImage(props.product))
const inCompare = computed(() => has(props.product.id))

function goToProduct () {
  router.push({ name: 'shop-product', params: { idOrSlug: productRouteParam(props.product) } })
}

function onToggleCompare () {
  const result = toggle(props.product.id)
  if (result.full) {
    notify.warning('You can compare up to ' + max + ' products.')
  } else if (result.removed) {
    notify.info('Removed from compare')
  } else if (result.ok) {
    notify.success('Added to compare')
  }
}
</script>

<style scoped lang="scss">
.product-card {
  transition: box-shadow 0.2s ease, transform 0.2s ease;

  &:hover {
    box-shadow: 0 4px 14px rgba(0, 0, 0, 0.12);
  }
}

.product-card__media {
  position: relative;
}

.product-card__placeholder {
  aspect-ratio: 1 / 1;
}

.product-card__name {
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.product-card__desc {
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}
</style>
