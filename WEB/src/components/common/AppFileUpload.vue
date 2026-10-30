<template>
  <div class="app-field app-fileupload">
    <AppFieldLabel v-if="label" :label="label" :required="required">
      <template #hint>{{ hintText }}</template>
    </AppFieldLabel>

    <!-- Dropzone -->
    <div
      class="app-fileupload__zone"
      :class="{ 'app-fileupload__zone--drag': dragging, 'app-fileupload__zone--busy': uploading }"
      role="button"
      tabindex="0"
      @click="pick"
      @keydown.enter.prevent="pick"
      @dragover.prevent="dragging = true"
      @dragleave.prevent="dragging = false"
      @drop.prevent="onDrop"
    >
      <q-spinner v-if="uploading" size="26px" color="primary" />
      <template v-else>
        <q-icon name="o_cloud_upload" size="30px" class="text-grey-6" />
        <div class="text-body2 text-grey-8">
          Drag &amp; drop {{ multiple ? 'files' : 'a file' }} here, or <span class="text-primary">browse</span>
        </div>
        <div class="text-caption text-grey-6">{{ hintText }}</div>
      </template>
      <input ref="inputRef" type="file" class="hidden" :accept="accept" :multiple="multiple" @change="onSelect">
    </div>

    <!-- Uploaded files as a mapping table (variant='table') -->
    <q-markup-table
      v-if="items.length && variant === 'table'"
      flat
      bordered
      dense
      class="q-mt-sm app-fileupload__table"
    >
      <thead>
        <tr>
          <th class="text-left">File</th>
          <th class="text-right" style="width: 72px">Actions</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="(item, i) in items" :key="item.url + i">
          <td class="text-left">
            <div class="row items-center no-wrap">
              <q-icon :name="isImage(item) ? 'o_image' : 'o_description'" size="20px" class="q-mr-sm text-grey-7" />
              <a :href="$media(item.url)" target="_blank" rel="noopener" class="text-primary ellipsis">{{ item.name }}</a>
            </div>
          </td>
          <td class="text-right">
            <q-btn flat round dense size="sm" color="negative" icon="o_delete" aria-label="Remove" @click="remove(i)">
              <q-tooltip>Remove</q-tooltip>
            </q-btn>
          </td>
        </tr>
      </tbody>
    </q-markup-table>

    <!-- Previews (thumbnail grid, default) -->
    <div v-if="items.length && variant === 'grid'" class="row q-col-gutter-sm q-mt-sm">
      <div v-for="(item, i) in items" :key="item.url + i" class="col-auto">
        <div class="app-fileupload__item">
          <img
            v-if="isImage(item)"
            :src="$media(item.url)"
            :alt="item.name"
            class="app-fileupload__thumb"
            @click="openLightbox(item)"
          >
          <div v-else class="app-fileupload__file" :title="item.name">
            <q-icon name="o_description" size="26px" class="text-grey-7" />
          </div>
          <q-btn
            round
            dense
            size="xs"
            color="negative"
            icon="o_close"
            class="app-fileupload__remove"
            aria-label="Remove"
            @click.stop="remove(i)"
          />
          <div class="app-fileupload__name" :title="item.name">{{ item.name }}</div>
        </div>
      </div>
    </div>

    <div v-if="errorMessage" class="text-negative text-caption q-mt-xs">{{ errorMessage }}</div>

    <!-- Image preview + SEO/metadata editor (editable when the item is a media-library asset) -->
    <MediaSeoDialog
      v-model="lightbox"
      :media-id="media ? (modelValue || null) : null"
      :fallback-url="lightboxUrl"
      @saved="onMediaSaved"
    />
  </div>
</template>

<script setup>
/*
 * AppFileUpload: the portal-standard single/multi file upload (replaces "Add URL"
 * fields). Drag & drop or browse, per-file preview with a remove action, click an
 * image to open it full-width in a centred popup, and hints for allowed extensions
 * + size limits. Uploads to the generic storage endpoint and returns public URLs.
 *
 * v-model: single → the URL string; multiple → an array of { url, assetKey, name }.
 * `media` mode (single only): v-model binds the central Media asset **id**, and the display URL is
 * carried separately via `v-model:previewUrl`. Uploads run the two-step Media flow (prepare → commit)
 * so no physical URL is stored on the owning record — only the Media id.
 * Limits: single ≤ 5 MB; multiple ≤ 20 files and ≤ 50 MB per selection.
 */
import { ref, computed } from 'vue'
import { api, unwrap, getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'
import { deleteConfirmation } from 'dialogs/delete_confirmation'
import AppFieldLabel from './AppFieldLabel.vue'

const props = defineProps({
  modelValue: { type: [String, Array], default: '' },
  multiple: { type: Boolean, default: false },
  // Media mode: model is the Media asset id; the display URL is v-model:previewUrl. Single-file only.
  media: { type: Boolean, default: false },
  previewUrl: { type: String, default: '' },
  label: { type: String, default: '' },
  required: { type: Boolean, default: false },
  hint: { type: String, default: '' },
  folder: { type: String, default: 'uploads' },
  accept: { type: String, default: 'image/*' },
  extensionsLabel: { type: String, default: '' },
  // Limits per the portal standard.
  maxFiles: { type: Number, default: 20 },
  singleMaxMb: { type: Number, default: 5 },
  multiMaxTotalMb: { type: Number, default: 50 },
  // Optional custom upload: async (file) => the item object stored in the model. Lets non-admin surfaces
  // (e.g. the storefront) upload through their own endpoint instead of the admin storage endpoint. In
  // multiple mode the returned objects are appended to the model as-is (so callers can carry extra fields
  // such as a mediaId); in single mode the model becomes that object rather than a bare URL string.
  uploadFn: { type: Function, default: null },
  // Uploaded-item layout: 'grid' (thumbnail previews) or 'table' (a File/Actions mapping table).
  variant: { type: String, default: 'grid' }
})

const emit = defineEmits(['update:modelValue', 'update:previewUrl'])
const notify = useNotify()

const inputRef = ref(null)
const dragging = ref(false)
const uploading = ref(false)
const errorMessage = ref('')
const lightbox = ref(false)
const lightboxUrl = ref('')

const IMAGE_EXT = ['png', 'jpg', 'jpeg', 'gif', 'webp', 'svg', 'bmp', 'avif']

// Normalise the model into a preview list of { url, assetKey?, name }.
const items = computed(() => {
  if (props.media) {
    // Media mode: the model holds the id; preview comes from previewUrl.
    return props.previewUrl ? [{ url: props.previewUrl, name: fileNameFromUrl(props.previewUrl) }] : []
  }
  if (props.multiple) {
    return Array.isArray(props.modelValue)
      ? props.modelValue.map((v) => (typeof v === 'string' ? { url: v, name: fileNameFromUrl(v) } : v))
      : []
  }
  return props.modelValue ? [{ url: props.modelValue, name: fileNameFromUrl(props.modelValue) }] : []
})

const hintText = computed(() => {
  if (props.hint) return props.hint
  const ext = props.extensionsLabel || (props.accept === 'image/*' ? 'PNG, JPG, GIF, WEBP, SVG' : props.accept)
  return props.multiple
    ? `${ext} — up to ${props.maxFiles} files, ${props.multiMaxTotalMb} MB total`
    : `${ext} — up to ${props.singleMaxMb} MB`
})

function fileNameFromUrl (url) {
  if (!url || typeof url !== 'string') return 'file'
  try {
    const clean = url.split('?')[0].split('#')[0]
    return decodeURIComponent(clean.substring(clean.lastIndexOf('/') + 1)) || 'file'
  } catch (e) {
    return 'file'
  }
}

function isImage (item) {
  const name = (item.name || item.url || '').toLowerCase()
  const ext = name.split('.').pop()
  return IMAGE_EXT.includes(ext) || (item.url || '').startsWith('data:image')
}

function pick () {
  if (!uploading.value && inputRef.value) inputRef.value.click()
}

function onSelect (e) {
  const files = Array.from(e.target.files || [])
  if (files.length) upload(files)
  e.target.value = '' // allow re-selecting the same file
}

function onDrop (e) {
  dragging.value = false
  const files = Array.from(e.dataTransfer?.files || [])
  if (files.length) upload(files)
}

function validate (files) {
  errorMessage.value = ''
  const mb = (bytes) => bytes / (1024 * 1024)

  if (!props.multiple) {
    if (files.length > 1) files = files.slice(0, 1)
    if (mb(files[0].size) > props.singleMaxMb) {
      errorMessage.value = `File exceeds the ${props.singleMaxMb} MB limit.`
      return null
    }
    return files
  }

  const existing = items.value.length
  if (existing + files.length > props.maxFiles) {
    errorMessage.value = `You can upload at most ${props.maxFiles} files.`
    return null
  }
  const total = files.reduce((sum, f) => sum + f.size, 0)
  if (mb(total) > props.multiMaxTotalMb) {
    errorMessage.value = `Selection exceeds the ${props.multiMaxTotalMb} MB total limit.`
    return null
  }
  return files
}

// Media mode: run the two-step Media flow (prepare → commit) for a single file and emit the Media id
// + resolved preview URL. No physical URL is stored on the owning record.
async function uploadMedia (rawFiles) {
  errorMessage.value = ''
  const file = (rawFiles || [])[0]
  if (!file) return
  if (file.size / (1024 * 1024) > props.singleMaxMb) {
    errorMessage.value = `File exceeds the ${props.singleMaxMb} MB limit.`
    return
  }

  uploading.value = true
  try {
    const form = new FormData()
    form.append('file', file)
    const draft = await api.post('/api/admin/media/prepare', form).then(unwrap)
    const committed = await api.post('/api/admin/media', {
      tempId: draft.tempId,
      seoFileName: draft.suggestedSeoFileName,
      altText: null,
      title: null,
      caption: null,
      description: null
    }).then(unwrap)
    emit('update:modelValue', committed.mediaId)
    emit('update:previewUrl', committed.publicUrl)
  } catch (err) {
    errorMessage.value = getApiErrorMessage(err)
    notify.error(errorMessage.value)
  } finally {
    uploading.value = false
  }
}

async function upload (rawFiles) {
  if (props.media) return uploadMedia(rawFiles)

  const files = validate(rawFiles)
  if (!files) return

  uploading.value = true
  try {
    const uploaded = []
    for (const file of files) {
      if (props.uploadFn) {
        const item = await props.uploadFn(file)
        if (item) uploaded.push(item)
      } else {
        const form = new FormData()
        form.append('file', file)
        form.append('folder', props.folder)
        const ref = await api.post('/api/admin/storage/upload', form).then(unwrap)
        if (ref?.publicUrl) uploaded.push({ url: ref.publicUrl, assetKey: ref.assetKey, name: file.name })
      }
    }

    if (props.multiple) {
      const current = Array.isArray(props.modelValue) ? props.modelValue.slice() : []
      emit('update:modelValue', current.concat(uploaded))
    } else if (uploaded.length) {
      // A custom uploadFn returns a caller-shaped object; the default path stores the bare URL string.
      emit('update:modelValue', props.uploadFn ? uploaded[0] : uploaded[0].url)
    }
  } catch (err) {
    errorMessage.value = getApiErrorMessage(err)
    notify.error(errorMessage.value)
  } finally {
    uploading.value = false
  }
}

async function remove (index) {
  if (!(await deleteConfirmation('this file', { title: 'Remove', okLabel: 'Remove', message: 'Are you sure you want to remove this file?' }))) return
  errorMessage.value = ''
  if (props.media) {
    emit('update:modelValue', null)
    emit('update:previewUrl', '')
  } else if (props.multiple) {
    const next = (Array.isArray(props.modelValue) ? props.modelValue.slice() : [])
    next.splice(index, 1)
    emit('update:modelValue', next)
  } else {
    emit('update:modelValue', '')
  }
}

function openLightbox (item) {
  lightboxUrl.value = item.url
  lightbox.value = true
}

// A SEO file-name change can alter the resolved public URL — refresh the preview to match.
function onMediaSaved (dto) {
  if (props.media && dto && dto.publicUrl) emit('update:previewUrl', dto.publicUrl)
}
</script>

<style scoped lang="scss">
.app-fileupload__zone {
  border: 1.5px dashed rgba(0, 0, 0, 0.22);
  border-radius: 6px;
  padding: 20px;
  text-align: center;
  cursor: pointer;
  transition: border-color 0.2s, background 0.2s;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 4px;
}
.app-fileupload__zone:hover { border-color: var(--q-primary); }
.app-fileupload__zone--drag { border-color: var(--q-primary); background: rgba(21, 101, 192, 0.05); }
.app-fileupload__zone--busy { pointer-events: none; opacity: 0.75; }
.hidden { display: none; }

.app-fileupload__item {
  position: relative;
  width: 88px;
}
.app-fileupload__thumb,
.app-fileupload__file {
  width: 88px;
  height: 88px;
  border: 1px solid rgba(0, 0, 0, 0.12);
  border-radius: 6px;
  object-fit: cover;
  background: #fafafa;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
}
.app-fileupload__remove {
  position: absolute;
  top: -8px;
  right: -8px;
}
.app-fileupload__name {
  font-size: 11px;
  color: #6b7280;
  margin-top: 2px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  max-width: 88px;
}

.app-fileupload__lightbox {
  position: relative;
  width: 96vw;
  max-width: 1100px;
  background: transparent;
  box-shadow: none;
}
.app-fileupload__lightbox-img {
  width: 100%;
  height: auto;
  display: block;
  border-radius: 6px;
}
.app-fileupload__lightbox-close {
  position: absolute;
  top: 8px;
  right: 8px;
  z-index: 2;
  background: rgba(255, 255, 255, 0.9);
}
</style>
