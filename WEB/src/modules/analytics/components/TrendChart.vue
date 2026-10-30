<template>
  <div class="trend-chart">
    <!-- Series toggle + legend -->
    <div class="row items-center q-mb-md">
      <q-btn-toggle
        v-model="mode"
        no-caps
        unelevated
        dense
        toggle-color="primary"
        color="grey-3"
        text-color="grey-8"
        :options="[
          { label: 'Both', value: 'both' },
          { label: 'Orders', value: 'orders' },
          { label: 'Revenue', value: 'revenue' }
        ]"
      />
      <q-space />
      <div class="row items-center q-gutter-md">
        <div v-if="mode !== 'revenue'" class="legend-item">
          <span class="legend-swatch" :style="{ background: ORDERS_COLOR }" /> Orders
        </div>
        <div v-if="mode !== 'orders'" class="legend-item">
          <span class="legend-swatch" :style="{ background: REVENUE_COLOR }" /> Revenue
        </div>
      </div>
    </div>

    <q-inner-loading :showing="loading" color="primary" />

    <div v-if="!hasData" class="trend-empty text-grey-6">
      <q-icon name="o_show_chart" size="26px" class="q-mr-sm" />
      No sales activity in this period.
    </div>

    <svg
      v-else
      class="trend-svg"
      :viewBox="`0 0 ${VBW} ${VBH}`"
      preserveAspectRatio="xMidYMid meet"
      role="img"
      aria-label="Sales trend chart"
    >
      <!-- Horizontal grid lines (+ y-axis labels in single-series mode) -->
      <g>
        <line
          v-for="(g, i) in gridLines"
          :key="'g' + i"
          class="trend-grid"
          :x1="padL"
          :x2="VBW - padR"
          :y1="g.y"
          :y2="g.y"
        />
        <template v-if="showAxis">
          <text
            v-for="(g, i) in gridLines"
            :key="'yl' + i"
            class="trend-axis"
            :x="padL - 8"
            :y="g.y + 4"
            text-anchor="end"
          >{{ g.label }}</text>
        </template>
      </g>

      <!-- X-axis labels -->
      <text
        v-for="(t, i) in xLabels"
        :key="'x' + i"
        class="trend-axis"
        :x="t.x"
        :y="VBH - 12"
        text-anchor="middle"
      >{{ t.label }}</text>

      <!-- Revenue series -->
      <template v-if="mode !== 'orders'">
        <polygon v-if="mode === 'revenue'" :points="revenueArea" :fill="REVENUE_COLOR" opacity="0.08" />
        <polyline :points="revenueLine" fill="none" :stroke="REVENUE_COLOR" stroke-width="2.5" stroke-linejoin="round" stroke-linecap="round" />
        <circle v-for="(p, i) in revenuePts" :key="'r' + i" :cx="p.x" :cy="p.y" r="3.5" :fill="REVENUE_COLOR">
          <title>{{ p.title }}</title>
        </circle>
      </template>

      <!-- Orders series -->
      <template v-if="mode !== 'revenue'">
        <polygon v-if="mode === 'orders'" :points="ordersArea" :fill="ORDERS_COLOR" opacity="0.08" />
        <polyline :points="ordersLine" fill="none" :stroke="ORDERS_COLOR" stroke-width="2.5" stroke-linejoin="round" stroke-linecap="round" />
        <circle v-for="(p, i) in ordersPts" :key="'o' + i" :cx="p.x" :cy="p.y" r="3.5" :fill="ORDERS_COLOR">
          <title>{{ p.title }}</title>
        </circle>
      </template>
    </svg>
  </div>
</template>

<script setup>
/*
 * TrendChart (WO-59): a dependency-free, responsive inline SVG chart of the sales trend.
 * Renders two self-normalised line series (orders + revenue) over the selected period with a
 * Both / Orders / Revenue toggle; single-series mode adds a light area fill and a numeric y-axis.
 * Data points carry native SVG <title> tooltips. No chart library is used — points are computed
 * from the data and scaled to a fixed viewBox, made fluid via viewBox + preserveAspectRatio.
 */
import { ref, computed } from 'vue'
import { formatDate } from 'src/utils/datetime'

const props = defineProps({
  data: { type: Array, default: () => [] },
  loading: { type: Boolean, default: false }
})

// Fixed drawing surface — the SVG scales to its container via viewBox + width:100%.
const VBW = 820
const VBH = 300
const padL = 54
const padR = 18
const padT = 18
const padB = 46
const plotW = VBW - padL - padR
const plotH = VBH - padT - padB

const ORDERS_COLOR = '#1976d2'
const REVENUE_COLOR = '#26a69a'

const mode = ref('both')

const rows = computed(() => (Array.isArray(props.data) ? props.data.filter((d) => d) : []))
const hasData = computed(() => rows.value.length > 0)
const showAxis = computed(() => mode.value !== 'both')

function xFor (i) {
  const n = rows.value.length
  if (n <= 1) return padL + plotW / 2
  return padL + (i / (n - 1)) * plotW
}
function yFor (v, max) {
  return padT + plotH - (Math.max(0, Number(v) || 0) / max) * plotH
}
const round = (n) => Math.round(n * 10) / 10

const maxOrders = computed(() => Math.max(1, ...rows.value.map((d) => Number(d.orderCount) || 0)))
const maxRevenue = computed(() => Math.max(1, ...rows.value.map((d) => Number(d.revenue) || 0)))

const ordersPts = computed(() =>
  rows.value.map((d, i) => ({
    x: round(xFor(i)),
    y: round(yFor(d.orderCount, maxOrders.value)),
    title: `${formatDate(d.date)} · ${Number(d.orderCount) || 0} orders`
  }))
)
const revenuePts = computed(() =>
  rows.value.map((d, i) => ({
    x: round(xFor(i)),
    y: round(yFor(d.revenue, maxRevenue.value)),
    title: `${formatDate(d.date)} · ${money(d.revenue)} revenue`
  }))
)

const ordersLine = computed(() => ordersPts.value.map((p) => `${p.x},${p.y}`).join(' '))
const revenueLine = computed(() => revenuePts.value.map((p) => `${p.x},${p.y}`).join(' '))

function areaFor (pts) {
  if (!pts.length) return ''
  const base = padT + plotH
  const first = pts[0]
  const last = pts[pts.length - 1]
  return `${first.x},${base} ${pts.map((p) => `${p.x},${p.y}`).join(' ')} ${last.x},${base}`
}
const ordersArea = computed(() => areaFor(ordersPts.value))
const revenueArea = computed(() => areaFor(revenuePts.value))

// 5 gridlines; labels reflect the emphasised series' own scale (only shown in single-series mode).
const gridLines = computed(() => {
  const ticks = 4
  const isRev = mode.value === 'revenue'
  const max = isRev ? maxRevenue.value : maxOrders.value
  const out = []
  for (let i = 0; i <= ticks; i++) {
    const y = padT + (i / ticks) * plotH
    const val = max * (1 - i / ticks)
    out.push({ y, label: isRev ? compactMoney(val) : String(Math.round(val)) })
  }
  return out
})

// Thin the x-axis to at most ~7 labels; always include the last point.
const xLabels = computed(() => {
  const s = rows.value
  if (!s.length) return []
  const step = Math.max(1, Math.ceil(s.length / 7))
  const out = []
  for (let i = 0; i < s.length; i += step) out.push({ x: round(xFor(i)), label: shortDate(s[i].date) })
  if ((s.length - 1) % step !== 0) out.push({ x: round(xFor(s.length - 1)), label: shortDate(s[s.length - 1].date) })
  return out
})

// Short M/D label straight off the yyyy-MM-dd string (no timezone shifting).
function shortDate (v) {
  if (!v) return ''
  const parts = String(v).slice(0, 10).split('-')
  return parts.length === 3 ? `${Number(parts[1])}/${Number(parts[2])}` : String(v)
}
function money (v) {
  const n = Number(v) || 0
  return n.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}
function compactMoney (v) {
  const n = Number(v) || 0
  if (n >= 1000000) return `${(n / 1000000).toFixed(1)}M`
  if (n >= 1000) return `${(n / 1000).toFixed(1)}k`
  return String(Math.round(n))
}
</script>

<style scoped lang="scss">
.trend-chart {
  position: relative;
  min-height: 260px;
}
.trend-svg {
  width: 100%;
  height: auto;
  display: block;
}
.trend-grid {
  stroke: rgba(0, 0, 0, 0.08);
  stroke-width: 1;
}
.trend-axis {
  fill: #9096a1;
  font-size: 11px;
}
.trend-empty {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 240px;
}
.legend-item {
  display: inline-flex;
  align-items: center;
  font-size: 12px;
  color: #6b6c76;
}
.legend-swatch {
  display: inline-block;
  width: 12px;
  height: 12px;
  border-radius: 3px;
  margin-right: 6px;
}
</style>
