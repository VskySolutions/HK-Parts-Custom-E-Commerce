<template>
  <q-page class="app-page">
    <AppDetailHeader
      :breadcrumbs="[
        { label: 'Home', icon: 'o_home', to: '/dashboard' },
        { label: 'Returns', to: '/returns' },
        { label: rma ? rma.rmaNumber : 'RMA' }
      ]"
      :status="rma ? humanizeEnum(rma.status) : ''"
      :status-color="rma ? rmaStatusColor(rma.status) : 'grey'"
      @back="$router.push({ name: 'admin-rmas' })"
    />

    <q-inner-loading :showing="loading" />

    <div v-if="rma" class="row q-col-gutter-md">
      <!-- ============================== main column ============================== -->
      <div class="col-12 col-md-8">
        <q-banner v-if="rma.lastRefundError" dense rounded class="bg-red-1 text-negative q-mb-md">
          <template #avatar><q-icon name="o_error" /></template>
          Refund attempt {{ rma.refundAttempts }} failed: {{ rma.lastRefundError }}
        </q-banner>
        <q-banner v-if="rma.requiresFfl && !rma.fflNumber && isOpen" dense rounded class="bg-orange-1 text-deep-orange q-mb-md">
          <template #avatar><q-icon name="o_gavel" /></template>
          Firearm return: record the returning dealer's FFL before approving. No self-service label is offered.
        </q-banner>

        <AppSection title="Return details">
          <div class="row q-col-gutter-md text-body2">
            <div class="col-6 col-sm-4">
              <div class="text-caption text-grey-7">Order</div>
              <a class="text-primary cursor-pointer" @click="openOrder(rma.orderId)">{{ rma.orderNumber || 'View order' }}</a>
            </div>
            <div class="col-6 col-sm-4">
              <div class="text-caption text-grey-7">Customer</div>
              {{ rma.customerName || 'Guest' }}<div class="text-caption">{{ rma.customerEmail }}</div>
            </div>
            <div class="col-6 col-sm-4"><div class="text-caption text-grey-7">Requested</div>{{ formatDate(rma.requestedOnUtc) }} · {{ humanizeEnum(rma.source) }}</div>
            <div class="col-6 col-sm-4"><div class="text-caption text-grey-7">Customer wants</div>{{ resolutionLabel(rma.requestedResolution) }}</div>
            <div class="col-6 col-sm-4"><div class="text-caption text-grey-7">Return method</div>{{ methodLabel(rma.returnMethod) }}</div>
            <div class="col-6 col-sm-4"><div class="text-caption text-grey-7">In status for</div>{{ rma.daysInStatus }} day(s)</div>
            <div v-if="rma.resolution !== 'None'" class="col-6 col-sm-4"><div class="text-caption text-grey-7">Resolution</div>{{ resolutionLabel(rma.resolution) }}</div>
            <div v-if="rma.replacementOrderId" class="col-6 col-sm-4">
              <div class="text-caption text-grey-7">Replacement order</div>
              <a class="text-primary cursor-pointer" @click="openOrder(rma.replacementOrderId)">{{ rma.replacementOrderNumber || 'View order' }}</a>
            </div>
            <div v-if="rma.shipToSummary" class="col-12 col-sm-8"><div class="text-caption text-grey-7">Ship-from (customer)</div>{{ rma.shipToSummary }}</div>
            <div class="col-12"><div class="text-caption text-grey-7">Customer comment</div>{{ rma.reason || '—' }}</div>
            <div v-if="rma.resolutionNotes" class="col-12"><div class="text-caption text-grey-7">Customer-visible note</div>{{ rma.resolutionNotes }}</div>
            <div v-if="rma.eligibilityOverrideReason" class="col-12">
              <div class="text-caption text-grey-7">Policy override</div>{{ rma.eligibilityOverrideReason }}
            </div>
            <template v-if="rma.requiresFfl">
              <div class="col-6 col-sm-4"><div class="text-caption text-grey-7">Returning dealer</div>{{ rma.fflDealerName || '—' }}</div>
              <div class="col-6 col-sm-4"><div class="text-caption text-grey-7">FFL #</div>{{ rma.fflNumber || '—' }}</div>
              <div class="col-6 col-sm-4"><div class="text-caption text-grey-7">FFL expiry</div>{{ rma.fflExpiry ? formatDay(rma.fflExpiry) : '—' }}</div>
            </template>
          </div>
        </AppSection>

        <AppSection title="Items" class="q-mt-md">
          <q-markup-table flat dense separator="horizontal">
            <thead>
              <tr>
                <th class="text-left">Product</th>
                <th class="text-left">Reason</th>
                <th class="text-right">Req.</th>
                <th class="text-right">Recv.</th>
                <th class="text-right">Accepted</th>
                <th class="text-right">Restocked</th>
                <th class="text-left">Condition</th>
                <th class="text-right">Refund</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="l in rma.lines" :key="l.id">
                <td>
                  <div class="text-weight-medium">{{ l.productName }}</div>
                  <div class="text-caption text-grey-7">{{ l.sku || '' }}</div>
                  <div v-if="l.customerComment" class="text-caption text-italic">“{{ l.customerComment }}”</div>
                </td>
                <td>
                  {{ l.lineReason || '—' }}
                  <q-badge v-if="l.merchantFault" outline color="positive" label="our fault" class="q-ml-xs" />
                  <q-badge v-if="l.feeApplies && !l.merchantFault" outline color="grey-7" label="fee" class="q-ml-xs" />
                </td>
                <td class="text-right">{{ l.quantity }}</td>
                <td class="text-right">{{ l.quantityReceived }}</td>
                <td class="text-right">{{ l.quantityAccepted }}</td>
                <td class="text-right">{{ l.quantityRestocked }}</td>
                <td>
                  <div v-if="l.conditionReported" class="text-caption">Customer: {{ humanizeEnum(l.conditionReported) }}</div>
                  <div v-if="l.conditionReceived">{{ humanizeEnum(l.conditionReceived) }}<span v-if="l.disposition"> → {{ humanizeEnum(l.disposition) }}</span></div>
                </td>
                <td class="text-right">
                  {{ formatMoney(l.lineRefund || l.unitRefund * l.quantity) }}
                  <div v-if="l.deduction" class="text-caption text-negative">−{{ formatMoney(l.deduction) }} {{ l.deductionReason }}</div>
                </td>
              </tr>
            </tbody>
          </q-markup-table>
        </AppSection>

        <AppSection title="Return shipping" class="q-mt-md">
          <div class="row q-col-gutter-md text-body2">
            <div class="col-6 col-sm-3"><div class="text-caption text-grey-7">Carrier</div>{{ rma.labelCarrier || '—' }} {{ rma.labelService || '' }}</div>
            <div class="col-6 col-sm-3"><div class="text-caption text-grey-7">Tracking</div>{{ rma.trackingNumber || '—' }}</div>
            <div class="col-6 col-sm-3">
              <div class="text-caption text-grey-7">Label</div>
              <a v-if="rma.labelUrl" :href="rma.labelUrl" target="_blank" rel="noopener" class="text-primary">Open label</a><span v-else>—</span>
            </div>
            <div class="col-6 col-sm-3"><div class="text-caption text-grey-7">Label cost</div>{{ rma.labelCost != null ? formatMoney(rma.labelCost) : '—' }}</div>
            <div v-if="rma.labelExpiresAtUtc && ['Approved'].includes(rma.status)" class="col-6 col-sm-3">
              <div class="text-caption text-grey-7">Expires</div>{{ formatDate(rma.labelExpiresAtUtc) }}
            </div>
            <div v-if="rma.returnInstructions" class="col-12"><div class="text-caption text-grey-7">Instructions to customer</div>{{ rma.returnInstructions }}</div>
          </div>
        </AppSection>

        <AppSection title="Messages" class="q-mt-md">
          <div v-if="!rma.messages.length" class="text-grey-6 q-mb-sm">No messages yet.</div>
          <q-chat-message
            v-for="m in rma.messages" :key="m.id"
            :name="(m.authorName || humanizeEnum(m.authorType)) + (m.isInternal ? ' · internal note' : '')"
            :text="[m.body]"
            :stamp="formatDate(m.createdOnUtc)"
            :sent="m.authorType === 'Admin'"
            :bg-color="m.isInternal ? 'yellow-2' : (m.authorType === 'Admin' ? 'blue-1' : 'grey-3')"
          />
          <q-input v-model="message.body" type="textarea" autogrow dense outlined label="Write a message or internal note" class="q-mt-sm" maxlength="4000" />
          <div class="row items-center q-gutter-sm q-mt-xs">
            <q-toggle v-model="message.isInternal" label="Internal note (never shown to the customer)" dense />
            <q-toggle v-if="!message.isInternal" v-model="message.notify" label="Email the customer" dense />
            <q-space />
            <q-btn unelevated color="primary" no-caps label="Add" :disable="!message.body.trim()" :loading="busy" @click="sendMessage" />
          </div>
        </AppSection>

        <AppSection title="Photos & files" class="q-mt-md">
          <div v-if="!rma.attachments.length" class="text-grey-6">No files.</div>
          <q-list dense>
            <q-item v-for="a in rma.attachments" :key="a.id" clickable @click="openAttachment(a)">
              <q-item-section avatar><q-icon :name="a.mimeType.startsWith('image/') ? 'o_image' : 'o_description'" /></q-item-section>
              <q-item-section>
                <q-item-label>{{ a.originalFileName }}</q-item-label>
                <q-item-label caption>{{ humanizeEnum(a.uploadedByType) }} · {{ formatDate(a.createdOnUtc) }} · {{ Math.ceil(a.sizeBytes / 1024) }} KB</q-item-label>
              </q-item-section>
            </q-item>
          </q-list>
          <q-file v-model="upload" dense outlined label="Add photo or PDF (max 10 MB)" accept=".jpg,.jpeg,.png,.webp,.heic,.pdf" class="q-mt-sm" @update:model-value="uploadFile">
            <template #prepend><q-icon name="o_attach_file" /></template>
          </q-file>
        </AppSection>

        <AppSection title="History" class="q-mt-md">
          <q-timeline color="primary" layout="dense">
            <q-timeline-entry
              v-for="h in [...rma.history].reverse()" :key="h.id"
              :title="h.fromStatus !== h.toStatus ? `${humanizeEnum(h.fromStatus) || 'New'} → ${humanizeEnum(h.toStatus)}` : 'Note'"
              :subtitle="`${formatDate(h.createdOnUtc)} · ${h.actorName || humanizeEnum(h.actorType)}`"
              :icon="h.fromStatus !== h.toStatus ? 'o_sync_alt' : 'o_notes'"
            >
              <div v-if="h.note" class="text-body2">{{ h.note }}</div>
            </q-timeline-entry>
          </q-timeline>
        </AppSection>
      </div>

      <!-- ============================== side column ============================== -->
      <div class="col-12 col-md-4">
        <AppSection title="Refund">
          <q-badge v-if="rma.refund.isEstimate" color="orange" outline label="Estimate — confirmed after inspection" class="q-mb-sm" />
          <q-markup-table flat dense>
            <tbody>
              <tr><td>Items</td><td class="text-right">{{ formatMoney(rma.refund.subtotal) }}</td></tr>
              <tr><td>Tax</td><td class="text-right">{{ formatMoney(rma.refund.tax) }}</td></tr>
              <tr v-if="rma.refund.shipping"><td>Shipping</td><td class="text-right">{{ formatMoney(rma.refund.shipping) }}</td></tr>
              <tr v-if="rma.refund.restockFee"><td>Restocking fee</td><td class="text-right text-negative">−{{ formatMoney(rma.refund.restockFee) }}</td></tr>
              <tr v-if="rma.refund.labelDeduction"><td>Return label</td><td class="text-right text-negative">−{{ formatMoney(rma.refund.labelDeduction) }}</td></tr>
              <tr v-if="rma.refund.adjustment"><td>Adjustment<div class="text-caption">{{ rma.adjustmentReason }}</div></td><td class="text-right">{{ formatMoney(rma.refund.adjustment) }}</td></tr>
              <tr class="text-weight-bold"><td>Total</td><td class="text-right">{{ rma.currencyCode }} {{ formatMoney(rma.refund.total) }}</td></tr>
              <tr v-if="rma.refundedAmount"><td>Refunded</td><td class="text-right text-positive">{{ formatMoney(rma.refundedAmount) }}</td></tr>
              <tr v-if="rma.storeCreditIssued"><td>Store credit issued</td><td class="text-right text-positive">{{ formatMoney(rma.storeCreditIssued) }}</td></tr>
            </tbody>
          </q-markup-table>
          <div v-if="rma.refundReference" class="text-caption text-grey-7 q-mt-xs">{{ humanizeEnum(rma.refundMethod) }}: {{ rma.refundReference }}</div>
          <div class="text-caption text-grey-7 q-mt-xs">Refundable on the order's payments: {{ formatMoney(rma.orderRefundableBalance) }}</div>
        </AppSection>

        <AppSection title="Actions" class="q-mt-md">
          <div class="column q-gutter-sm">
            <q-btn v-if="can('approve')" color="positive" unelevated no-caps icon="o_check" label="Approve" @click="openDialog('approve')" />
            <q-btn v-if="can('setFfl')" color="deep-orange" outline no-caps icon="o_gavel" label="Returning dealer (FFL)" @click="openDialog('setFfl')" />
            <q-btn v-if="can('requestInfo')" color="primary" outline no-caps icon="o_help_outline" label="Ask customer for info" @click="openDialog('requestInfo')" />
            <q-btn v-if="can('setLabel')" color="primary" outline no-caps icon="o_local_shipping" label="Return label / shipping" @click="openDialog('setLabel')" />
            <q-btn v-if="can('markInTransit')" color="primary" outline no-caps icon="o_flight_takeoff" label="Mark in transit" @click="openDialog('markInTransit')" />
            <q-btn v-if="can('receive')" color="primary" unelevated no-caps icon="o_inventory_2" :label="rma.status === 'Received' ? 'Correct receipt' : 'Receive package'" @click="openDialog('receive')" />
            <q-btn v-if="can('inspect')" color="primary" unelevated no-caps icon="o_fact_check" :label="rma.status === 'Inspected' ? 'Re-inspect' : 'Inspect'" @click="openDialog('inspect')" />
            <q-btn v-if="can('refund')" color="positive" unelevated no-caps icon="o_payments" label="Send refund" @click="openDialog('refund')" />
            <q-btn v-if="can('storeCredit')" color="positive" outline no-caps icon="o_card_giftcard" label="Issue store credit" @click="openDialog('storeCredit')" />
            <q-btn v-if="can('exchange')" color="primary" outline no-caps icon="o_swap_horiz" label="Exchange (replacement order)" @click="openDialog('exchange')" />
            <q-btn v-if="can('editLines')" color="grey-8" flat no-caps icon="o_edit" label="Edit quantities" @click="openDialog('editLines')" />
            <q-btn v-if="can('close')" color="grey-8" outline no-caps icon="o_task_alt" label="Close" @click="openDialog('close')" />
            <q-btn v-if="can('reopen')" color="grey-8" outline no-caps icon="o_restart_alt" label="Reopen" @click="openDialog('reopen')" />
            <q-btn v-if="can('reject')" color="negative" outline no-caps icon="o_block" label="Reject" @click="openDialog('reject')" />
            <q-btn v-if="can('cancel')" color="negative" flat no-caps icon="o_close" label="Cancel return" @click="openDialog('cancel')" />
            <div v-if="actionCount === 0" class="text-grey-6">This return is {{ humanizeEnum(rma.status).toLowerCase() }}; no further actions.</div>
          </div>
        </AppSection>
      </div>
    </div>

    <!-- ============================== action dialog ============================== -->
    <q-dialog v-model="dialog.open" persistent>
      <q-card style="min-width: 420px; max-width: 900px; width: 100%">
        <q-card-section class="text-subtitle1 text-weight-medium">{{ dialogTitle }}</q-card-section>

        <q-card-section class="q-gutter-sm" style="max-height: 70vh; overflow-y: auto">
          <template v-if="dialog.type === 'approve'">
            <div class="text-body2">The customer is emailed the return address and instructions. The return expires if nothing ships in time.</div>
            <q-input v-model="dialog.instructions" type="textarea" autogrow dense outlined label="Instructions to the customer (optional)" />
            <q-input v-model="dialog.note" dense outlined label="Internal note (optional)" />
          </template>

          <template v-else-if="dialog.type === 'reject'">
            <q-input v-model="dialog.reason" type="textarea" autogrow dense outlined label="Reason (emailed to the customer) *" />
          </template>

          <template v-else-if="dialog.type === 'requestInfo'">
            <q-input v-model="dialog.message" type="textarea" autogrow dense outlined label="Question for the customer *" />
          </template>

          <template v-else-if="dialog.type === 'setFfl'">
            <q-input v-model="dialog.dealerName" dense outlined label="Dealer name *" />
            <q-input v-model="dialog.fflNumber" dense outlined label="FFL number *" />
            <q-input v-model="dialog.fflExpiry" type="date" dense outlined stack-label label="FFL expiry *" />
          </template>

          <template v-else-if="dialog.type === 'setLabel'">
            <div class="text-caption text-grey-7">Create the return label in the carrier's portal, then record it here; the customer is emailed the link.</div>
            <div class="row q-col-gutter-sm">
              <div class="col-6"><q-select v-model="dialog.carrier" :options="carrierOptions" emit-value map-options dense outlined label="Carrier" /></div>
              <div class="col-6"><q-input v-model="dialog.service" dense outlined label="Service" /></div>
              <div class="col-6"><q-input v-model="dialog.trackingNumber" dense outlined label="Tracking number" /></div>
              <div class="col-6"><q-input v-model.number="dialog.labelCost" type="number" step="0.01" min="0" dense outlined label="Label cost" /></div>
            </div>
            <q-input v-model="dialog.labelUrl" dense outlined label="Label link (https://…)" />
            <q-input v-model="dialog.instructions" type="textarea" autogrow dense outlined label="Instructions to the customer" />
            <q-toggle v-model="dialog.notify" label="Email the customer" />
          </template>

          <template v-else-if="dialog.type === 'markInTransit'">
            <q-select v-model="dialog.carrier" :options="carrierOptions" emit-value map-options dense outlined label="Carrier" />
            <q-input v-model="dialog.trackingNumber" dense outlined label="Tracking number" />
          </template>

          <template v-else-if="dialog.type === 'receive'">
            <div class="text-caption text-grey-7">Resellable / open-box units marked Restock go back into stock at the order's store. Saving again only adjusts the difference.</div>
            <q-markup-table flat dense>
              <thead><tr><th class="text-left">Item</th><th>Authorised</th><th style="width: 90px">Received</th><th style="width: 180px">Condition</th><th style="width: 180px">Disposition</th></tr></thead>
              <tbody>
                <tr v-for="l in dialog.lines" :key="l.rmaLineItemId">
                  <td>{{ l.productName }}</td>
                  <td class="text-center">{{ l.quantity }}</td>
                  <td><q-input v-model.number="l.quantityReceived" type="number" min="0" dense outlined /></td>
                  <td><q-select v-model="l.condition" :options="rmaConditionOptions" emit-value map-options dense outlined /></td>
                  <td><q-select v-model="l.disposition" :options="rmaDispositionOptions" emit-value map-options dense outlined /></td>
                </tr>
              </tbody>
            </q-markup-table>
            <q-input v-model="dialog.note" dense outlined label="Note (optional)" />
            <q-toggle v-model="dialog.notify" label="Email the customer that it arrived" />
          </template>

          <template v-else-if="dialog.type === 'inspect'">
            <q-markup-table flat dense>
              <thead><tr><th class="text-left">Item</th><th>Received</th><th style="width: 90px">Accept</th><th style="width: 110px">Deduction</th><th class="text-left">Deduction reason</th></tr></thead>
              <tbody>
                <tr v-for="l in dialog.lines" :key="l.rmaLineItemId">
                  <td>{{ l.productName }}<div class="text-caption text-grey-7">{{ humanizeEnum(l.conditionReceived) }}</div></td>
                  <td class="text-center">{{ l.quantityReceived }}</td>
                  <td><q-input v-model.number="l.quantityAccepted" type="number" min="0" :max="Math.min(l.quantityReceived, l.quantity)" dense outlined /></td>
                  <td><q-input v-model.number="l.deduction" type="number" min="0" step="0.01" dense outlined /></td>
                  <td><q-input v-model="l.deductionReason" dense outlined :placeholder="l.deduction > 0 ? 'Required' : ''" /></td>
                </tr>
              </tbody>
            </q-markup-table>
            <q-toggle v-model="dialog.refundShipping" label="Refund the original shipping" />
            <q-toggle v-model="dialog.waiveRestockFee" label="Waive the restocking fee" />
            <div class="row q-col-gutter-sm">
              <div class="col-4"><q-input v-model.number="dialog.adjustment" type="number" step="0.01" dense outlined label="Adjustment (±)" /></div>
              <div class="col-8"><q-input v-model="dialog.adjustmentReason" dense outlined label="Adjustment reason" :disable="!dialog.adjustment" /></div>
            </div>
            <q-input v-model="dialog.note" dense outlined label="Note (optional)" />
          </template>

          <template v-else-if="dialog.type === 'refund'">
            <div class="text-body1">Refund <strong>{{ rma.currencyCode }} {{ formatMoney(rma.refund.total - (rma.refundedAmount || 0)) }}</strong></div>
            <q-option-group
              v-model="dialog.method" type="radio"
              :options="[{ label: 'To the original payment (gateway, then gift certificate)', value: 'OriginalPayment' }, { label: 'Record a manual refund (made outside the system)', value: 'Manual' }]"
            />
            <q-input v-if="dialog.method === 'Manual'" v-model="dialog.manualReference" dense outlined label="How / reference (cheque #, portal ref …) *" />
            <q-toggle v-model="dialog.notify" label="Email the customer" />
          </template>

          <template v-else-if="dialog.type === 'storeCredit'">
            <div class="text-body2">Adds <strong>{{ rma.currencyCode }} {{ formatMoney(rma.refund.total) }}</strong> (plus any configured bonus) to the customer's store-credit balance. No card refund is made.</div>
            <q-toggle v-model="dialog.notify" label="Email the customer" />
          </template>

          <template v-else-if="dialog.type === 'exchange'">
            <div class="text-body2">Creates a no-charge replacement order for the accepted items and takes them out of stock. Charge or refund any price difference separately.</div>
            <q-input v-model="dialog.note" dense outlined label="Note (optional)" />
            <q-toggle v-model="dialog.notify" label="Email the customer" />
          </template>

          <template v-else-if="dialog.type === 'editLines'">
            <q-markup-table flat dense>
              <thead><tr><th class="text-left">Item</th><th style="width: 110px">Quantity</th></tr></thead>
              <tbody>
                <tr v-for="l in dialog.lines" :key="l.rmaLineItemId">
                  <td>{{ l.productName }}</td>
                  <td><q-input v-model.number="l.quantity" type="number" min="0" dense outlined /></td>
                </tr>
              </tbody>
            </q-markup-table>
            <div class="text-caption text-grey-7">0 removes the item from the return.</div>
            <q-input v-model="dialog.note" dense outlined label="Reason for the change" />
          </template>

          <template v-else-if="['close', 'cancel'].includes(dialog.type)">
            <q-input v-model="dialog.note" type="textarea" autogrow dense outlined label="Note (optional)" />
          </template>

          <template v-else-if="dialog.type === 'reopen'">
            <q-input v-model="dialog.reason" type="textarea" autogrow dense outlined label="Why are you reopening it? *" />
          </template>
        </q-card-section>

        <q-card-actions align="right">
          <q-btn flat no-caps label="Cancel" :disable="busy" @click="dialog.open = false" />
          <q-btn unelevated color="primary" no-caps :label="dialogOk" :loading="busy" @click="submitDialog" />
        </q-card-actions>
      </q-card>
    </q-dialog>

    <AppRecordMeta entity-type="rma" :record-id="rma?.id" />
  </q-page>
</template>

<script setup>
/* Admin RMA detail and workflow (docs/order-return-requirements.bak.md, section 7). Every action posts to
 * AdminRmaController and replaces the page state with the refreshed return. */
import { ref, reactive, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { getApiErrorMessage } from 'services/api'
import { useNotify } from 'composables/useNotify'
import {
  rmaApi, rmaStatusColor, rmaResolutionOptions, rmaReturnMethodOptions, rmaConditionOptions,
  rmaDispositionOptions, shipmentCarrierOptions, humanizeEnum, formatMoney, formatDate
} from 'modules/orders/api'

const route = useRoute()
const router = useRouter()
const notify = useNotify()

const rma = ref(null)
const loading = ref(false)
const busy = ref(false)
const upload = ref(null)
const message = reactive({ body: '', isInternal: true, notify: true })
const dialog = reactive({ open: false, type: '' })

const carrierOptions = shipmentCarrierOptions.filter((c) => c.value !== 'Freightview')

const isOpen = computed(() => rma.value && !['Rejected', 'Cancelled', 'Expired', 'Closed'].includes(rma.value.status))
const can = (action) => !!rma.value?.availableActions?.includes(action)
const actionCount = computed(() => (rma.value?.availableActions || []).filter((a) => a !== 'message').length)

const resolutionLabel = (v) => rmaResolutionOptions.find((o) => o.value === v)?.label || humanizeEnum(v)
const methodLabel = (v) => rmaReturnMethodOptions.find((o) => o.value === v)?.label || humanizeEnum(v)
const formatDay = (v) => (v ? new Date(v).toLocaleDateString() : '')

const TITLES = {
  approve: ['Approve return', 'Approve'],
  reject: ['Reject return', 'Reject'],
  requestInfo: ['Ask the customer for more information', 'Send'],
  setFfl: ['Returning dealer (FFL)', 'Save'],
  setLabel: ['Return label / shipping details', 'Save'],
  markInTransit: ['Mark in transit', 'Save'],
  receive: ['Receive package', 'Save receipt'],
  inspect: ['Inspect items', 'Save inspection'],
  refund: ['Send refund', 'Refund now'],
  storeCredit: ['Issue store credit', 'Issue credit'],
  exchange: ['Exchange', 'Create replacement order'],
  editLines: ['Edit requested quantities', 'Save'],
  close: ['Close return', 'Close'],
  reopen: ['Reopen return', 'Reopen'],
  cancel: ['Cancel return', 'Cancel return']
}
const dialogTitle = computed(() => TITLES[dialog.type]?.[0] || '')
const dialogOk = computed(() => TITLES[dialog.type]?.[1] || 'Save')

async function load () {
  loading.value = true
  try {
    rma.value = await rmaApi.get(route.params.id)
  } catch (err) {
    notify.error(getApiErrorMessage(err))
    rma.value = null
  } finally {
    loading.value = false
  }
}

function openOrder (id) {
  if (id) router.push({ name: 'admin-order-detail', params: { id } })
}

function openDialog (type) {
  const r = rma.value
  // Reset to a clean slate, then prefill what the action needs.
  Object.keys(dialog).forEach((k) => { if (!['open', 'type'].includes(k)) delete dialog[k] })
  dialog.type = type
  dialog.note = ''
  dialog.reason = ''
  dialog.notify = true
  switch (type) {
    case 'approve':
      dialog.instructions = r.returnInstructions || ''
      break
    case 'setFfl':
      dialog.dealerName = r.fflDealerName || ''
      dialog.fflNumber = r.fflNumber || ''
      dialog.fflExpiry = r.fflExpiry ? r.fflExpiry.substring(0, 10) : ''
      break
    case 'setLabel':
      Object.assign(dialog, {
        carrier: r.labelCarrier || 'USPS', service: r.labelService || '', trackingNumber: r.trackingNumber || '',
        labelUrl: r.labelUrl || '', labelCost: r.labelCost, instructions: r.returnInstructions || ''
      })
      break
    case 'markInTransit':
      dialog.carrier = r.labelCarrier || ''
      dialog.trackingNumber = r.trackingNumber || ''
      break
    case 'receive':
      dialog.lines = r.lines.map((l) => ({
        rmaLineItemId: l.id,
        productName: l.productName,
        quantity: l.quantity,
        quantityReceived: r.status === 'Received' ? l.quantityReceived : l.quantity,
        condition: l.conditionReceived || 'Resellable',
        disposition: l.disposition || 'Restock'
      }))
      break
    case 'inspect':
      dialog.lines = r.lines.map((l) => ({
        rmaLineItemId: l.id,
        productName: l.productName,
        quantity: l.quantity,
        quantityReceived: l.quantityReceived,
        conditionReceived: l.conditionReceived,
        quantityAccepted: l.quantityAccepted,
        deduction: l.deduction || 0,
        deductionReason: l.deductionReason || ''
      }))
      Object.assign(dialog, {
        refundShipping: r.refundShipping, waiveRestockFee: r.waiveRestockFee,
        adjustment: r.refund.adjustment || 0, adjustmentReason: r.adjustmentReason || ''
      })
      break
    case 'refund':
      dialog.method = 'OriginalPayment'
      dialog.manualReference = ''
      break
    case 'editLines':
      dialog.lines = r.lines.map((l) => ({ rmaLineItemId: l.id, productName: l.productName, quantity: l.quantity }))
      break
  }
  dialog.open = true
}

const blank = (s) => (s && String(s).trim() ? String(s).trim() : null)

async function submitDialog () {
  const id = rma.value.id
  const d = dialog
  const calls = {
    approve: () => rmaApi.approve(id, { rmaId: id, instructions: blank(d.instructions), note: blank(d.note) }),
    reject: () => rmaApi.reject(id, { rmaId: id, reason: d.reason }),
    requestInfo: () => rmaApi.requestInfo(id, { rmaId: id, message: d.message }),
    setFfl: () => rmaApi.setFfl(id, { rmaId: id, dealerName: d.dealerName, fflNumber: d.fflNumber, fflExpiry: d.fflExpiry || null }),
    setLabel: () => rmaApi.setLabel(id, {
      rmaId: id, carrier: blank(d.carrier), service: blank(d.service), trackingNumber: blank(d.trackingNumber),
      labelUrl: blank(d.labelUrl), labelCost: d.labelCost === '' || d.labelCost == null ? null : Number(d.labelCost),
      instructions: d.instructions ?? null, notifyCustomer: d.notify
    }),
    markInTransit: () => rmaApi.markInTransit(id, { rmaId: id, carrier: blank(d.carrier), trackingNumber: blank(d.trackingNumber) }),
    receive: () => rmaApi.receive(id, {
      rmaId: id,
      lines: d.lines.map((l) => ({ rmaLineItemId: l.rmaLineItemId, quantityReceived: Number(l.quantityReceived) || 0, condition: l.condition, disposition: l.disposition })),
      note: blank(d.note),
      notifyCustomer: d.notify
    }),
    inspect: () => rmaApi.inspect(id, {
      rmaId: id,
      lines: d.lines.map((l) => ({ rmaLineItemId: l.rmaLineItemId, quantityAccepted: Number(l.quantityAccepted) || 0, deduction: Number(l.deduction) || 0, deductionReason: blank(l.deductionReason) })),
      refundShipping: !!d.refundShipping,
      waiveRestockFee: !!d.waiveRestockFee,
      adjustment: Number(d.adjustment) || 0,
      adjustmentReason: blank(d.adjustmentReason),
      note: blank(d.note)
    }),
    refund: () => rmaApi.refund(id, { rmaId: id, method: d.method, manualReference: blank(d.manualReference), notifyCustomer: d.notify }),
    storeCredit: () => rmaApi.refund(id, { rmaId: id, method: 'StoreCredit', notifyCustomer: d.notify }),
    exchange: () => rmaApi.exchange(id, { rmaId: id, note: blank(d.note), notifyCustomer: d.notify }),
    editLines: () => rmaApi.updateLines(id, { rmaId: id, lines: d.lines.map((l) => ({ rmaLineItemId: l.rmaLineItemId, quantity: Number(l.quantity) || 0 })), note: blank(d.note) }),
    close: () => rmaApi.close(id, { rmaId: id, note: blank(d.note) }),
    reopen: () => rmaApi.reopen(id, { rmaId: id, reason: d.reason }),
    cancel: () => rmaApi.cancel(id, { rmaId: id, note: blank(d.note) })
  }
  busy.value = true
  try {
    rma.value = await calls[d.type]()
    notify.success(`${dialogTitle.value}: done`)
    dialog.open = false
  } catch (err) {
    notify.error(getApiErrorMessage(err))
    // A failed refund is recorded on the return; show it.
    if (d.type === 'refund' || d.type === 'storeCredit') await load()
  } finally {
    busy.value = false
  }
}

async function sendMessage () {
  busy.value = true
  try {
    rma.value = await rmaApi.addMessage(rma.value.id, {
      rmaId: rma.value.id, body: message.body, isInternal: message.isInternal, notifyCustomer: !message.isInternal && message.notify
    })
    message.body = ''
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    busy.value = false
  }
}

async function uploadFile (file) {
  if (!file) return
  busy.value = true
  try {
    await rmaApi.uploadAttachment(rma.value.id, file)
    await load()
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    upload.value = null
    busy.value = false
  }
}

async function openAttachment (a) {
  try {
    await rmaApi.openAttachment(rma.value.id, a.id)
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  }
}

onMounted(load)
</script>
