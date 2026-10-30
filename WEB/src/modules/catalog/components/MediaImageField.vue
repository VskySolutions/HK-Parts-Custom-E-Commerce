<template>
  <div class="app-field">
    <AppFieldLabel :label="label">
      <template v-if="hint" #hint>{{ hint }}</template>
    </AppFieldLabel>

    <div class="row items-center q-gutter-sm">
      <div v-if="url" class="media-image-field__thumb">
        <img :src="$media(url)" :alt="alt || label" class="media-image-field__img">
        <q-btn v-if="!disable" round dense size="xs" color="negative" icon="o_close" class="media-image-field__remove" @click="clear">
          <q-tooltip>Remove</q-tooltip>
        </q-btn>
      </div>
      <q-file
        v-if="!disable"
        v-model="file"
        dense outlined
        accept="image/*"
        :label="url ? 'Replace image' : 'Upload image'"
        :loading="uploading"
        class="col"
        style="max-width: 320px"
        @update:model-value="onFile"
      >
        <template #prepend><q-icon name="o_add_photo_alternate" /></template>
      </q-file>
      <div v-else-if="!url" class="text-grey-6 text-caption">No image.</div>
    </div>
  </div>
</template>

<script setup>
/*
 * Single-image picker backed by the media library: uploads via the two-step prepare → commit flow and
 * emits the committed media id (v-model) plus its public URL (update:url) for the preview.
 */
import { ref } from 'vue'
import { getApiErrorMessage } from 'services/api'
import { mediaApi } from 'modules/catalog/api'
import { useNotify } from 'composables/useNotify'
import AppFieldLabel from 'components/common/AppFieldLabel.vue'

const props = defineProps({
  modelValue: { type: String, default: null },
  url: { type: String, default: null },
  label: { type: String, default: 'Image' },
  hint: { type: String, default: '' },
  alt: { type: String, default: '' },
  disable: { type: Boolean, default: false }
})
const emit = defineEmits(['update:modelValue', 'update:url'])

const notify = useNotify()
const file = ref(null)
const uploading = ref(false)

async function onFile (selected) {
  if (!selected) return
  uploading.value = true
  try {
    const draft = await mediaApi.prepare(selected)
    const committed = await mediaApi.commit({
      tempId: draft.tempId,
      seoFileName: draft.suggestedSeoFileName,
      altText: props.alt || null,
      title: null,
      caption: null,
      description: null
    })
    emit('update:url', committed.publicUrl)
    emit('update:modelValue', committed.mediaId)
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    uploading.value = false
    file.value = null
  }
}

function clear () {
  emit('update:url', null)
  emit('update:modelValue', null)
}
</script>

<style scoped lang="scss">
.media-image-field {
  &__thumb { position: relative; width: 96px; }
  &__img {
    width: 96px; height: 96px; object-fit: cover; display: block;
    border: 1px solid rgba(0, 0, 0, 0.12); border-radius: 6px; background: #fafafa;
  }
  &__remove { position: absolute; top: -8px; right: -8px; }
}
</style>
