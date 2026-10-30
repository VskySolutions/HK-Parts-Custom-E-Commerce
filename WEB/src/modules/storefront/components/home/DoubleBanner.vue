<template>
  <div class="sf-section">
    <div class="sf-container">
      <div class="row q-col-gutter-md">
        <div v-for="(b, i) in banners" :key="i" class="col-12 col-sm-6">
          <router-link :to="b.to" class="sf-promo" :style="{ background: b.background }">
            <div class="sf-promo__body">
              <div class="sf-promo__label">{{ b.label }}</div>
              <div class="sf-promo__title">{{ b.title }}</div>
              <span class="sf-promo__cta">{{ b.cta }} <q-icon name="o_arrow_forward" size="16px" /></span>
            </div>
          </router-link>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
/*
 * Porto double promotional banner (WO-110). The Banner system (display-location
 * "home-double-banner") does not exist yet (flagged on WO-110), so these render
 * branded default promos linking to the storefront; feed real banners via `banners`.
 */
import { computed } from 'vue'

const props = defineProps({ banners: { type: Array, default: null } })

const defaults = [
  {
    label: 'Limited time',
    title: 'New Season Arrivals',
    cta: 'Shop new',
    to: { name: 'shop-search', query: { sort: 'newest' } },
    background: 'linear-gradient(120deg, #f5f6fb, #e9ebf6)'
  },
  {
    label: 'Best value',
    title: 'Deals Under Budget',
    cta: 'Browse deals',
    to: { name: 'shop-search', query: { sort: 'price_asc' } },
    background: 'linear-gradient(120deg, #fbf4f2, #f6e9e6)'
  }
]

const banners = computed(() => (props.banners && props.banners.length ? props.banners : defaults))
</script>

<style scoped lang="scss">
.sf-promo {
  display: block;
  position: relative;
  min-height: 200px;
  border-radius: var(--sf-radius);
  overflow: hidden;
  text-decoration: none;
  color: var(--sf-heading);
  border: 1px solid var(--sf-border);
  transition: transform var(--sf-transition), box-shadow var(--sf-transition), border-color var(--sf-transition);
}
.sf-promo:hover { transform: translateY(-3px); box-shadow: var(--sf-shadow-hover); border-color: var(--sf-accent); }
.sf-promo__body { padding: 32px; }
.sf-promo__label { text-transform: uppercase; letter-spacing: 1.5px; font-size: 12px; color: var(--sf-muted); }
.sf-promo__title { font-size: 26px; font-weight: 800; margin: 8px 0 16px; }
.sf-promo__cta { font-weight: 600; text-transform: uppercase; letter-spacing: 0.5px; font-size: 13px; color: var(--sf-accent); display: inline-flex; align-items: center; gap: 6px; }
</style>
