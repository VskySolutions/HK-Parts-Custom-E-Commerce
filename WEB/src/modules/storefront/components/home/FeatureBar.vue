<template>
  <div class="sf-featurebar">
    <div class="sf-container">
      <div class="row sf-featurebar__row">
        <div v-for="f in features" :key="f.title" class="col-12 col-sm-6 col-md-3 sf-featurebar__box">
          <q-icon :name="f.icon" size="34px" class="sf-featurebar__icon" />
          <div>
            <div class="sf-featurebar__title">{{ f.title }}</div>
            <div class="sf-featurebar__sub">{{ f.sub }}</div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
/* Porto feature bar (WO-110): 4 trust icon boxes → 2×2 on tablet → stacked on mobile. */
const features = [
  { icon: 'o_local_shipping', title: 'Free Shipping', sub: 'On orders over the threshold' },
  { icon: 'o_support_agent', title: '24/7 Support', sub: 'Dedicated help, any time' },
  { icon: 'o_cached', title: 'Easy Returns', sub: 'Hassle-free within 30 days' },
  { icon: 'o_lock', title: 'Secure Payment', sub: 'Encrypted & protected' }
]
</script>

<style scoped lang="scss">
.sf-featurebar {
  background: var(--sf-surface-alt);
  border-top: 1px solid var(--sf-border);
  border-bottom: 1px solid var(--sf-border);
}
.sf-featurebar__box {
  display: flex;
  align-items: center;
  gap: 14px;
  padding: 22px 20px;
}
.sf-featurebar__box:not(:last-child) { border-right: 1px solid var(--sf-border); }
@media (max-width: 1023px) {
  .sf-featurebar__box:nth-child(2) { border-right: none; }
}
@media (max-width: 599px) {
  .sf-featurebar__box { border-right: none !important; border-bottom: 1px solid var(--sf-border); }
}
.sf-featurebar__icon { color: var(--sf-accent); flex: 0 0 auto; }
.sf-featurebar__title { font-weight: 700; color: var(--sf-heading); font-size: 15px; }
.sf-featurebar__sub { font-size: 12.5px; color: var(--sf-muted); }
</style>
