<template>
  <div class="sf-qa">
    <!-- Header -->
    <div class="sf-qa__head">
      <h3 class="sf-qa__title">Questions &amp; Answers</h3>
      <q-btn
        outline
        no-caps
        color="primary"
        icon="o_help_outline"
        label="Ask a question"
        class="sf-qa__ask"
        @click="showForm = !showForm"
      />
    </div>

    <!-- Ask form (public) -->
    <q-slide-transition>
      <q-card v-if="showForm" flat bordered class="sf-qa__form q-mb-lg">
        <q-form @submit.prevent="submit">
          <q-card-section>
            <div class="row q-col-gutter-md">
              <q-input
                v-model="form.askerName"
                class="col-12 col-sm-6"
                dense
                outlined
                label="Your name"
                :rules="[(v) => !!(v && v.trim()) || 'Name is required']"
              />
              <q-input
                v-model="form.askerEmail"
                class="col-12 col-sm-6"
                dense
                outlined
                type="email"
                label="Email"
                :rules="emailRules"
              />
              <q-input
                v-model="form.questionText"
                class="col-12"
                dense
                outlined
                type="textarea"
                label="Your question"
                autogrow
                :rules="[(v) => !!(v && v.trim()) || 'Please enter your question']"
              />
            </div>
          </q-card-section>
          <q-card-actions align="right">
            <q-btn flat no-caps label="Cancel" @click="showForm = false" />
            <q-btn
              type="submit"
              color="primary"
              unelevated
              no-caps
              label="Submit question"
              :loading="submitting"
            />
          </q-card-actions>
        </q-form>
      </q-card>
    </q-slide-transition>

    <!-- States -->
    <div v-if="loading" class="q-py-lg text-center">
      <q-spinner color="primary" size="28px" />
    </div>
    <div v-else-if="!questions.length" class="sf-qa__empty">
      <q-icon name="o_forum" size="26px" class="q-mb-xs" />
      <div>No questions yet. Be the first to ask.</div>
    </div>

    <!-- Threaded Q / A list -->
    <ul v-else class="sf-qa__list">
      <li v-for="(q, i) in questions" :key="i" class="sf-qa__item">
        <div class="sf-qa__row">
          <span class="sf-qa__badge">Q</span>
          <div class="sf-qa__content">
            <div class="sf-qa__question">{{ q.questionText }}</div>
            <div class="sf-qa__meta">
              {{ q.askerName || 'Anonymous' }}<template v-if="q.createdOnUtc"> · {{ formatDate(q.createdOnUtc) }}</template>
            </div>
          </div>
        </div>
        <div v-if="q.answerText" class="sf-qa__row sf-qa__row--answer">
          <span class="sf-qa__badge sf-qa__badge--answer">A</span>
          <div class="sf-qa__content">
            <div class="sf-qa__answer">{{ q.answerText }}</div>
          </div>
        </div>
      </li>
    </ul>
  </div>
</template>

<script setup>
/*
 * Product questions & answers (WO-58). Lists APPROVED + answered questions and
 * offers a PUBLIC "Ask a question" form (name, optional email, question). The
 * submit posts anonymously with a reCAPTCHA token (form type 'QaSubmit') and the
 * question is queued for moderation.
 */
import { ref, reactive, watch, onMounted } from 'vue'
import { useNotify } from 'composables/useNotify'
import { getApiErrorMessage } from 'services/api'
import { formatDate } from 'src/utils/datetime'
import { questionsApi } from 'modules/storefront/product-engagement-api'
import { useRecaptcha } from 'modules/storefront/composables/useRecaptcha'

const props = defineProps({
  productId: { type: [String, Number], default: null }
})

const notify = useNotify()
const { getToken } = useRecaptcha()

const loading = ref(false)
const submitting = ref(false)
const showForm = ref(false)
const questions = ref([])
const form = reactive({ askerName: '', askerEmail: '', questionText: '' })

// Email is required — validate presence + a basic address shape (mirrors the server's EmailAddress rule).
const emailRules = [
  (v) => !!(v && v.trim()) || 'Email is required',
  (v) => /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test((v || '').trim()) || 'Enter a valid email'
]

async function load () {
  if (!props.productId) return
  loading.value = true
  try {
    const res = await questionsApi.list(props.productId)
    questions.value = Array.isArray(res) ? res : (res && (res.items || res.questions)) || []
  } catch (e) {
    // Endpoint may not exist yet — degrade to an empty list.
    questions.value = []
  } finally {
    loading.value = false
  }
}

async function submit () {
  if (!form.askerName.trim() || !form.askerEmail.trim() || !form.questionText.trim()) {
    notify.warning('Please enter your name, email and question.')
    return
  }
  submitting.value = true
  try {
    const recaptchaToken = await getToken('QaSubmit')
    await questionsApi.submit(props.productId, {
      askerName: form.askerName.trim(),
      askerEmail: form.askerEmail.trim(),
      questionText: form.questionText.trim(),
      recaptchaToken
    })
    notify.success('Thanks! Your question was submitted for review.')
    showForm.value = false
    form.askerName = ''
    form.askerEmail = ''
    form.questionText = ''
  } catch (e) {
    notify.error(getApiErrorMessage(e))
  } finally {
    submitting.value = false
  }
}

watch(() => props.productId, load)
onMounted(load)
</script>

<style scoped lang="scss">
.sf-qa__head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  margin-bottom: 16px;
}
.sf-qa__title {
  margin: 0;
  font-size: 18px;
  font-weight: 600;
  color: var(--sf-heading);
}
.sf-qa__ask {
  flex: 0 0 auto;
}
.sf-qa__form {
  border-radius: var(--sf-radius);
}

.sf-qa__empty {
  text-align: center;
  color: var(--sf-muted);
  padding: 32px 0;
}

// Threaded Q/A list — plain rows separated by thin dividers, no cards.
.sf-qa__list {
  list-style: none;
  margin: 0;
  padding: 0;
}
.sf-qa__item {
  padding: 18px 0;

  & + & {
    border-top: 1px solid var(--sf-border);
  }
}
.sf-qa__row {
  display: flex;
  align-items: flex-start;
  gap: 12px;

  &--answer {
    margin-top: 10px;
  }
}
.sf-qa__badge {
  flex: 0 0 auto;
  width: 24px;
  height: 24px;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 12px;
  font-weight: 700;
  line-height: 1;
  background: var(--sf-heading);
  color: #fff;

  &--answer {
    background: var(--sf-surface-alt);
    color: var(--sf-heading);
  }
}
.sf-qa__content {
  min-width: 0;
}
.sf-qa__question {
  font-weight: 600;
  color: var(--sf-heading);
  line-height: 1.4;
}
.sf-qa__meta {
  margin-top: 2px;
  font-size: 12px;
  color: var(--sf-muted);
}
.sf-qa__answer {
  color: var(--sf-text);
  line-height: 1.55;
  white-space: pre-line;
}
</style>
