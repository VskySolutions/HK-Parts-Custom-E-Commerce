<template>
  <q-dialog :model-value="modelValue" @update:model-value="$emit('update:modelValue', $event)" @hide="onHide">
    <q-card class="media-seo">
      <q-card-section class="row items-center q-py-sm">
        <q-icon name="o_image" color="primary" size="22px" class="q-mr-sm" />
        <div class="text-subtitle1 text-weight-medium col">Image details</div>
        <q-btn flat round dense icon="o_close" v-close-popup />
      </q-card-section>
      <q-separator />

      <q-inner-loading :showing="loading" color="primary" />

      <q-card-section class="row q-col-gutter-md">
        <!-- Preview -->
        <div class="col-12 col-md-5">
          <div class="media-seo__preview">
            <img :src="$media(imageUrl)" alt="Preview">
            <!-- Hover actions: view full image + download -->
            <div v-if="imageUrl" class="media-seo__hover">
              <q-btn round dense unelevated color="dark" text-color="white" icon="o_open_in_full" @click="fullOpen = true">
                <q-tooltip>View full image</q-tooltip>
              </q-btn>
              <q-btn round dense unelevated color="dark" text-color="white" icon="o_download" @click="download">
                <q-tooltip>Download</q-tooltip>
              </q-btn>
            </div>
          </div>
          <div v-if="meta" class="text-caption text-grey-7 q-mt-sm q-gutter-y-xs">
            <div v-if="meta.originalFileName"><span class="text-grey-6">File:</span> {{ meta.originalFileName }}</div>
            <div v-if="meta.width && meta.height"><span class="text-grey-6">Size:</span> {{ meta.width }} × {{ meta.height }} px</div>
            <div v-if="meta.fileSizeBytes"><span class="text-grey-6">Weight:</span> {{ prettySize(meta.fileSizeBytes) }}</div>
            <div v-if="meta.mimeType"><span class="text-grey-6">Type:</span> {{ meta.mimeType }}</div>
          </div>
        </div>

        <!-- SEO / accessibility fields -->
        <div class="col-12 col-md-7">
          <template v-if="editable">
            <AppTextField v-model="form.altText" label="Alt text" hint="Describes the image for screen readers & SEO" />
            <AppTextField v-model="form.seoFileName" label="SEO file name" hint="Lowercase letters, digits and single hyphens (e.g. red-running-shoe)" />
            <AppTextField v-model="form.title" label="Title" />
            <AppTextField v-model="form.caption" label="Caption" />
            <AppTextField v-model="form.description" label="Description" type="textarea" autogrow />
            <div v-if="errorMessage" class="text-negative text-caption q-mt-xs">{{ errorMessage }}</div>
          </template>
          <div v-else class="text-grey-6 q-pa-md text-center">
            SEO details are available for media-library images once uploaded.
          </div>
        </div>
      </q-card-section>

      <template v-if="editable">
        <q-separator />
        <q-card-actions align="right" class="q-pa-md">
          <q-btn flat no-caps color="grey-8" label="Cancel" v-close-popup />
          <q-btn unelevated no-caps color="primary" icon="o_save" label="Save details" :loading="saving" @click="save" />
        </q-card-actions>
      </template>
    </q-card>
  </q-dialog>

  <!-- Full-image viewer -->
  <q-dialog v-model="fullOpen" maximized>
    <div class="media-seo__full" @click="fullOpen = false">
      <q-btn round dense icon="o_close" color="white" text-color="dark" class="media-seo__full-close" @click.stop="fullOpen = false" />
      <q-btn round dense icon="o_download" color="white" text-color="dark" class="media-seo__full-download" @click.stop="download">
        <q-tooltip>Download</q-tooltip>
      </q-btn>
      <img :src="$media(imageUrl)" alt="Full image" @click.stop>
    </div>
  </q-dialog>
</template>

<script setup>
/*
 * MediaSeoDialog: the standard image-preview popup that also edits a media item's SEO / accessibility
 * metadata (alt text, SEO file name, title, caption, description). Given a `mediaId` it loads the
 * current values (GET /api/admin/media/{id}) and saves via PUT; without a mediaId it shows the image
 * read-only (`fallbackUrl`). Emits `saved` with the updated MediaDto so the opener can refresh.
 */
import { ref, reactive, computed, watch } from 'vue'
import { mediaApi, mediaUrl, getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'
import AppTextField from './AppTextField.vue'

const props = defineProps({
  modelValue: { type: Boolean, default: false },
  mediaId: { type: [String, null], default: null },
  fallbackUrl: { type: String, default: '' }
})
const emit = defineEmits(['update:modelValue', 'saved'])
const notify = useNotify()

const loading = ref(false)
const saving = ref(false)
const errorMessage = ref('')
const meta = ref(null)
const fullOpen = ref(false)
const form = reactive({ seoFileName: '', altText: '', title: '', caption: '', description: '' })

const editable = computed(() => !!props.mediaId)
const imageUrl = computed(() => meta.value?.publicUrl || props.fallbackUrl)

function prettySize (bytes) {
  if (!bytes) return ''
  const kb = bytes / 1024
  return kb < 1024 ? `${Math.round(kb)} KB` : `${(kb / 1024).toFixed(1)} MB`
}

function fileNameFromUrl (url) {
  if (!url || typeof url !== 'string') return 'image'
  try {
    const clean = url.split('?')[0].split('#')[0]
    return decodeURIComponent(clean.substring(clean.lastIndexOf('/') + 1)) || 'image'
  } catch (e) {
    return 'image'
  }
}

// Download the image. Fetch to a blob first (so it saves rather than navigates, even cross-origin
// on the API host); fall back to opening in a new tab if the fetch is blocked.
async function download () {
  const url = mediaUrl(imageUrl.value)
  if (!url) return
  const name = meta.value?.originalFileName || fileNameFromUrl(imageUrl.value)
  try {
    const res = await fetch(url)
    if (!res.ok) throw new Error('fetch failed')
    const blob = await res.blob()
    const href = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = href
    a.download = name
    document.body.appendChild(a)
    a.click()
    a.remove()
    URL.revokeObjectURL(href)
  } catch (e) {
    window.open(url, '_blank', 'noopener')
  }
}

async function load () {
  errorMessage.value = ''
  meta.value = null
  if (!props.mediaId) return
  loading.value = true
  try {
    const dto = await mediaApi.get(props.mediaId)
    meta.value = dto
    form.seoFileName = dto.seoFileName || ''
    form.altText = dto.altText || ''
    form.title = dto.title || ''
    form.caption = dto.caption || ''
    form.description = dto.description || ''
  } catch (err) {
    errorMessage.value = getApiErrorMessage(err)
  } finally {
    loading.value = false
  }
}

async function save () {
  errorMessage.value = ''
  saving.value = true
  try {
    const dto = await mediaApi.update(props.mediaId, {
      seoFileName: form.seoFileName.trim(),
      altText: form.altText.trim() || null,
      title: form.title.trim() || null,
      caption: form.caption.trim() || null,
      description: form.description.trim() || null
    })
    notify.success('Image details saved')
    emit('saved', dto)
    emit('update:modelValue', false)
  } catch (err) {
    errorMessage.value = getApiErrorMessage(err)
    notify.error(errorMessage.value)
  } finally {
    saving.value = false
  }
}

function onHide () {
  emit('update:modelValue', false)
}

// Load fresh values each time the dialog opens.
watch(
  () => props.modelValue,
  (open) => { if (open) load() }
)
</script>

<style scoped lang="scss">
.media-seo {
  width: 60vw;
  max-width: 94vw;
}
.media-seo__preview {
  position: relative;
  background: #f4f4f6;
  border: 1px solid #e4e4e7;
  border-radius: 6px;
  min-height: 180px;
  display: flex;
  align-items: center;
  justify-content: center;
  overflow: hidden;

  img {
    max-width: 100%;
    max-height: 320px;
    object-fit: contain;
    display: block;
  }
}
.media-seo__hover {
  position: absolute;
  right: 8px;
  bottom: 8px;
  display: flex;
  gap: 6px;
  opacity: 0;
  transform: translateY(4px);
  transition: opacity 0.2s ease, transform 0.2s ease;
}
.media-seo__preview:hover .media-seo__hover { opacity: 1; transform: translateY(0); }
.media-seo__hover .q-btn { box-shadow: 0 1px 4px rgba(0, 0, 0, 0.35); }

.media-seo__full {
  width: 100%;
  height: 100%;
  background: rgba(0, 0, 0, 0.9);
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 24px;
  cursor: zoom-out;

  img {
    max-width: 96vw;
    max-height: 92vh;
    object-fit: contain;
    cursor: default;
  }
}
.media-seo__full-close { position: fixed; top: 16px; right: 16px; }
.media-seo__full-download { position: fixed; top: 16px; right: 64px; }
</style>
