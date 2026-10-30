<template>
  <div v-if="!hidden" class="sf-reviews">
    <!-- Summary + write action -->
    <div class="row items-start q-col-gutter-lg q-mb-lg">
      <div class="col-12 col-sm-auto text-center sf-reviews__score">
        <div class="sf-reviews__avg">{{ averageRating.toFixed(1) }}</div>
        <StarRating :value="averageRating" :show-count="false" />
        <div class="text-caption text-grey-6 q-mt-xs">
          {{ totalCount }} review{{ totalCount === 1 ? '' : 's' }}
        </div>
      </div>

      <div class="col">
        <div
          v-for="row in distribution"
          :key="row.star"
          class="row items-center no-wrap q-gutter-sm sf-reviews__bar-row"
        >
          <span class="text-caption text-grey-7 sf-reviews__bar-label">{{ row.star }} star</span>
          <q-linear-progress
            :value="row.ratio"
            color="amber"
            track-color="grey-3"
            size="8px"
            rounded
            class="col"
          />
          <span class="text-caption text-grey-6 sf-reviews__bar-count">{{ row.count }}</span>
        </div>
      </div>

      <div v-if="isAuthenticated && enabled" class="col-12 col-sm-auto column items-start">
        <q-btn
          color="primary"
          no-caps
          unelevated
          icon="o_rate_review"
          label="Write a Review"
          :disable="!canReview"
          :loading="eligibilityLoading"
          @click="showForm = !showForm"
        />
        <div v-if="showEligibilityNote" class="text-caption text-grey-6 q-mt-xs sf-reviews__note">
          <q-icon name="o_info" size="14px" class="q-mr-xs" />{{ eligibilityMessage }}
        </div>
      </div>
    </div>

    <!-- Signed-out hint -->
    <q-banner v-if="!isAuthenticated" dense rounded class="bg-grey-2 text-body2 q-mb-md">
      <template #avatar><q-icon name="o_lock" color="grey-7" /></template>
      Please
      <router-link class="text-primary" :to="loginTo">sign in</router-link>
      to write a review.
    </q-banner>

    <!-- Submit form -->
    <q-slide-transition>
      <q-card v-if="showForm && canReview" flat bordered class="q-mb-lg">
        <q-form @submit.prevent="submit">
          <q-card-section class="q-gutter-md">
            <div class="row items-center q-gutter-sm">
              <span class="text-body2">Your rating</span>
              <q-rating
                v-model="form.rating"
                size="26px"
                color="amber"
                icon="mdi-star-outline"
                icon-selected="mdi-star"
              />
            </div>
            <q-input
              v-model="form.title"
              dense
              outlined
              label="Title"
              maxlength="120"
              :rules="[(v) => !!(v && v.trim()) || 'Title is required']"
            />
            <q-input
              v-model="form.body"
              dense
              outlined
              type="textarea"
              label="Your review"
              autogrow
              :rules="[(v) => !!(v && v.trim()) || 'Please write a few words']"
            />
          </q-card-section>
          <q-card-actions align="right">
            <q-btn flat no-caps label="Cancel" @click="showForm = false" />
            <q-btn
              type="submit"
              color="primary"
              unelevated
              no-caps
              label="Submit review"
              :loading="submitting"
              :disable="!form.rating"
            />
          </q-card-actions>
        </q-form>
      </q-card>
    </q-slide-transition>

    <!-- List -->
    <div v-if="loading" class="q-py-lg text-center">
      <q-spinner color="primary" size="28px" />
    </div>
    <div
      v-else-if="!reviews.length"
      class="text-grey-6 q-py-lg text-center bg-grey-1 rounded-borders"
    >
      <q-icon name="o_rate_review" size="36px" class="q-mb-sm" />
      <div>No reviews yet. Be the first to share your thoughts.</div>
    </div>
    <div v-else class="q-gutter-y-md">
      <div v-for="(r, i) in reviews" :key="i" class="sf-reviews__item">
        <div class="row items-center justify-between">
          <div class="text-weight-medium">{{ r.reviewerName || 'Anonymous' }}</div>
          <span class="text-caption text-grey-6">{{ formatDate(r.createdOnUtc) }}</span>
        </div>
        <StarRating :value="r.rating || 0" :show-count="false" class="q-my-xs" />
        <div v-if="r.title" class="text-weight-medium">{{ r.title }}</div>
        <div class="text-body2 text-grey-8">{{ r.body }}</div>
      </div>
    </div>
  </div>
</template>

<script setup>
/*
 * Product reviews (WO-14). Shows the average-rating summary + star breakdown and
 * a list of APPROVED reviews. A signed-in customer sees a submit form (rating /
 * title / body); the server enforces the "must have purchased" rule and returns a
 * 403 whose message is surfaced. `summary.enabled === false` hides the section.
 * Emits `summary` so the parent can reflect the rating in the page header.
 */
import { ref, reactive, computed, watch, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { useCustomerAuthStore } from 'stores/customerAuth'
import { useNotify } from 'composables/useNotify'
import { getApiErrorMessage } from 'services/api'
import { formatDate } from 'src/utils/datetime'
import { reviewsApi } from 'modules/storefront/product-engagement-api'
import StarRating from 'modules/storefront/components/StarRating.vue'

const props = defineProps({
  productId: { type: [String, Number], default: null }
})
const emit = defineEmits(['summary'])

const route = useRoute()
const auth = useCustomerAuthStore()
const notify = useNotify()

const loading = ref(false)
const submitting = ref(false)
const showForm = ref(false)
const summary = ref(null)
const reviews = ref([])
const eligibility = ref(null)
const eligibilityLoading = ref(false)
const form = reactive({ rating: 5, title: '', body: '' })

const isAuthenticated = computed(() => auth.isAuthenticated)
// Only an explicit `enabled === false` disables reviews; a missing flag (or missing
// endpoint) degrades to an enabled, empty section.
const enabled = computed(() => !(summary.value && summary.value.enabled === false))
const hidden = computed(() => !!(summary.value && summary.value.enabled === false))
// The customer may write a review only when signed in, reviews are enabled, and the server eligibility
// check (purchased + not already reviewed) passed. While the check is in flight the button stays disabled;
// if the check couldn't run (request failed) we don't block — the submit endpoint still enforces the rule.
const canReview = computed(() => {
  if (!isAuthenticated.value || !enabled.value) return false
  if (eligibilityLoading.value) return false
  if (!eligibility.value) return true
  return !!eligibility.value.canReview
})
const eligibilityMessage = computed(() =>
  (eligibility.value && eligibility.value.message) || 'You can only review products you have purchased.'
)
// Surface the "why you can't review" note only once ineligibility has actually been confirmed.
const showEligibilityNote = computed(() =>
  isAuthenticated.value && enabled.value && !eligibilityLoading.value &&
  !!eligibility.value && !eligibility.value.canReview
)
const loginTo = computed(() => ({ name: 'shop-login', query: { redirect: route.fullPath } }))

const averageRating = computed(() => {
  const s = summary.value || {}
  const a = s.averageRating ?? s.average ?? s.rating
  return typeof a === 'number' ? a : 0
})
const totalCount = computed(() => {
  const s = summary.value || {}
  return s.totalCount ?? s.total ?? s.count ?? reviews.value.length
})

// Normalise the star breakdown: accept star1..star5, breakdown{}, or ratingCounts{};
// fall back to deriving it from the loaded reviews when the summary omits it.
const distribution = computed(() => {
  const s = summary.value || {}
  const counts = {}
  for (let star = 1; star <= 5; star++) {
    counts[star] = Number(
      s['star' + star] ??
        (s.breakdown && (s.breakdown[star] ?? s.breakdown['star' + star])) ??
        (s.ratingCounts && s.ratingCounts[star]) ??
        0
    )
  }
  const anyProvided = Object.values(counts).some((c) => c > 0)
  if (!anyProvided && reviews.value.length) {
    for (const r of reviews.value) {
      const k = Math.round(r.rating || 0)
      if (k >= 1 && k <= 5) counts[k] += 1
    }
  }
  const max = Math.max(1, ...Object.values(counts))
  return [5, 4, 3, 2, 1].map((star) => ({
    star,
    count: counts[star] || 0,
    ratio: (counts[star] || 0) / max
  }))
})

async function loadEligibility () {
  eligibility.value = null
  if (!isAuthenticated.value || !props.productId) return
  eligibilityLoading.value = true
  try {
    eligibility.value = await reviewsApi.eligibility(props.productId)
  } catch (e) {
    // Couldn't determine eligibility — leave it unknown so we don't wrongly block an eligible shopper;
    // the submit endpoint still enforces the purchase rule.
    eligibility.value = null
  } finally {
    eligibilityLoading.value = false
  }
}

async function load () {
  if (!props.productId) return
  loading.value = true
  loadEligibility() // kicked off in parallel so the button reflects eligibility from the first render
  try {
    const res = await reviewsApi.list(props.productId)
    summary.value = res && res.summary ? res.summary : {}
    reviews.value = res && Array.isArray(res.reviews) ? res.reviews : []
  } catch (e) {
    // Endpoint may not exist yet — degrade to an empty, enabled section.
    summary.value = {}
    reviews.value = []
  } finally {
    loading.value = false
    emit('summary', {
      enabled: enabled.value,
      averageRating: averageRating.value,
      totalCount: totalCount.value
    })
  }
}

async function submit () {
  if (!form.rating) {
    notify.warning('Please choose a star rating.')
    return
  }
  submitting.value = true
  try {
    await reviewsApi.submit(props.productId, {
      rating: form.rating,
      title: form.title.trim(),
      body: form.body.trim()
    })
    notify.success('Thanks! Your review was submitted for approval.')
    showForm.value = false
    form.rating = 5
    form.title = ''
    form.body = ''
    await load()
  } catch (e) {
    // 403 => must have purchased the product; surface the server's message.
    notify.error(getApiErrorMessage(e))
  } finally {
    submitting.value = false
  }
}

watch(() => props.productId, load)
// Re-check eligibility when the shopper signs in or out without leaving the page.
watch(isAuthenticated, loadEligibility)
onMounted(load)
</script>

<style scoped lang="scss">
.sf-reviews__score {
  min-width: 120px;
}
.sf-reviews__avg {
  font-size: 40px;
  font-weight: 700;
  line-height: 1;
  color: var(--sf-heading);
}
.sf-reviews__bar-row + .sf-reviews__bar-row {
  margin-top: 6px;
}
.sf-reviews__bar-label {
  width: 46px;
  flex: 0 0 auto;
}
.sf-reviews__bar-count {
  width: 28px;
  flex: 0 0 auto;
  text-align: right;
}
.sf-reviews__item {
  border: 1px solid var(--sf-border);
  border-radius: var(--sf-radius);
  padding: 14px 16px;
}
.sf-reviews__note {
  max-width: 240px;
  line-height: 1.4;
}
</style>
