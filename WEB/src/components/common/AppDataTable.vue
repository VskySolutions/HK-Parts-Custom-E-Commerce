<template>
  <q-table
    class="app-table-card"
    flat
    bordered
    :rows="rows"
    :columns="computedColumns"
    :visible-columns="visibleColumnNames"
    :row-key="rowKey"
    :loading="loading"
    :pagination="pagination"
    :selection="selectable ? 'multiple' : 'none'"
    :selected="selected"
    binary-state-sort
    v-bind="$attrs"
    @request="onRequest"
    @update:selected="$emit('update:selected', $event)"
  >
    <!-- Forward all parent-provided slots (body-cell-*, header, ...). -->
    <template v-for="name in forwardedSlots" #[name]="slotProps" :key="name">
      <slot :name="name" v-bind="slotProps || {}" />
    </template>

    <!-- Standardized card header: a parent `top` slot wins; otherwise title + column tools + refresh. -->
    <template v-if="hasSlot('top') || title || columnTools" #top="slotProps">
      <slot name="top" v-bind="slotProps || {}">
        <div class="row items-center full-width no-wrap">
          <div class="app-section__title col ellipsis">{{ title }}</div>

          <q-btn
            v-if="columnTools"
            flat round dense
            icon="o_view_column"
            aria-label="Columns"
            class="q-mr-xs"
          >
            <q-tooltip>Show, hide, reorder &amp; resize columns</q-tooltip>
            <q-menu anchor="bottom right" self="top right">
              <div class="app-cols">
                <div class="app-cols__head row items-center no-wrap">
                  <div class="col text-weight-medium">Columns</div>
                  <q-btn flat dense no-caps size="sm" color="primary" label="Reset" @click="resetLayout" />
                </div>
                <div class="app-cols__hint">Drag to reorder · toggle to show/hide · drag a column edge to resize.</div>
                <q-list dense class="app-cols__list">
                  <q-item
                    v-for="(col, idx) in menuColumns"
                    :key="col.name"
                    class="app-cols__item"
                    :class="{ 'app-cols__item--drag': dragIndex === idx }"
                    draggable="true"
                    @dragstart="onDragStart(idx)"
                    @dragenter.prevent="onDragEnter(idx)"
                    @dragover.prevent
                    @dragend="onDragEnd"
                  >
                    <q-item-section side class="app-cols__grip">
                      <q-icon name="o_drag_indicator" size="18px" />
                    </q-item-section>
                    <q-item-section>
                      <q-checkbox
                        dense
                        :model-value="!isHidden(col.name)"
                        :label="col.label || col.name"
                        :disable="!isHidden(col.name) && visibleDataCount <= 1"
                        @update:model-value="toggleColumn(col.name, $event)"
                      />
                    </q-item-section>
                  </q-item>
                </q-list>
              </div>
            </q-menu>
          </q-btn>

          <q-btn flat round dense icon="o_refresh" :aria-label="refreshLabel" @click="$emit('refresh')">
            <q-tooltip>{{ refreshLabel }}</q-tooltip>
          </q-btn>
        </div>
      </slot>
    </template>

    <!-- Header cells: preserve q-th (alignment + click-to-sort) but render an always-visible sort
         indicator (neutral when sortable, brand arrow when active) plus a resize handle. -->
    <template v-if="columnTools" #header-cell="hp">
      <q-th
        :props="hp"
        class="app-th"
        :class="{ 'app-th--sortable': hp.col.sortable, 'app-th--sorted': isSorted(hp.col) }"
      >
        <span class="app-th__cell">
          <span class="app-th__label">{{ hp.col.label }}</span>
          <q-icon v-if="hp.col.sortable" :name="sortIcon(hp.col)" size="16px" class="app-th__sort" />
        </span>
        <span
          v-if="hp.col.name !== 'actions'"
          class="app-col-resizer"
          @mousedown.stop.prevent="startResize($event, hp.col)"
          @click.stop
          @dblclick.stop.prevent="clearWidth(hp.col)"
        />
      </q-th>
    </template>

    <!-- Actions column cell -->
    <template v-if="showActions" #body-cell-actions="cellProps">
      <q-td :props="cellProps" class="text-right">
        <slot name="actions" :row="cellProps.row" :props="cellProps">
          <q-btn flat round dense icon="o_more_vert" :aria-label="actionsLabel">
            <q-menu auto-close>
              <slot name="actions-menu" :row="cellProps.row" />
            </q-menu>
          </q-btn>
        </slot>
      </q-td>
    </template>

    <template v-if="!hasSlot('loading')" #loading>
      <q-inner-loading showing color="primary" />
    </template>

    <template v-if="!hasSlot('no-data')" #no-data>
      <div class="full-width row flex-center text-grey-7 q-py-lg">
        <q-icon name="o_inbox" size="22px" class="q-mr-sm" />
        {{ noDataLabel }}
      </div>
    </template>
  </q-table>
</template>

<script setup>
/*
 * AppDataTable (WO-94 Step 10): a q-table wrapper with server-side pagination
 * (@request), optional multi-select, an Actions column, and transparent
 * body-cell-* slot passthrough.
 *
 * Column tools (persisted per page-key via usePreferences, cookie pref_<pageKey>):
 *   - show/hide columns (visible-columns), remembered as hiddenColumns
 *   - reorder columns by drag in the Columns menu, remembered as columnOrder
 *   - resize columns by dragging a header edge (double-click to auto), remembered as columnWidths
 * All default to the table's declared layout, so nothing changes visually until a
 * user customizes. Sorting stays whatever each column declares (server-side sort is
 * per-endpoint). Set :column-tools="false" to opt a table out.
 */
import { ref, computed, useSlots, onBeforeUnmount } from 'vue'
import { usePreferences } from 'composables/usePreferences'

defineOptions({ inheritAttrs: false })

const props = defineProps({
  pageKey: { type: String, default: 'app-table' },
  rows: { type: Array, default: () => [] },
  columns: { type: Array, default: () => [] },
  loading: { type: Boolean, default: false },
  rowKey: { type: String, default: 'id' },
  pagination: {
    type: Object,
    default: () => ({ page: 1, rowsPerPage: 10, rowsNumber: 0 })
  },
  selectable: { type: Boolean, default: false },
  selected: { type: Array, default: () => [] },
  showActions: { type: Boolean, default: false },
  actionsLabel: { type: String, default: 'Actions' },
  noDataLabel: { type: String, default: 'No data available' },
  title: { type: String, default: '' },
  refreshLabel: { type: String, default: 'Refresh' },
  columnTools: { type: Boolean, default: true }
})

const emit = defineEmits(['request', 'update:pagination', 'update:selected', 'refresh'])

const slots = useSlots()
function hasSlot (name) {
  return Object.prototype.hasOwnProperty.call(slots, name)
}
// Never forward the slots this component renders itself. (loading/no-data stay forwardable: the parent
// can override them and our fallbacks are guarded by `!hasSlot(...)`.)
const RESERVED = ['actions', 'actions-menu', 'top', 'header-cell']
const forwardedSlots = computed(() => Object.keys(slots).filter((name) => !RESERVED.includes(name)))

const prefs = usePreferences(props.pageKey)

// ---- Column order / visibility / widths -----------------------------------
function joinStyle (base, extra) {
  if (!base) return extra
  if (typeof base === 'function') return (arg) => `${base(arg) || ''};${extra}`
  return `${base};${extra}`
}

// Parent data columns in the user's saved order (unknown names dropped; new columns kept in place).
const orderedDataColumns = computed(() => {
  const cols = props.columns || []
  const order = prefs.columnOrder.value
  if (!order || !order.length) return cols
  const byName = new Map(cols.map((c) => [c.name, c]))
  const result = []
  for (const name of order) {
    if (byName.has(name)) { result.push(byName.get(name)); byName.delete(name) }
  }
  for (const c of cols) { if (byName.has(c.name)) result.push(c) } // columns added since the pref was saved
  return result
})

// Apply a persisted width to a column (pins it via width/min/max so auto-layout honours the drag).
function withWidth (col) {
  const w = prefs.columnWidths.value[col.name]
  if (!w) return col
  const s = `width:${w}px;min-width:${w}px;max-width:${w}px`
  return { ...col, style: joinStyle(col.style, s), headerStyle: joinStyle(col.headerStyle, s) }
}

const computedColumns = computed(() => {
  const cols = orderedDataColumns.value.map(withWidth)
  if (props.showActions && !cols.some((c) => c.name === 'actions')) {
    cols.push({ name: 'actions', label: props.actionsLabel, field: 'actions', align: 'right', sortable: false })
  }
  return cols
})

// Columns offered in the picker (everything the parent declared; the Actions column isn't user-managed).
const menuColumns = computed(() => orderedDataColumns.value)

function isHidden (name) {
  return (prefs.hiddenColumns.value || []).includes(name)
}
const visibleDataCount = computed(() =>
  (props.columns || []).filter((c) => !isHidden(c.name)).length
)
// Actions is always visible; every non-hidden column (incl. any without column-tools) shows.
const visibleColumnNames = computed(() =>
  computedColumns.value.filter((c) => c.name === 'actions' || !isHidden(c.name)).map((c) => c.name)
)

function toggleColumn (name, visible) {
  const hidden = new Set(prefs.hiddenColumns.value || [])
  if (visible) hidden.delete(name)
  else {
    if (visibleDataCount.value <= 1) return // keep at least one column
    hidden.add(name)
  }
  prefs.hiddenColumns.value = [...hidden]
}

function resetLayout () {
  prefs.hiddenColumns.value = []
  prefs.columnOrder.value = null
  prefs.columnWidths.value = {}
}

// ---- Reorder (drag in the menu) -------------------------------------------
const dragIndex = ref(null)
function onDragStart (idx) { dragIndex.value = idx }
function onDragEnter (idx) {
  if (dragIndex.value === null || dragIndex.value === idx) return
  const order = menuColumns.value.map((c) => c.name)
  const [moved] = order.splice(dragIndex.value, 1)
  order.splice(idx, 0, moved)
  dragIndex.value = idx
  prefs.columnOrder.value = order
}
function onDragEnd () { dragIndex.value = null }

// ---- Resize (drag a header edge) ------------------------------------------
let resizing = null
function startResize (evt, col) {
  const th = evt.target.closest('th')
  const startWidth = th ? th.offsetWidth : (prefs.columnWidths.value[col.name] || 120)
  resizing = { name: col.name, startX: evt.clientX, startWidth }
  window.addEventListener('mousemove', onResizeMove)
  window.addEventListener('mouseup', endResize)
  document.body.style.cursor = 'col-resize'
  document.body.style.userSelect = 'none'
}
function onResizeMove (e) {
  if (!resizing) return
  const w = Math.max(60, Math.round(resizing.startWidth + (e.clientX - resizing.startX)))
  prefs.columnWidths.value = { ...prefs.columnWidths.value, [resizing.name]: w }
}
function endResize () {
  resizing = null
  window.removeEventListener('mousemove', onResizeMove)
  window.removeEventListener('mouseup', endResize)
  document.body.style.cursor = ''
  document.body.style.userSelect = ''
}
// Double-click the handle → clear the custom width (back to auto).
function clearWidth (col) {
  if (!prefs.columnWidths.value[col.name]) return
  const next = { ...prefs.columnWidths.value }
  delete next[col.name]
  prefs.columnWidths.value = next
}
onBeforeUnmount(endResize)

// ---- Sort indicator (always visible; reflects the active server-side sort) ------
function isSorted (col) {
  return !!props.pagination && props.pagination.sortBy === col.name
}
function sortIcon (col) {
  if (!isSorted(col)) return 'o_unfold_more' // neutral "sortable" affordance
  return props.pagination.descending ? 'o_arrow_downward' : 'o_arrow_upward'
}

// ---- Server-side request passthrough --------------------------------------
function onRequest (reqProps) {
  if (reqProps && reqProps.pagination) {
    emit('update:pagination', reqProps.pagination)
    if (reqProps.pagination.rowsPerPage) {
      prefs.pageSize.value = reqProps.pagination.rowsPerPage
    }
    // Remember the active sort so it's available to endpoints that support it.
    if (reqProps.pagination.sortBy !== undefined) prefs.sortBy.value = reqProps.pagination.sortBy
    if (reqProps.pagination.descending !== undefined) prefs.descending.value = reqProps.pagination.descending
  }
  emit('request', reqProps)
}
</script>

<style scoped lang="scss">
// Header resize handle (q-th gets position:relative so the handle can pin to its right edge).
:deep(.app-th) {
  position: relative;
}
.app-col-resizer {
  position: absolute;
  top: 0;
  right: 0;
  width: 7px;
  height: 100%;
  cursor: col-resize;
  z-index: 1;
}
.app-col-resizer:hover {
  background: rgba(25, 118, 210, 0.35);
}

// ---- Header design: subtle background, bolder labels, column separators ----
:deep(.app-table-card thead th) {
  background: #f6f7f9;
  color: #45464f;
  font-weight: 600;
  border-bottom: 1px solid rgba(0, 0, 0, 0.12);
}
:deep(.app-table-card thead th:not(:last-child)) {
  border-right: 1px solid rgba(0, 0, 0, 0.06);
}
:deep(.app-table-card thead th.sortable:hover) {
  background: #eceef1;
}

// Always-visible sort indicator (replaces q-th's hover-only native icon inside our custom header cell).
:deep(.app-th .q-table__sort-icon) {
  display: none;
}
.app-th__cell {
  display: inline-flex;
  align-items: center;
  gap: 4px;
}
.app-th__sort {
  opacity: 0.3;
  transition: opacity 0.2s ease, color 0.2s ease;
}
.app-th--sortable:hover .app-th__sort {
  opacity: 0.65;
}
.app-th--sorted .app-th__sort {
  opacity: 1;
  color: var(--q-primary);
}
// Fallback for tables that opt out of column tools: keep the native sort icon visible (not hover-only).
:deep(.app-table-card thead th.sortable .q-table__sort-icon) {
  opacity: 0.4;
}

// Columns menu
.app-cols {
  min-width: 240px;
  padding: 6px 4px 4px;
}
.app-cols__head {
  padding: 4px 10px;
}
.app-cols__hint {
  padding: 0 10px 6px;
  font-size: 11px;
  color: var(--q-color-grey-6, #9a9aa2);
}
.app-cols__list {
  max-height: 320px;
  overflow-y: auto;
}
.app-cols__item {
  cursor: grab;
  border-radius: 4px;
}
.app-cols__item--drag {
  background: rgba(25, 118, 210, 0.08);
}
.app-cols__grip {
  min-width: 0;
  padding-right: 4px;
  color: #b0b0b8;
}
</style>
