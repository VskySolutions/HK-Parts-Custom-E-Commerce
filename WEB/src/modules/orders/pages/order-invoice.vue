<template>
  <!-- Printable invoice (the PHP admin's /orders/invoice/{id} page). Standalone: no admin shell, so it prints
       cleanly. Same data as the PDF download (GET /api/admin/orders/{id}/invoice/view). -->
  <div class="inv-page">
    <div class="inv-toolbar no-print">
      <q-btn flat dense no-caps icon="o_arrow_back" label="Back to order" :to="{ name: 'admin-order-detail', params: { id: route.params.id } }" />
      <q-space />
      <q-btn flat dense no-caps icon="o_print" label="Print" :disable="!inv" @click="print" />
      <q-btn unelevated dense no-caps color="primary" icon="o_download" label="Invoice Download" class="q-ml-sm q-px-sm" :loading="downloading" :disable="!inv" @click="download" />
    </div>

    <div v-if="loading" class="inv-state"><q-spinner size="32px" color="primary" /></div>
    <div v-else-if="error" class="inv-state text-negative">{{ error }}</div>

    <div v-else-if="inv" class="inv">
      <h1 class="inv-title">INVOICE</h1>

      <div class="inv-meta">
        <div>Order #: {{ inv.orderNumber }}</div>
        <div>Order Date: {{ inv.orderDateText }}</div>
      </div>

      <div class="inv-panels">
        <div class="inv-panel">
          <div class="inv-panel__head">Billing Address</div>
          <div class="inv-panel__body">
            <div v-for="(l, i) in addressLines(inv.billing)" :key="i">{{ l }}</div>
            <div v-if="inv.paymentMethod" class="inv-gap">Payment Method: {{ inv.paymentMethod }}</div>
          </div>
        </div>
        <div class="inv-panel">
          <div class="inv-panel__head">{{ inv.isPickup ? 'Pickup' : inv.shipsToFflDealer ? 'Shipping Address (FFL Dealer)' : 'Shipping Address' }}</div>
          <div class="inv-panel__body">
            <div v-for="(l, i) in addressLines(inv.shipping)" :key="i">{{ l }}</div>
            <div v-if="inv.shipsToFflDealer && inv.fflNumber">FFL Number: {{ inv.fflNumber }}</div>
            <div v-if="inv.shippingMethod" :class="{ 'inv-gap': inv.shipping }">Shipping Method: {{ inv.shippingMethod }}</div>
            <template v-if="inv.fflDealer">
              <div class="inv-gap text-weight-bold">FFL items ship to:</div>
              <div v-for="(l, i) in addressLines(inv.fflDealer)" :key="'f' + i">{{ l }}</div>
              <div v-if="inv.fflNumber">FFL Number: {{ inv.fflNumber }}</div>
              <div v-if="inv.fflShippingMethod">Shipping Method: {{ inv.fflShippingMethod }}</div>
            </template>
          </div>
        </div>
      </div>

      <table class="inv-table">
        <thead>
          <tr>
            <th style="width: 13%">SKU</th>
            <th>Name</th>
            <th style="width: 10%">Qty</th>
            <th style="width: 14%">Price</th>
            <th style="width: 20%">Sub Total</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="(l, i) in inv.lines" :key="i">
            <td>{{ l.sku }}</td>
            <td>
              {{ l.name }}
              <div v-if="l.options" class="inv-options">{{ l.options }}</div>
            </td>
            <td class="num">{{ l.quantity }}</td>
            <td class="num">{{ money(l.price) }}</td>
            <td class="num">{{ money(l.subTotal) }}</td>
          </tr>
        </tbody>
      </table>

      <table class="inv-totals">
        <tbody>
          <tr v-for="t in totals" :key="t.label">
            <th>{{ t.label }}</th>
            <td>{{ t.value }}</td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { orderApi } from 'modules/orders/api'
import { getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'

const route = useRoute()
const notify = useNotify()

const inv = ref(null)
const loading = ref(true)
const error = ref('')
const downloading = ref(false)

// "$249.99" for USD (as the PHP invoice and the PDF), else "EUR 249.99".
function money (v) {
  const n = Math.abs(Number(v) || 0).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
  const sign = Number(v) < 0 ? '-' : ''
  const code = inv.value?.currencyCode || 'USD'
  return code.toUpperCase() === 'USD' ? `${sign}$${n}` : `${sign}${code} ${n}`
}

function addressLines (a) {
  if (!a) return []
  return [a.name, a.company, a.line1, a.line2, a.cityLine, a.phone].filter((x) => x && String(x).trim())
}

const totals = computed(() => {
  const i = inv.value
  if (!i) return []
  const rows = [{ label: 'Sub Total:', value: money(i.subTotal) }]
  if (i.discount > 0) rows.push({ label: 'Discount:', value: money(-i.discount) })
  rows.push({ label: 'Shipping:', value: money(i.shippingTotal) })
  rows.push({ label: 'TAX:', value: money(i.tax) })
  if (i.paymentFee > 0) rows.push({ label: 'Payment Fee:', value: money(i.paymentFee) })
  rows.push({ label: 'Total:', value: money(i.total) })
  rows.push({ label: 'Payment Applied:', value: money(i.paymentApplied) })
  if (i.refunded > 0) rows.push({ label: 'Refunded:', value: money(i.refunded) })
  rows.push({ label: 'Amount Due:', value: money(i.amountDue) })
  return rows
})

function print () { window.print() }

async function download () {
  downloading.value = true
  try {
    await orderApi.invoice(route.params.id, inv.value?.orderNumber)
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    downloading.value = false
  }
}

onMounted(async () => {
  try {
    inv.value = await orderApi.invoiceView(route.params.id)
    document.title = `Invoice ${inv.value.orderNumber}`
  } catch (err) {
    error.value = getApiErrorMessage(err)
  } finally {
    loading.value = false
  }
})
</script>

<style scoped>
.inv-page {
  min-height: 100vh;
  background: #fff;
  color: #333;
  font-family: 'Times New Roman', Times, serif;
}
.inv-toolbar {
  display: flex;
  align-items: center;
  max-width: 1140px;
  margin: 0 auto;
  padding: 10px 16px;
  border-bottom: 1px solid #eee;
  font-family: Roboto, -apple-system, 'Segoe UI', sans-serif;
}
.inv-state { padding: 80px 16px; text-align: center; }
.inv {
  max-width: 1140px;
  margin: 0 auto;
  padding: 16px 16px 48px;
  font-size: 14px;
}
.inv-title {
  margin: 8px 0 40px;
  text-align: right;
  font-size: 36px;
  font-weight: 400;
  line-height: 1.2;
}
.inv-meta {
  display: flex;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: 8px;
  margin-bottom: 12px;
  color: #777;
  font-size: 22px;
}
.inv-panels {
  display: flex;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: 20px;
  margin-bottom: 20px;
}
.inv-panel {
  flex: 0 1 40%;
  min-width: 260px;
  border: 1px solid #ddd;
  border-radius: 4px;
  box-shadow: 0 1px 1px rgba(0, 0, 0, 0.05);
}
.inv-panel__head {
  padding: 18px 15px;
  background: #f5f5f5;
  border-bottom: 1px solid #ddd;
  font-size: 18px;
}
.inv-panel__body { padding: 15px; line-height: 1.45; }
.inv-gap { margin-top: 20px; }
.inv-table {
  width: 100%;
  border-collapse: collapse;
  margin-bottom: 20px;
}
.inv-table th,
.inv-table td {
  border: 1px solid #ddd;
  padding: 8px;
  vertical-align: top;
  text-align: left;
}
.inv-table th {
  font-size: 18px;
  font-weight: 400;
  padding: 16px 8px;
  border-bottom-width: 2px;
}
.inv-table td.num { text-align: right; }
.inv-options { font-size: 12px; color: #777; }
.inv-totals {
  margin-left: auto;
  border-collapse: collapse;
  font-weight: 700;
}
.inv-totals th,
.inv-totals td { padding: 1px 0; text-align: right; }
.inv-totals th { padding-right: 24px; }
.inv-totals td { min-width: 150px; }

@media (max-width: 600px) {
  .inv-panel { flex-basis: 100%; }
  .inv-meta { font-size: 18px; }
  .inv-table th { font-size: 15px; }
}

@media print {
  .no-print { display: none !important; }
  .inv { max-width: none; padding: 0; }
  .inv-panel { box-shadow: none; }
}
</style>
