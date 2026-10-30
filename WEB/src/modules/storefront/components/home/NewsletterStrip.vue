<template>
  <div class="sf-newsletter">
    <div class="sf-container q-py-xl">
      <div class="row items-center q-col-gutter-lg">
        <div class="col-12 col-md-6">
          <div class="text-h5 text-weight-bold">Join our newsletter</div>
          <div class="text-body1 q-mt-xs" style="opacity: 0.85">
            Subscribe for new arrivals, exclusive offers and more.
          </div>
        </div>
        <div class="col-12 col-md-6">
          <NewsletterForm />
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
/* Porto newsletter strip (WO-110): full-width dark section wrapping the shared NewsletterForm. */
import NewsletterForm from 'modules/storefront/components/NewsletterForm.vue'
</script>
