<template>
  <!-- Proper grouped attribute pickers (Colour swatches, Size dropdown/buttons, custom inputs, …) -->
  <div v-if="hasAttributes" class="variant-selector column q-gutter-md">
    <div v-for="attr in attributes" :key="attr.id" class="vs-attr">
      <div class="text-body2 q-mb-xs">
        <span class="text-weight-medium">{{ attr.name }}</span>
        <span v-if="isCustomInput(attr) && attr.isRequired" class="text-red-6"> *</span>
        <!-- The dropdown displays its own selection, so only the other pickers echo it up here. -->
        <span v-else-if="!isDropdown(attr) && selectedValueName(attr)" class="text-grey-8">: {{ selectedValueName(attr) }}</span>
      </div>

      <!-- Custom input: the shopper types the value (no predefined options) -->
      <q-input
        v-if="isCustomInput(attr)"
        class="vs-field"
        dense
        outlined
        hide-bottom-space
        :model-value="customValues[attr.id] || ''"
        :type="attr.inputType === 'Number' ? 'number' : 'text'"
        :maxlength="attr.inputType === 'Number' ? undefined : (attr.maxLength || undefined)"
        :placeholder="customPlaceholder(attr)"
        :hint="customHint(attr)"
        :error="!!customError(attr)"
        :error-message="customError(attr)"
        @update:model-value="onCustomInput(attr, $event)"
        @blur="touched[attr.id] = true"
      />

      <!-- Swatch: colour circles -->
      <div v-else-if="showSwatches(attr)" class="row q-gutter-sm">
        <button
          v-for="val in attr.values"
          :key="val.id"
          type="button"
          class="vs-swatch"
          :class="{ 'vs-swatch--active': selected[attr.id] === val.id, 'vs-swatch--disabled': !isAvailable(attr, val.id) }"
          :style="{ background: resolveColor(val) || '#e0e0e0' }"
          :aria-label="val.value"
          @click="pick(attr, val.id)"
        >
          <q-icon v-if="selected[attr.id] === val.id" name="o_check" size="16px" class="vs-swatch__check" />
          <q-tooltip>{{ val.value }}<template v-if="!isAvailable(attr, val.id)"> — unavailable</template></q-tooltip>
        </button>
      </div>

      <!-- Dropdown: a real select control -->
      <q-select
        v-else-if="isDropdown(attr)"
        class="vs-field"
        dense
        outlined
        clearable
        emit-value
        map-options
        hide-bottom-space
        option-value="id"
        option-label="value"
        :model-value="selected[attr.id] || null"
        :options="attr.values"
        :option-disable="(val) => !isAvailable(attr, val.id)"
        :display-value="selectedValueName(attr) || `Choose ${attr.name.toLowerCase()}`"
        @update:model-value="choose(attr, $event)"
      />

      <!-- Button: labelled options -->
      <div v-else class="row q-gutter-sm">
        <button
          v-for="val in attr.values"
          :key="val.id"
          type="button"
          class="vs-option"
          :class="{ 'vs-option--active': selected[attr.id] === val.id, 'vs-option--disabled': !isAvailable(attr, val.id) }"
          @click="pick(attr, val.id)"
        >
          {{ val.value }}
        </button>
      </div>
    </div>
  </div>

  <!-- Fallback: no attribute metadata available — legacy SKU chips -->
  <div v-else-if="variants.length" class="variant-selector">
    <div class="text-caption text-grey-7 q-mb-xs">Options</div>
    <div class="row q-gutter-sm">
      <q-chip
        v-for="v in variants"
        :key="v.id"
        clickable
        :outline="modelValue !== v.id"
        :color="modelValue === v.id ? 'primary' : 'grey-3'"
        :text-color="modelValue === v.id ? 'white' : 'dark'"
        :disable="!v.isEnabled"
        @click="pickVariant(v)"
      >
        {{ variantLabel(v) }}
        <q-tooltip v-if="!v.isEnabled">Unavailable</q-tooltip>
      </q-chip>
    </div>
  </div>
</template>

<script setup>
/*
 * Variant selector (WO-19): renders the product's attributes as grouped pickers — colour swatches for
 * Swatch attributes, a select for Dropdown, labelled buttons for Button — and resolves the selected
 * value combination to a concrete variant, emitting its id (or null while incomplete). Values that
 * can't combine with the current selection into an enabled variant are greyed out.
 *
 * CustomInput attributes carry no values: the shopper types the value (text or number, bounded by
 * maxLength) and it is emitted up via `update:customValues`. They drive no variant, so they're kept
 * out of the combination matching; a mandatory one blocks add-to-cart through the exposed validate().
 *
 * Falls back to legacy SKU chips when the product has variants but no attribute metadata.
 */
import { reactive, computed, watch } from 'vue'
import { formatPrice } from 'modules/storefront/api'

const props = defineProps({
  // StorefrontAttributeDto[]: { id, name, displayType, values: [{ id, value, colorHex }],
  //                             inputType, maxLength, isRequired }
  attributes: { type: Array, default: () => [] },
  // StorefrontVariantDto[]: { id, sku, price, stockQuantity, isEnabled, attributeValueIds }
  variants: { type: Array, default: () => [] },
  modelValue: { type: [String, Number, null], default: null }
})

const emit = defineEmits(['update:modelValue', 'update:customValues'])

// attributeId -> selected valueId (option attributes only)
const selected = reactive({})
// attributeId -> typed value (CustomInput attributes only)
const customValues = reactive({})
// attributeId -> whether the shopper has left the field / tried to add to cart (drives the error)
const touched = reactive({})

function isCustomInput (a) { return a.displayType === 'CustomInput' || a.displayType === 3 }
function isDropdown (a) { return a.displayType === 'Dropdown' || a.displayType === 0 }
function isSwatch (a) { return a.displayType === 'Swatch' || a.displayType === 2 }

// The attributes that pick a variant (everything except the typed-in ones).
const optionAttributes = computed(() => props.attributes.filter((a) => !isCustomInput(a)))
const customAttributes = computed(() => props.attributes.filter(isCustomInput))

const hasAttributes = computed(() =>
  optionAttributes.value.some((a) => a.values && a.values.length) || customAttributes.value.length > 0
)

// A value's swatch colour: an explicit hex, else the value's own name when it's a valid CSS colour
// (e.g. "Red" -> red, "Light Blue" -> lightblue). Returns null when nothing resolves.
const _probe = typeof document !== 'undefined' ? document.createElement('span') : null
function cssNamedColor (name) {
  if (!name || !_probe) return null
  const candidate = String(name).trim().toLowerCase().replace(/\s+/g, '')
  if (!candidate) return null
  _probe.style.color = ''
  _probe.style.color = candidate
  return _probe.style.color ? candidate : null
}
function resolveColor (val) { return val.colorHex || cssNamedColor(val.value) }

// Swatches need a colour per value; a Swatch attribute whose values don't all resolve to one falls
// back to labelled buttons rather than rendering a row of blank circles.
function showSwatches (attr) {
  return isSwatch(attr) && (attr.values || []).length > 0 && attr.values.every((v) => !!resolveColor(v))
}

function variantIds (v) { return v.attributeValueIds || [] }

// The variant defined by the current (complete) selection, if any enabled one matches.
const matched = computed(() => {
  const ids = optionAttributes.value.map((a) => selected[a.id]).filter(Boolean)
  if (!ids.length || ids.length !== optionAttributes.value.length) return null
  return props.variants.find((v) =>
    v.isEnabled && variantIds(v).length === ids.length && ids.every((id) => variantIds(v).includes(id))
  ) || null
})

// A value is available if some enabled variant carries it together with the OTHER current selections.
function isAvailable (attr, valueId) {
  const others = optionAttributes.value
    .filter((a) => a.id !== attr.id)
    .map((a) => selected[a.id])
    .filter(Boolean)
  return props.variants.some((v) =>
    v.isEnabled && variantIds(v).includes(valueId) && others.every((id) => variantIds(v).includes(id))
  )
}

function selectedValueName (attr) {
  const val = (attr.values || []).find((v) => v.id === selected[attr.id])
  return val ? val.value : ''
}

// Swatch / button pick — clicking the active value clears it.
function pick (attr, valueId) {
  if (!isAvailable(attr, valueId)) return
  apply(attr, selected[attr.id] === valueId ? null : valueId)
}

// Dropdown pick — the select already carries the new value (or null when cleared).
function choose (attr, valueId) {
  if (valueId && !isAvailable(attr, valueId)) return
  apply(attr, valueId || null)
}

function apply (attr, valueId) {
  selected[attr.id] = valueId
  emit('update:modelValue', matched.value ? matched.value.id : null)
}

// ---- Custom inputs ----
function customPlaceholder (attr) {
  return attr.inputType === 'Number' ? 'Enter a number' : 'Enter your text'
}

function customHint (attr) {
  const max = Number(attr.maxLength) || 0
  if (!max) return undefined
  return attr.inputType === 'Number' ? `Up to ${max} digits` : `Up to ${max} characters`
}

function onCustomInput (attr, raw) {
  let value = raw == null ? '' : String(raw)
  const max = Number(attr.maxLength) || 0
  // type="number" ignores maxlength, and a pasted value can overshoot either way — trim to the limit.
  if (max > 0 && value.length > max) value = value.slice(0, max)
  customValues[attr.id] = value
  emit('update:customValues', { ...customValues })
}

function customError (attr) {
  if (!attr.isRequired || !touched[attr.id]) return ''
  return isBlank(customValues[attr.id]) ? `${attr.name} is required` : ''
}

function isBlank (value) { return !String(value ?? '').trim() }

// Called by the product page before add-to-cart: surfaces the errors and reports whether every
// mandatory custom input has been filled in. Optional ones may stay empty.
function validate () {
  let ok = true
  for (const attr of customAttributes.value) {
    touched[attr.id] = true
    if (attr.isRequired && isBlank(customValues[attr.id])) ok = false
  }
  return ok
}

defineExpose({ validate })

// A different product (or a changed attribute set) starts from a clean slate.
watch(
  () => props.attributes,
  () => {
    const ids = new Set(props.attributes.map((a) => a.id))
    for (const key of Object.keys(customValues)) if (!ids.has(key)) delete customValues[key]
    for (const key of Object.keys(touched)) if (!ids.has(key)) delete touched[key]
    emit('update:customValues', { ...customValues })
  }
)

// ---- Legacy fallback (no attribute metadata) ----
function variantLabel (v) {
  const name = v.sku || ('Variant ' + String(v.id).slice(0, 8))
  return v.price != null ? name + ' · ' + formatPrice(v.price) : name
}
function pickVariant (v) {
  if (!v.isEnabled) return
  emit('update:modelValue', props.modelValue === v.id ? null : v.id)
}

// Sync local selection when the parent sets/clears the variant externally (e.g. on product load).
function selectFromVariant (variant) {
  if (!variant) return
  for (const attr of optionAttributes.value) {
    const val = (attr.values || []).find((v) => variantIds(variant).includes(v.id))
    selected[attr.id] = val ? val.id : null
  }
}

watch(
  () => props.modelValue,
  (id) => {
    if (!id) {
      for (const k of Object.keys(selected)) selected[k] = null
      return
    }
    if (matched.value && matched.value.id === id) return
    selectFromVariant(props.variants.find((v) => v.id === id))
  },
  { immediate: true }
)
</script>

<style scoped lang="scss">
.vs-field {
  max-width: 320px;
}

.vs-swatch {
  width: 34px;
  height: 34px;
  border-radius: 50%;
  border: 2px solid var(--sf-border);
  padding: 0;
  cursor: pointer;
  position: relative;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  transition: box-shadow 0.2s ease, border-color 0.2s ease, transform 0.2s ease;
}
.vs-swatch:hover { transform: scale(1.08); }
.vs-swatch--active {
  border-color: var(--sf-accent);
  box-shadow: 0 0 0 2px var(--sf-accent);
}
.vs-swatch--disabled {
  opacity: 0.35;
  cursor: not-allowed;
}
.vs-swatch--disabled:hover { transform: none; }
.vs-swatch__check {
  color: #fff;
  filter: drop-shadow(0 0 1px rgba(0, 0, 0, 0.7));
}

.vs-option {
  padding: 7px 16px;
  border-radius: var(--sf-radius);
  border: 1px solid var(--sf-border);
  background: #fff;
  color: var(--sf-heading);
  font-size: 13px;
  font-weight: 600;
  cursor: pointer;
  transition: background 0.2s ease, border-color 0.2s ease, color 0.2s ease;
}
.vs-option:hover { border-color: var(--sf-accent); color: var(--sf-accent); }
.vs-option--active,
.vs-option--active:hover {
  background: var(--sf-accent);
  border-color: var(--sf-accent);
  color: #fff;
}
.vs-option--disabled,
.vs-option--disabled:hover {
  opacity: 0.4;
  cursor: not-allowed;
  text-decoration: line-through;
  border-color: var(--sf-border);
  color: var(--sf-heading);
  background: #fff;
}
</style>
