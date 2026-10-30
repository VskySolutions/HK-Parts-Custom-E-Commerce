<template>
  <AppFormDrawer
    ref="drawerRef"
    :model-value="modelValue"
    title="Add FAQ"
    :saving="saving"
    @update:model-value="$emit('update:modelValue', $event)"
    @submit="onSubmit"
  >
    <div class="column q-gutter-md">
      <q-banner dense class="bg-blue-1 text-primary rounded-borders">
        <template #avatar><q-icon name="o_info" color="primary" /></template>
        <strong>Publish now</strong> makes it live on the product page straight away.
        <strong>Save as draft</strong> keeps it hidden until you approve it from the moderation queue.
      </q-banner>

      <!-- Product picker: only when a product isn't already in context (list page). -->
      <q-select
        v-if="!productId"
        v-model="form.productId"
        label="Product"
        dense outlined
        use-input
        input-debounce="300"
        :options="productOptions"
        emit-value
        map-options
        :loading="productLoading"
        :rules="[(val) => !!val || 'Select a product']"
        hint="Search by product name"
        @filter="onProductFilter"
      >
        <template #no-option>
          <q-item>
            <q-item-section class="text-grey-7">
              {{ productLoading ? 'Searching…' : 'Type to search products' }}
            </q-item-section>
          </q-item>
        </template>
        <template #option="scope">
          <q-item v-bind="scope.itemProps">
            <q-item-section>
              <q-item-label>{{ scope.opt.label }}</q-item-label>
              <q-item-label v-if="scope.opt.sku" caption>SKU: {{ scope.opt.sku }}</q-item-label>
            </q-item-section>
          </q-item>
        </template>
      </q-select>

      <!-- Fixed-product context (panel usage). -->
      <div v-else-if="productName">
        <div class="text-caption text-grey-7">Product</div>
        <div class="text-weight-medium">{{ productName }}</div>
      </div>

      <AppTextField
        v-model="form.questionText"
        label="Question"
        required
        type="textarea"
        autogrow
        input-style="min-height: 70px"
        placeholder="e.g. Is this product waterproof?"
        :rules="[req]"
      />

      <AppTextField
        v-model="form.answerText"
        label="Answer"
        required
        type="textarea"
        autogrow
        input-style="min-height: 110px"
        placeholder="Write the answer shoppers will see"
        :rules="[req]"
      />

      <AppTextField
        v-model="form.askerName"
        label="Asked by"
        placeholder="Store"
        hint="Display name shown alongside the question on the storefront (optional)"
      />
    </div>

    <!-- Two explicit actions: keep it as a draft (Pending) or publish it live (Approved). -->
    <template #footer="{ submit, cancel }">
      <q-btn flat no-caps color="grey-8" label="Cancel" :disable="saving" @click="cancel" />
      <q-btn
        outline no-caps color="primary" icon="o_drafts" label="Save as draft"
        :loading="saving && mode === 'draft'" :disable="saving"
        @click="() => triggerSubmit('draft', submit)"
      />
      <q-btn
        unelevated no-caps color="primary" icon="o_public" label="Publish now"
        :loading="saving && mode === 'publish'" :disable="saving"
        @click="() => triggerSubmit('publish', submit)"
      />
    </template>
  </AppFormDrawer>
</template>

<script setup>
/*
 * ProductFaqFormDrawer: authors a FAQ (question + answer) for a product. Used both from the CMS Product
 * Q&A list (with a product picker) and from the product page's FAQ tab (product fixed via the productId
 * prop). Two actions: "Save as draft" (Pending, hidden) and "Publish now" (Approved, live) — both go to
 * questionApi.createFaq with the corresponding `publish` flag.
 */
import { ref, reactive, watch, nextTick } from 'vue'
import { productApi } from 'modules/catalog/api'
import { questionApi } from 'modules/product-qa/api'
import { getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'

const props = defineProps({
  modelValue: { type: Boolean, default: false },
  // When set, the FAQ is scoped to this product and the picker is hidden (product page usage).
  productId: { type: String, default: '' },
  productName: { type: String, default: '' }
})
const emit = defineEmits(['update:modelValue', 'created'])

const notify = useNotify()

const drawerRef = ref(null)
const saving = ref(false)
const mode = ref('draft') // 'draft' | 'publish' — set by whichever footer button is pressed

const form = reactive({ productId: null, questionText: '', answerText: '', askerName: 'Store' })

// Tag the intended action, then run AppFormDrawer's validate-then-submit flow.
function triggerSubmit (m, submit) {
  mode.value = m
  submit()
}

const req = (val) => (!!val && String(val).trim().length > 0) || 'Required'

// ---- Product picker (list-page usage) ----------------------------------------
const productOptions = ref([])
const productLoading = ref(false)

function onProductFilter (val, update, abort) {
  productLoading.value = true
  productApi.list({ search: val || undefined, page: 1, pageSize: 20 })
    .then((result) => {
      const items = Array.isArray(result) ? result : result?.items || []
      update(() => { productOptions.value = items.map((p) => ({ label: p.name, value: p.id, sku: p.sku })) })
    })
    .catch(() => { abort() })
    .finally(() => { productLoading.value = false })
}

// Reset the form each time the drawer opens.
watch(() => props.modelValue, (open) => {
  if (!open) return
  form.productId = props.productId || null
  form.questionText = ''
  form.answerText = ''
  form.askerName = 'Store'
  productOptions.value = []
  nextTick(() => drawerRef.value?.resetValidation?.())
})

async function onSubmit () {
  const publish = mode.value === 'publish'
  saving.value = true
  try {
    const dto = await questionApi.createFaq({
      productId: form.productId,
      questionText: form.questionText.trim(),
      answerText: form.answerText.trim(),
      askerName: form.askerName && form.askerName.trim() ? form.askerName.trim() : undefined,
      publish
    })
    notify.success(publish ? 'FAQ published' : 'FAQ saved as draft')
    emit('created', dto)
    emit('update:modelValue', false)
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    saving.value = false
  }
}
</script>
