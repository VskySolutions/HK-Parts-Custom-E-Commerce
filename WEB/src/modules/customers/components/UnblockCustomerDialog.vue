<template>
  <q-dialog :model-value="modelValue" persistent @update:model-value="emit('update:modelValue', $event)">
    <q-card class="unblock-card">
      <div class="unblock-header row items-center no-wrap">
        <div class="text-h6 text-weight-bold">Unblock Customer</div>
        <q-space />
        <q-btn flat round dense icon="o_close" color="white" :disable="saving" @click="close" />
      </div>

      <q-card-section class="q-pt-lg">
        <div class="text-body1 text-grey-8">
          This customer will regain access to place orders on the website. Are you sure you want to continue?
        </div>

        <div class="unblock-details q-mt-lg">
          <div class="row no-wrap q-mb-sm">
            <q-icon name="o_person" class="detail-icon" />
            <div>
              <div class="text-caption text-grey-7">Customer</div>
              <div class="text-subtitle1 text-weight-bold">{{ name || '—' }}</div>
            </div>
          </div>
          <div class="row no-wrap q-mb-sm">
            <q-icon name="o_mail" class="detail-icon" />
            <div>
              <div class="text-caption text-grey-7">Email</div>
              <div class="text-body2">{{ entry?.email || '—' }}</div>
            </div>
          </div>
          <div class="row no-wrap">
            <span class="detail-icon" />
            <div>
              <div class="text-caption text-grey-7">Blacklist Reason</div>
              <div class="text-body2">{{ entry?.reason || '—' }}</div>
            </div>
          </div>
        </div>

        <div class="unblock-note q-mt-md">
          <strong>Note:</strong> After unblocking, the customer can immediately place new orders.
        </div>
      </q-card-section>

      <q-separator />
      <q-card-actions align="right" class="q-pa-md">
        <q-btn outline no-caps color="grey-8" label="Cancel" :disable="saving" @click="close" />
        <q-btn unelevated no-caps class="unblock-btn" label="Unblock Customer" :loading="saving" @click="confirm" />
      </q-card-actions>
    </q-card>
  </q-dialog>
</template>

<script setup>
/*
 * Confirms unblocking a Blocked Customers entry. The parent does the API call in its `confirm` handler
 * (receives the entry) and closes the dialog; `saving` shows the button spinner meanwhile.
 */
import { computed } from 'vue'

const props = defineProps({
  modelValue: { type: Boolean, default: false },
  entry: { type: Object, default: null },
  saving: { type: Boolean, default: false }
})
const emit = defineEmits(['update:modelValue', 'confirm'])

const name = computed(() => [props.entry?.firstName, props.entry?.lastName].filter(Boolean).join(' '))

function close () { emit('update:modelValue', false) }
function confirm () { emit('confirm', props.entry) }
</script>

<style scoped>
.unblock-card {
  width: 500px;
  max-width: 95vw;
  border-radius: 10px;
  overflow: hidden;
}
.unblock-header {
  background: #7cc142;
  color: #fff;
  padding: 14px 20px 14px 32px;
}
.unblock-details {
  background: #f7f8fa;
  border: 1px solid #e3e6ea;
  border-radius: 8px;
  padding: 16px 18px;
}
.detail-icon {
  width: 30px;
  min-width: 30px;
  font-size: 16px;
  color: #7cc142;
  padding-top: 2px;
}
.unblock-note {
  background: #f2f9ec;
  border-left: 4px solid #7cc142;
  padding: 10px 16px;
  font-size: 13px;
  color: #555;
}
.unblock-btn {
  background: #7cc142;
  color: #fff;
  min-width: 158px;
}
</style>
