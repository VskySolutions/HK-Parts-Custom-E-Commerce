<template>
  <div class="sf-carousel">
    <q-btn
      v-if="showArrows"
      round
      dense
      unelevated
      class="sf-carousel__arrow sf-carousel__arrow--prev"
      icon="o_chevron_left"
      color="white"
      text-color="dark"
      @click="scrollBy(-1)"
    />
    <div ref="track" class="sf-carousel__track">
      <div v-for="p in products" :key="p.id" class="sf-carousel__cell">
        <StorefrontProductCard :product="p" />
      </div>
    </div>
    <q-btn
      v-if="showArrows"
      round
      dense
      unelevated
      class="sf-carousel__arrow sf-carousel__arrow--next"
      icon="o_chevron_right"
      color="white"
      text-color="dark"
      @click="scrollBy(1)"
    />
  </div>
</template>

<script setup>
/*
 * Porto product rail (WO-110/WO-111): a horizontal scroll-snap carousel of
 * StorefrontProductCard — 4 per row on desktop, 2 on tablet, ~1.5 on mobile
 * (CSS-driven), with prev/next arrows on wider viewports.
 */
import { ref, computed } from 'vue'
import { useQuasar } from 'quasar'
import StorefrontProductCard from 'modules/storefront/components/StorefrontProductCard.vue'

const props = defineProps({
  products: { type: Array, default: () => [] }
})

const $q = useQuasar()
const track = ref(null)

const showArrows = computed(() => $q.screen.gt.sm && props.products.length > 4)

function scrollBy (dir) {
  if (!track.value) return
  const amount = track.value.clientWidth * 0.8 * dir
  track.value.scrollBy({ left: amount, behavior: 'smooth' })
}
</script>

<style scoped lang="scss">
.sf-carousel {
  position: relative;
}
.sf-carousel__track {
  display: flex;
  gap: 20px;
  overflow-x: auto;
  scroll-snap-type: x mandatory;
  scroll-behavior: smooth;
  padding-bottom: 8px;
  -ms-overflow-style: none;
  scrollbar-width: none;
}
.sf-carousel__track::-webkit-scrollbar { display: none; }

.sf-carousel__cell {
  flex: 0 0 calc((100% - 60px) / 4);
  scroll-snap-align: start;
}
@media (max-width: 1023px) {
  .sf-carousel__cell { flex-basis: calc((100% - 20px) / 2); }
}
@media (max-width: 599px) {
  .sf-carousel__cell { flex-basis: 68%; }
}

.sf-carousel__arrow {
  position: absolute;
  top: 38%;
  z-index: 4;
  box-shadow: 0 2px 10px rgba(0, 0, 0, 0.18);
}
.sf-carousel__arrow--prev { left: -14px; }
.sf-carousel__arrow--next { right: -14px; }
</style>
