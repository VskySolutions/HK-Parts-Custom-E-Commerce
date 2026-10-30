<template>
  <q-page class="q-pa-md storefront-container">
    <div class="text-h5 text-weight-bold q-mb-md">Checkout</div>

    <q-inner-loading :showing="loading && !cart" color="primary" />

    <!-- Inquiry submitted (REQ-INQ-001) — no payment was taken, so no totals/transaction block -->
    <q-card v-if="inquiryResult" flat bordered class="q-mx-auto confirm-card">
      <q-card-section class="text-center q-pa-xl">
        <q-icon name="o_mark_email_read" color="positive" size="72px" />
        <div class="text-h5 text-weight-bold q-mt-md">Thanks — we've got your request!</div>
        <div class="text-body1 text-grey-8 q-mt-sm">
          Your reference is
          <span class="text-weight-bold text-dark">{{ inquiryResult.referenceNumber }}</span>.
        </div>
        <div class="text-body2 text-grey-8 q-mt-md">
          No payment has been taken. Our team will review your request and get back to you
          <template v-if="preferredContact === 'Phone'">by phone</template>
          <template v-else-if="preferredContact === 'WhatsApp'">on WhatsApp</template>
          <template v-else>by email</template>.
        </div>
        <div class="text-caption text-grey-6 q-mt-lg">
          A confirmation of your request is on its way to {{ email }}.
        </div>
        <div class="row justify-center q-gutter-sm q-mt-lg">
          <q-btn unelevated color="primary" no-caps label="Continue browsing" :to="{ name: 'shop-home' }" />
        </div>
      </q-card-section>
    </q-card>

    <!-- Order confirmation (AC-CHK-003.8) -->
    <q-card v-else-if="orderResult && orderResult.success" flat bordered class="q-mx-auto confirm-card">
      <q-card-section class="text-center q-pa-xl">
        <q-icon name="o_check_circle" color="positive" size="72px" />
        <div class="text-h5 text-weight-bold q-mt-md">Thank you for your order!</div>
        <div class="text-body1 text-grey-8 q-mt-sm">
          Your order <span class="text-weight-bold text-dark">{{ orderResult.orderNumber }}</span> has been placed.
        </div>
        <div class="row justify-center q-gutter-xl q-mt-lg">
          <div>
            <div class="text-caption text-grey-6">Order total</div>
            <div class="text-subtitle1 text-weight-bold">{{ format(orderResult.total) }}</div>
          </div>
          <div>
            <div class="text-caption text-grey-6">Payment</div>
            <div class="text-subtitle1 text-weight-bold">{{ orderResult.paymentStatus }}</div>
          </div>
        </div>
        <div v-if="orderResult.transactionId" class="q-mt-lg">
          <div class="text-caption text-grey-6">Transaction ID</div>
          <div class="text-body2 text-weight-medium sf-txn-id">{{ orderResult.transactionId }}</div>
        </div>
        <div class="text-caption text-grey-6 q-mt-lg">
          A confirmation email with your order details is on its way.
        </div>

        <!-- Backordered items ship separately (WO-86 confirmation notice) -->
        <q-banner
          v-if="orderHadBackorder"
          dense
          class="sf-backorder-banner rounded-borders q-mt-lg text-left"
        >
          <template #avatar><q-icon name="o_schedule" color="orange-9" /></template>
          Some items in your order are backordered and will ship separately as soon as they're back in stock.
        </q-banner>

        <div class="row justify-center q-gutter-sm q-mt-lg">
          <q-btn
            unelevated
            color="primary"
            no-caps
            label="Continue shopping"
            :to="{ name: 'shop-home' }"
          />
          <q-btn
            v-if="isAuthed"
            outline
            color="primary"
            no-caps
            icon="o_receipt_long"
            label="My orders"
            :to="{ name: 'shop-account-orders' }"
          />
        </div>
      </q-card-section>
    </q-card>

    <!-- Verifying a redirect payment on return from the gateway -->
    <q-card v-else-if="confirming" flat bordered class="q-mx-auto confirm-card">
      <q-card-section class="text-center q-pa-xl">
        <q-spinner color="primary" size="48px" />
        <div class="text-subtitle1 text-weight-medium q-mt-md">Confirming your payment…</div>
        <div class="text-caption text-grey-6 q-mt-sm">Please wait, this only takes a moment.</div>
      </q-card-section>
    </q-card>

    <!-- Payment cancelled / not completed — offer a retry (AC-PAY-001.3) -->
    <q-card v-else-if="retryState" flat bordered class="q-mx-auto confirm-card">
      <q-card-section class="text-center q-pa-xl">
        <q-icon name="o_error_outline" color="warning" size="64px" />
        <div class="text-h6 text-weight-bold q-mt-md">Payment not completed</div>
        <div class="text-body2 text-grey-8 q-mt-sm">{{ retryState.message }}</div>
        <div class="text-caption text-grey-6 q-mt-sm">Your cart is saved — you can try again.</div>
        <div class="row justify-center q-gutter-sm q-mt-lg">
          <q-btn
            unelevated
            color="primary"
            no-caps
            icon="o_refresh"
            label="Retry payment"
            :loading="retrying"
            @click="retryPayment"
          />
          <q-btn flat color="primary" no-caps label="Change payment method" @click="retryState = null" />
        </div>
      </q-card-section>
    </q-card>

    <!-- Empty cart -->
    <q-banner
      v-else-if="cart && !items.length"
      class="bg-grey-2 rounded-borders q-my-md"
    >
      Your cart is empty — there is nothing to check out.
      <template #action>
        <q-btn flat no-caps color="primary" label="Continue shopping" :to="{ name: 'shop-home' }" />
      </template>
    </q-banner>

    <!-- Checkout form -->
    <div v-else-if="items.length" class="row q-col-gutter-lg">
      <!-- Left: address, shipping, payment -->
      <div class="col-12 col-md-7">
        <q-form ref="formRef" greedy>
          <!-- Contact + delivery address -->
          <q-card flat bordered class="q-mb-lg">
            <q-card-section>
              <div class="text-subtitle1 text-weight-bold q-mb-md">
                {{ contactSectionTitle }}
              </div>

              <!-- Deliver vs collect — only offered when a store actually opts in to pickup -->
              <q-btn-toggle
                v-if="pickupOffered"
                v-model="fulfilment"
                class="q-mb-md sf-fulfilment-toggle"
                no-caps
                unelevated
                spread
                toggle-color="primary"
                color="grey-2"
                text-color="grey-8"
                :options="fulfilmentOptions"
              />

              <!-- Pickup: choose the store to collect from; the info icon carries its address -->
              <div v-if="isPickup" class="column q-gutter-sm q-mb-md">
                <q-item
                  v-for="s in pickupStores"
                  :key="s.id"
                  tag="label"
                  clickable
                  class="sf-address-option rounded-borders"
                  :class="{ 'sf-address-option--active': pickupStoreId === s.id }"
                >
                  <q-item-section avatar>
                    <q-radio v-model="pickupStoreId" :val="s.id" color="primary" />
                  </q-item-section>
                  <q-item-section>
                    <q-item-label class="text-weight-medium row items-center no-wrap">
                      {{ s.name }}
                      <q-icon name="o_info" size="18px" color="primary" class="q-ml-xs cursor-pointer">
                        <q-tooltip anchor="top middle" self="bottom middle" class="sf-store-tooltip text-body2">
                          <div class="text-weight-medium">{{ s.name }}</div>
                          <div>{{ formatAddr(s) || 'Address not available' }}</div>
                        </q-tooltip>
                      </q-icon>
                    </q-item-label>
                    <q-item-label caption>{{ formatAddr(s) }}</q-item-label>
                  </q-item-section>
                </q-item>
              </div>

              <!-- Signed-in: pick a saved address or add a new one (delivery only) -->
              <div v-if="!isPickup && isAuthed && savedAddresses.length" class="column q-gutter-sm q-mb-md">
                <q-item
                  v-for="a in savedAddresses"
                  :key="a.id"
                  tag="label"
                  clickable
                  class="sf-address-option rounded-borders"
                  :class="{ 'sf-address-option--active': selectedAddressId === a.id }"
                >
                  <q-item-section avatar top>
                    <q-radio :model-value="selectedAddressId" :val="a.id" @update:model-value="selectSaved(a)" />
                  </q-item-section>
                  <q-item-section>
                    <q-item-label class="text-weight-medium">
                      {{ a.firstName }} {{ a.lastName }}
                      <q-badge v-if="a.isDefault" color="primary" label="Default" class="q-ml-xs" />
                    </q-item-label>
                    <q-item-label caption>{{ formatAddr(a) }}</q-item-label>
                    <q-item-label v-if="a.phoneNumber" caption>{{ a.phoneNumber }}</q-item-label>
                  </q-item-section>
                  <q-item-section side top>
                    <q-btn
                      v-if="!a.isDefault"
                      flat
                      dense
                      no-caps
                      size="sm"
                      color="primary"
                      icon="o_push_pin"
                      label="Set as default"
                      :loading="settingDefaultId === a.id"
                      @click.stop.prevent="setSavedAddressDefault(a)"
                    />
                  </q-item-section>
                </q-item>

                <q-item
                  tag="label"
                  clickable
                  class="sf-address-option rounded-borders"
                  :class="{ 'sf-address-option--active': selectedAddressId === 'new' }"
                >
                  <q-item-section avatar>
                    <q-radio :model-value="selectedAddressId" val="new" @update:model-value="selectNew()" />
                  </q-item-section>
                  <q-item-section>
                    <q-item-label class="text-primary text-weight-medium">
                      <q-icon name="o_add" size="18px" class="q-mr-xs" />Use a new address
                    </q-item-label>
                  </q-item-section>
                </q-item>
              </div>

              <!-- Contact recap when a saved address is in use -->
              <div v-if="!isPickup && isAuthed && !showAddressForm" class="text-caption text-grey-7 q-mb-sm">
                Order updates go to <strong>{{ profileEmail }}</strong>
              </div>

              <!-- Manual entry: guest, no saved addresses, "new address" chosen — or any pickup order -->
              <template v-if="showContactForm">
                <div class="row q-col-gutter-x-md q-mb-sm">
                  <div class="col-12 col-sm-6">
                    <AppTextField
                      label="Email"
                      type="email"
                      required
                      :model-value="email"
                      :v="emailV"
                      placeholder="you@example.com"
                      @update:model-value="email = $event"
                    />
                  </div>
                  <div class="col-12 col-sm-6">
                    <AppPhoneInput
                      label="Phone"
                      required
                      :model-value="addr.phoneNumber"
                      :default-country="addr.countryCode || 'US'"
                      @update:model-value="addr.phoneNumber = $event"
                    />
                  </div>
                </div>
                <!-- Pickup and contact-only inquiries need just who is asking; delivery takes the names
                     from the address form. -->
                <div v-if="isPickup || contactOnly" class="row q-col-gutter-x-md">
                  <div class="col-12 col-sm-6">
                    <AppTextField
                      label="First name"
                      required
                      :model-value="addr.firstName"
                      @update:model-value="addr.firstName = $event"
                    />
                  </div>
                  <div class="col-12 col-sm-6">
                    <AppTextField
                      label="Last name"
                      required
                      :model-value="addr.lastName"
                      @update:model-value="addr.lastName = $event"
                    />
                  </div>
                </div>
                <AppAddressForm
                  v-else
                  v-model="addr"
                  :required="!isInquiry"
                  :show-company="false"
                  :show-phone="false"
                />
                <q-toggle
                  v-if="!isPickup && !contactOnly && isAuthed"
                  v-model="saveNewAddress"
                  label="Save this address to my account"
                  dense
                  class="q-mt-sm"
                />
              </template>

              <div v-if="!isInquiry" class="q-mt-md">
                <q-btn
                  unelevated
                  color="primary"
                  no-caps
                  :label="quoteLabel"
                  :icon="isPickup ? 'o_storefront' : 'o_local_shipping'"
                  :loading="quoting"
                  :disable="!canQuote || quoting"
                  @click="runQuote"
                />
              </div>
            </q-card-section>
          </q-card>
        </q-form>

        <!-- What the buyer actually wants (REQ-INQ-001). This card is the inquiry: it replaces the
             shipping + payment steps, and it is what the sales team reads first. -->
        <q-card v-if="isInquiry" flat bordered class="q-mb-lg">
          <q-card-section>
            <div class="text-subtitle1 text-weight-bold q-mb-md">Your requirement</div>

            <div class="row q-col-gutter-x-md q-mb-sm">
              <div class="col-12 col-sm-6">
                <AppTextField
                  :model-value="companyName"
                  placeholder="Company or organisation (optional)"
                  @update:model-value="companyName = $event"
                />
              </div>
              <div class="col-12 col-sm-6">
                <q-select
                  v-model="preferredContact"
                  outlined
                  dense
                  emit-value
                  map-options
                  label="Preferred contact"
                  :options="contactPreferenceOptions"
                  class="q-mb-md"
                />
              </div>
            </div>

            <q-input
              v-model="requiredBy"
              outlined
              dense
              type="date"
              label="Needed by (optional)"
              class="q-mb-md"
            />

            <q-input
              v-model="inquiryMessage"
              outlined
              type="textarea"
              autogrow
              counter
              maxlength="4000"
              label="Tell us what you need"
              placeholder="Quantities, specifications, delivery timelines, or anything else we should know."
              :input-style="{ minHeight: '110px' }"
            />
          </q-card-section>
        </q-card>

        <!-- Address hint before a quote is available -->
        <q-banner
          v-if="!isInquiry && !quote && !quoting && !quoteError"
          dense
          class="bg-blue-1 text-blue-9 rounded-borders q-mb-lg"
        >
          <template #avatar><q-icon name="o_info" color="blue-9" /></template>
          <template v-if="isPickup">
            Choose a store and confirm your contact details, then <strong>Calculate total</strong> to see live totals.
          </template>
          <template v-else>
            Complete your delivery address, then <strong>Save &amp; calculate shipping</strong> to see options and live totals.
          </template>
        </q-banner>

        <q-banner v-if="quoteError" dense class="bg-red-1 text-red-9 rounded-borders q-mb-lg">
          <template #avatar><q-icon name="o_error" color="red-9" /></template>
          {{ quoteError }}
        </q-banner>

        <!-- Not routable to any store -->
        <q-banner
          v-if="quote && !isRoutable"
          dense
          class="bg-orange-1 text-orange-9 rounded-borders q-mb-lg"
        >
          <template #avatar><q-icon name="o_local_shipping" color="orange-9" /></template>
          We're unable to deliver to this address right now. Please try a different address.
        </q-banner>

        <!-- Guest ordering not permitted — offer express account creation (AC-CHK-003.2 / AC-STR-001.5) -->
        <q-card
          v-if="quote && isRoutable && !guestOrderingAllowed"
          flat
          bordered
          class="q-mb-lg bg-amber-1"
        >
          <q-card-section>
            <div class="row items-center no-wrap q-mb-sm">
              <q-icon name="o_lock" color="amber-9" size="28px" class="q-mr-md" />
              <div class="col">
                <div class="text-subtitle2 text-weight-bold">One quick step to place your order</div>
                <div class="text-body2 text-grey-8">
                  This store requires an account. We'll create one from the details above and email you a password —
                  sign in and your cart &amp; details will be right here to finish, no re-typing.
                </div>
              </div>
            </div>
            <div class="row items-center justify-end q-gutter-sm">
              <q-btn flat no-caps color="primary" label="I already have an account" :to="loginTo" />
              <q-btn
                unelevated
                color="primary"
                no-caps
                icon="o_person_add"
                label="Create account &amp; finish"
                :loading="registering"
                :disable="!canQuote || registering"
                @click="registerAndContinue"
              />
            </div>
          </q-card-section>
        </q-card>

        <!-- Shipping method — never shown for an inquiry: nothing is being shipped or rated yet. -->
        <q-card v-if="canCollectDetails && !isInquiry" flat bordered class="q-mb-lg">
          <q-card-section>
            <div class="text-subtitle1 text-weight-bold q-mb-sm">{{ isPickup ? 'Collection' : 'Shipping method' }}</div>
            <div v-if="shippingCarriers.length" class="row items-center q-gutter-xs q-mb-sm">
              <span class="text-caption text-grey-6">Rates from</span>
              <q-chip
                v-for="c in shippingCarriers"
                :key="c.carrier"
                dense
                square
                :icon="c.icon"
                :label="c.label"
                class="sf-partner-chip q-my-none"
              />
            </div>
            <q-inner-loading :showing="quoting" color="primary" />

            <q-list v-if="shippingOptions.length" separator>
              <q-item
                v-for="opt in primaryShippingOptions"
                :key="opt.methodId"
                tag="label"
                clickable
              >
                <q-item-section avatar>
                  <q-radio
                    :model-value="selectedShippingMethodId"
                    :val="opt.methodId"
                    color="primary"
                    @update:model-value="onShippingChange"
                  />
                </q-item-section>
                <q-item-section>
                  <q-item-label>
                    {{ opt.name }}
                    <q-badge v-if="opt.isRecommended" color="primary" class="q-ml-sm" label="Recommended" />
                  </q-item-label>
                  <q-item-label caption>
                    {{ opt.carrier }}
                    <span v-if="opt.estimatedDeliveryDays != null"> · {{ deliveryText(opt) }}</span>
                  </q-item-label>
                </q-item-section>
                <q-item-section side>
                  <span class="text-weight-medium text-dark">{{ format(opt.rate) }}</span>
                </q-item-section>
              </q-item>
            </q-list>

            <!-- Under automatic selection the recommendation is shown alone; the rest stay one click away
                 so the customer keeps the choice rather than being locked into the scored pick. -->
            <q-expansion-item
              v-if="otherShippingOptions.length"
              v-model="showOtherShipping"
              dense
              class="q-mt-sm"
              :label="`Other delivery options (${otherShippingOptions.length})`"
              header-class="text-primary text-body2"
            >
              <q-list separator>
                <q-item
                  v-for="opt in otherShippingOptions"
                  :key="opt.methodId"
                  tag="label"
                  clickable
                >
                  <q-item-section avatar>
                    <q-radio
                      :model-value="selectedShippingMethodId"
                      :val="opt.methodId"
                      color="primary"
                      @update:model-value="onShippingChange"
                    />
                  </q-item-section>
                  <q-item-section>
                    <q-item-label>{{ opt.name }}</q-item-label>
                    <q-item-label caption>
                      {{ opt.carrier }}
                      <span v-if="opt.estimatedDeliveryDays != null"> · {{ deliveryText(opt) }}</span>
                    </q-item-label>
                  </q-item-section>
                  <q-item-section side>
                    <span class="text-weight-medium text-dark">{{ format(opt.rate) }}</span>
                  </q-item-section>
                </q-item>
              </q-list>
            </q-expansion-item>

            <div v-else-if="shippingUnavailable" class="text-body2 text-negative row items-start no-wrap">
              <q-icon name="o_error_outline" size="20px" class="q-mr-sm q-mt-xs" />
              <span>
                No delivery options are available for this address. Try a different address<span v-if="pickupOffered">, pick up in store</span>,
                or contact us and we'll help.
              </span>
            </div>
          </q-card-section>
        </q-card>

        <!-- Payment method (AC-PAY-001.4/5) — absent for an inquiry; no money changes hands. -->
        <q-card v-if="canCollectDetails && !isInquiry" flat bordered class="q-mb-lg">
          <q-card-section>
            <div class="text-subtitle1 text-weight-bold q-mb-sm">Payment method</div>

            <div v-if="paymentMethods.length" class="column q-gutter-sm">
              <q-item
                v-for="m in paymentMethods"
                :key="m.value"
                tag="label"
                clickable
                class="sf-payment-option rounded-borders"
                :class="{ 'sf-payment-option--active': paymentMethod === m.value }"
              >
                <q-item-section avatar>
                  <q-avatar square rounded size="42px" class="sf-payment-icon">
                    <q-icon :name="m.icon" size="24px" />
                  </q-avatar>
                </q-item-section>
                <q-item-section>
                  <q-item-label class="text-weight-medium">
                    {{ m.label }}
                    <q-badge
                      v-if="m.isProduction === false"
                      color="warning"
                      text-color="dark"
                      label="Sandbox"
                      class="q-ml-xs"
                    />
                    <q-badge
                      v-else-if="m.isProduction === true"
                      color="positive"
                      label="Live"
                      class="q-ml-xs"
                    />
                    <q-badge
                      v-else
                      color="grey-5"
                      text-color="dark"
                      label="Manual"
                      class="q-ml-xs"
                    />
                  </q-item-label>
                  <q-item-label caption>{{ m.hint }}</q-item-label>
                  <q-item-label v-if="m.feePercent > 0" caption class="text-orange-8">
                    +{{ m.feePercent }}% transaction fee
                  </q-item-label>
                </q-item-section>
                <q-item-section side>
                  <q-radio :model-value="paymentMethod" :val="m.value" color="primary" @update:model-value="paymentMethod = m.value" />
                </q-item-section>
              </q-item>
            </div>

            <q-banner v-else dense class="bg-red-1 text-negative rounded-borders">
              <template #avatar><q-icon name="o_error_outline" color="negative" /></template>
              No payment methods are available for this order.
            </q-banner>

            <!-- Square on-site card entry: the Web Payments SDK renders a hosted field here and tokenizes the
                 card into a nonce on Place order (the store never sees the card number). Kept in the DOM via
                 v-show so switching payment methods doesn't tear the mounted field down. -->
            <div v-show="paymentMethod === 'Square'" class="q-mt-md">
              <q-banner v-if="squareNotConfigured" dense class="bg-red-1 text-negative rounded-borders">
                <template #avatar><q-icon name="o_error_outline" color="negative" /></template>
                Square card payment isn't fully set up. Please choose another payment method.
              </q-banner>
              <template v-else>
                <div class="text-caption text-grey-7 q-mb-xs">Card details</div>
                <div id="sq-card-container" class="sf-square-card"></div>
                <div class="row items-center text-caption text-grey-6 q-mt-xs no-wrap">
                  <q-icon name="o_lock" size="14px" class="q-mr-xs" />
                  Entered securely in a Square field — the store never sees your card number.
                </div>
                <q-banner v-if="squareCardError" dense class="bg-red-1 text-negative rounded-borders q-mt-sm">
                  {{ squareCardError }}
                </q-banner>
              </template>
            </div>

            <!-- Authorize.Net on-site entry: plain fields tokenized by Accept.js into a single-use nonce on
                 Place order (the store never sees the card or bank account number). Buyers choose the
                 instrument — credit/debit card or bank account (ACH/eCheck) — both charged via the same nonce. -->
            <div v-if="paymentMethod === 'AuthorizeNet'" class="q-mt-md">
              <q-banner v-if="authNetNotConfigured" dense class="bg-red-1 text-negative rounded-borders">
                <template #avatar><q-icon name="o_error_outline" color="negative" /></template>
                Payment isn't fully set up. Please choose another payment method.
              </q-banner>
              <template v-else>
                <q-btn-toggle
                  v-model="authNetInstrument"
                  class="q-mb-md sf-instrument-toggle"
                  no-caps
                  unelevated
                  spread
                  toggle-color="primary"
                  color="grey-2"
                  text-color="grey-8"
                  :options="authNetInstrumentOptions"
                />

                <!-- Card -->
                <template v-if="authNetInstrument === 'card'">
                  <div class="text-caption text-grey-7 q-mb-xs">Card details</div>
                  <q-input
                    v-model="authNetCard.number"
                    outlined dense
                    inputmode="numeric"
                    maxlength="23"
                    label="Card number"
                    placeholder="1234 5678 9012 3456"
                    class="q-mb-sm"
                  />
                  <div class="row q-col-gutter-sm">
                    <div class="col-6">
                      <q-input
                        v-model="authNetCard.expiry"
                        outlined dense
                        mask="##/##"
                        label="Expiry (MM/YY)"
                        placeholder="MM/YY"
                      />
                    </div>
                    <div class="col-6">
                      <q-input
                        v-model="authNetCard.cvc"
                        outlined dense
                        inputmode="numeric"
                        maxlength="4"
                        label="CVC"
                        placeholder="123"
                      />
                    </div>
                  </div>
                  <div class="row items-center text-caption text-grey-6 q-mt-xs no-wrap">
                    <q-icon name="o_lock" size="14px" class="q-mr-xs" />
                    Tokenized securely by Authorize.Net — the store never sees your card number.
                  </div>
                </template>

                <!-- Bank account (ACH / eCheck) -->
                <template v-else>
                  <div class="text-caption text-grey-7 q-mb-xs">Bank account details</div>
                  <q-input
                    v-model="authNetBank.nameOnAccount"
                    outlined dense
                    label="Name on account"
                    placeholder="Full name as it appears on the account"
                    class="q-mb-sm"
                  />
                  <div class="row q-col-gutter-sm q-mb-sm">
                    <div class="col-12 col-sm-6">
                      <q-input
                        v-model="authNetBank.routingNumber"
                        outlined dense
                        inputmode="numeric"
                        mask="#########"
                        unmasked-value
                        label="Routing number"
                        placeholder="9 digits"
                      />
                    </div>
                    <div class="col-12 col-sm-6">
                      <q-input
                        v-model="authNetBank.accountNumber"
                        outlined dense
                        inputmode="numeric"
                        maxlength="17"
                        label="Account number"
                        placeholder="Account number"
                      />
                    </div>
                  </div>
                  <q-select
                    v-model="authNetBank.accountType"
                    outlined dense
                    emit-value
                    map-options
                    :options="authNetAccountTypes"
                    label="Account type"
                  />
                  <div class="row items-center text-caption text-grey-6 q-mt-xs no-wrap">
                    <q-icon name="o_lock" size="14px" class="q-mr-xs" />
                    Tokenized securely by Authorize.Net (eCheck) — the store never sees your account number.
                  </div>
                </template>
              </template>
            </div>

            <div v-if="paymentMethods.length" class="row items-center text-caption text-grey-6 q-mt-sm no-wrap">
              <q-icon name="o_lock" size="16px" class="q-mr-xs" />
              Payments are processed securely — the storefront never stores your card details.
            </div>
          </q-card-section>
        </q-card>
      </div>

      <!-- Right: order summary -->
      <div class="col-12 col-md-5">
        <q-card flat bordered class="summary-card">
          <q-card-section>
            <div class="text-subtitle1 text-weight-bold q-mb-md">Order summary</div>

            <!-- Line items -->
            <div v-for="item in items" :key="item.id" class="row items-start q-mb-sm">
              <div class="col">
                <div class="text-body2">{{ item.productName }}</div>
                <div class="text-caption text-grey-6">Qty {{ item.quantity }} · {{ format(item.unitPrice) }}</div>
                <AppLineCustomAttributes :attributes="item.customAttributes" />
                <!-- Backordered line (AC-CHK-005.1) -->
                <div v-if="isBackordered(item)" class="q-mt-xs row items-center q-gutter-x-xs">
                  <span class="sf-badge sf-badge--backorder">Backordered</span>
                  <span v-if="restockDate(item)" class="text-caption text-orange-9">Ships by {{ restockDate(item) }}</span>
                </div>
              </div>
              <div class="text-body2 text-weight-medium">{{ format(item.lineTotal) }}</div>
            </div>

            <!-- Backorder notice + split-shipment option (AC-CHK-005.2 / AC-CHK-005.4) -->
            <div v-if="hasBackorder" class="sf-backorder-note q-mt-sm q-mb-xs">
              <div class="row items-start no-wrap">
                <q-icon name="o_schedule" size="18px" class="q-mr-xs q-mt-xs" />
                <div class="text-caption">Backordered items will ship separately as soon as they're back in stock.</div>
              </div>
              <q-toggle
                v-if="hasMixedFulfilment"
                v-model="splitShipment"
                dense
                class="q-mt-xs sf-split-toggle"
                label="Ship my in-stock items now in a separate shipment"
              />
            </div>

            <q-separator class="q-my-md" />

            <div v-if="appliedCouponCode" class="row items-center q-mb-sm">
              <q-chip
                dense
                color="green-1"
                text-color="green-9"
                icon="o_local_offer"
                :label="appliedCouponCode"
              />
            </div>

            <!-- Totals -->
            <div class="row items-center justify-between q-mb-xs">
              <span class="text-grey-8">
                Subtotal
                <SummaryHint text="Total price of all items — unit price × quantity — before discounts, shipping and tax." />
              </span>
              <span>{{ format(displaySubtotal) }}</span>
            </div>

            <template v-if="quote">
              <!-- Customer Group pricing (e.g. a 25% member discount) is applied by lowering unit prices,
                   so itemize the saving here — otherwise it is invisible against the list-priced lines. -->
              <div
                v-if="quote.groupDiscountTotal > 0"
                class="row items-center justify-between q-mb-xs text-green-8"
              >
                <span>
                  {{ quote.groupDiscountName || 'Customer group discount' }}
                  <SummaryHint text="A member discount for your customer group, applied by lowering item unit prices. It's already reflected in the totals below." />
                </span>
                <span>-{{ format(quote.groupDiscountTotal) }}</span>
              </div>
              <div
                v-for="d in quote.discounts"
                :key="d.discountId"
                class="row items-center justify-between q-mb-xs text-green-8"
              >
                <span>
                  {{ d.name || 'Discount' }}
                  <SummaryHint text="A promotion or coupon applied to your order. Discounts reduce the item total that sales tax is calculated on." />
                </span>
                <span>-{{ format(d.amount) }}</span>
              </div>
              <div
                v-if="quote.discountTotal > 0 && !quote.discounts.length"
                class="row items-center justify-between q-mb-xs text-green-8"
              >
                <span>
                  Discount
                  <SummaryHint text="A promotion or coupon applied to your order. Discounts reduce the item total that sales tax is calculated on." />
                </span>
                <span>-{{ format(quote.discountTotal) }}</span>
              </div>

              <div class="row items-center justify-between q-mb-xs">
                <span class="text-grey-8">
                  Shipping<span v-if="selectedShipping"> ({{ selectedShipping.name }})</span>
                  <SummaryHint>
                    The delivery charge for your selected method. Depending on your destination, shipping may itself be taxable.
                    <template v-if="shippingIntegrationName"><br>Rates are calculated by {{ shippingIntegrationName }}.</template>
                  </SummaryHint>
                </span>
                <span>{{ format(quote.shippingTotal) }}</span>
              </div>

              <div class="row items-center justify-between q-mb-xs">
                <span class="text-grey-8">
                  Tax
                  <SummaryHint>
                    Sales tax for your delivery address, calculated on your item subtotal after discounts, plus any taxable shipping.
                    <template v-if="taxProviderInfo"><br>Calculated by {{ taxProviderInfo.label }}.</template>
                  </SummaryHint>
                </span>
                <span>{{ format(quote.taxTotal) }}</span>
              </div>
              <div v-if="quote.tax && quote.tax.fallbackApplied" class="text-caption text-orange-8 q-mb-xs">
                Estimated tax — {{ taxProviderInfo ? `${taxProviderInfo.label} was unavailable, so a` : 'a' }} fallback rate was applied.
              </div>

              <!-- Payment transaction fee for the selected gateway (added to the total the buyer pays). -->
              <div v-if="paymentFee > 0" class="row items-center justify-between q-mb-xs">
                <span class="text-grey-8">
                  Payment fee<span v-if="selectedPaymentFeePercent"> ({{ selectedPaymentFeePercent }}%)</span>
                  <SummaryHint text="A processing fee charged by your selected payment method, calculated as a percentage of the order total." />
                </span>
                <span>{{ format(paymentFee) }}</span>
              </div>

              <!-- Reward points (WO-27) — signed-in buyers with a positive balance redeem points for a discount -->
              <template v-if="showLoyalty">
                <q-separator class="q-my-sm" />
                <div class="row items-center justify-between q-mb-xs">
                  <span class="text-grey-8 row items-center no-wrap">
                    <q-icon name="o_loyalty" size="16px" class="q-mr-xs text-primary" />
                    Reward points
                    <SummaryHint text="Redeem your loyalty points for money off. Points are applied to the total after tax, like a payment." />
                  </span>
                  <span class="text-caption text-grey-6">{{ pointsBalance }} available</span>
                </div>

                <template v-if="pointsApplied > 0">
                  <div class="row items-center justify-between q-mb-xs text-green-8">
                    <span>{{ pointsApplied }} points applied</span>
                    <span>-{{ format(pointsDiscount) }}</span>
                  </div>
                  <div class="q-mb-xs">
                    <q-btn flat dense no-caps size="sm" color="primary" label="Remove" :loading="pointsBusy" @click="removePoints" />
                  </div>
                </template>

                <div v-else class="q-mb-xs">
                  <q-input
                    v-model.number="pointsInput"
                    dense
                    outlined
                    type="number"
                    min="1"
                    :max="pointsBalance"
                    placeholder="Points to redeem"
                    :disable="pointsBusy"
                    @keyup.enter="applyPoints"
                  >
                    <template #append>
                      <q-btn
                        flat
                        dense
                        no-caps
                        color="primary"
                        label="Apply"
                        :loading="pointsBusy"
                        :disable="!canApplyPoints"
                        @click="applyPoints"
                      />
                    </template>
                  </q-input>
                </div>
              </template>

              <q-separator class="q-my-sm" />

              <div class="row items-center justify-between text-subtitle1 text-weight-bold">
                <span>
                  Total
                  <SummaryHint text="The final amount you pay: item subtotal, minus discounts, plus shipping, tax and any payment fee, minus reward points." />
                </span>
                <span>{{ format(grandTotal) }}</span>
              </div>
            </template>

            <div v-else-if="isInquiry" class="text-caption text-grey-6 q-mt-sm">
              This is a request, not an order — shipping, taxes and final pricing are confirmed in the
              quote we send you.
            </div>

            <div v-else class="text-caption text-grey-6 q-mt-sm">
              Shipping and taxes are calculated once your address is complete.
            </div>

            <!-- Submit request (REQ-INQ-001) -->
            <template v-if="isInquiry">
              <q-banner v-if="placeError" dense class="bg-red-1 text-red-9 rounded-borders q-mt-md">
                <template #avatar><q-icon name="o_error" color="red-9" /></template>
                {{ placeError }}
              </q-banner>

              <q-btn
                unelevated
                color="primary"
                class="full-width q-mt-md"
                no-caps
                label="Submit request"
                icon-right="o_send"
                :loading="placing"
                :disable="!canSubmitInquiry"
                @click="submitInquiry"
              />
              <div class="text-caption text-grey-6 text-center q-mt-sm">
                {{ submitNote || 'No payment is taken now — we\'ll confirm your request and send payment details.' }}
              </div>

              <q-btn
                flat
                no-caps
                color="primary"
                class="full-width q-mt-sm"
                icon="o_arrow_back"
                label="Back to cart"
                :to="{ name: 'shop-cart' }"
              />
            </template>

            <template v-else>

            <!-- Place order / retry -->
            <q-banner v-if="placeError" dense class="bg-red-1 text-red-9 rounded-borders q-mt-md">
              <template #avatar><q-icon name="o_error" color="red-9" /></template>
              {{ placeError }}
            </q-banner>

            <q-btn
              v-if="!(quote && isRoutable && !guestOrderingAllowed)"
              unelevated
              color="primary"
              class="full-width q-mt-md"
              no-caps
              :label="placeError ? 'Retry payment' : 'Place order'"
              icon-right="o_lock"
              :loading="placing"
              :disable="!canPlace"
              @click="placeOrder"
            />
            <q-btn
              v-else
              unelevated
              color="primary"
              class="full-width q-mt-md"
              no-caps
              label="Sign in to continue"
              :to="loginTo"
            />

            <q-btn
              flat
              no-caps
              color="primary"
              class="full-width q-mt-sm"
              icon="o_arrow_back"
              label="Back to cart"
              :to="{ name: 'shop-cart' }"
            />
            </template>
          </q-card-section>
        </q-card>
      </div>
    </div>
  </q-page>
</template>

<script setup>
/*
 * Storefront one-page checkout (WO-30, REQ-CHK-003). A single view that collects
 * the contact + delivery address and, as soon as it is complete, calls
 * checkout/quote to price the cart — routing result, shipping options, discounts,
 * tax and live totals (AC-CHK-003.7). Choosing a shipping method re-quotes so the
 * totals stay real-time. A payment-method selector plus a live order summary lead
 * to checkout/place, which shows an order-confirmation panel on success
 * (AC-CHK-003.8) or a retryable error on a declined payment (AC-PAY-001.3). When
 * the quote reports guest ordering is not allowed, the buyer is prompted to sign
 * in instead (AC-CHK-003.2 / AC-STR-001.5).
 */
import { ref, computed, watch, onMounted, nextTick } from 'vue'
import { LocalStorage } from 'quasar'
import { useRouter, useRoute } from 'vue-router'
import AppAddressForm from 'components/common/AppAddressForm.vue'
import AppTextField from 'components/common/AppTextField.vue'
import AppPhoneInput from 'components/common/AppPhoneInput.vue'
import SummaryHint from 'modules/storefront/components/SummaryHint.vue'
import { isPossiblePhoneNumber } from 'libphonenumber-js'
import { emptyAddress } from 'composables/useAddress'
import { checkoutApi } from 'modules/storefront/api'
import { pointsApi } from 'modules/storefront/checkout-extras-api'
import { customerAuthApi, accountApi } from 'modules/storefront/account-api'
import { useCustomerAuthStore } from 'stores/customerAuth'
import { useCart } from 'modules/storefront/composables/useCart'
import { useCurrency } from 'modules/storefront/composables/useCurrency'
import { useRecaptcha } from 'modules/storefront/composables/useRecaptcha'
import { useCommerceMode } from 'modules/storefront/composables/useCommerceMode'
import { useSquarePayment } from 'modules/storefront/composables/useSquarePayment'
import { useAuthorizeNetPayment } from 'modules/storefront/composables/useAuthorizeNetPayment'
import { useNotify } from 'composables/useNotify'
import { getApiErrorMessage } from 'services/api'

const { sessionId, cart, items, subtotal, appliedCouponCode, loading, refresh } = useCart()
const { format, load: loadCurrencies } = useCurrency()
const { getToken: getRecaptchaToken } = useRecaptcha()
const square = useSquarePayment()
const authNet = useAuthorizeNetPayment()
const notify = useNotify()
const router = useRouter()
const route = useRoute()
const customerAuth = useCustomerAuthStore()

// ---- Address form (standard AppAddressForm; email kept separate as the order contact) ----------
const formRef = ref(null)
const email = ref('')
const addr = ref(emptyAddress())

// ---- Fulfilment: deliver, or collect from a store (AC-SHP-004.1) --------------------------------
// Pickup skips routing and carrier rates entirely: the server fulfils at the chosen store and prices a
// single zero-cost "Pickup in store" option, so there is no delivery address to collect — only who is
// collecting. The choice is only offered when at least one store actually opts in to pickup.
const pickupStores = ref([])
const fulfilment = ref('deliver')
const pickupStoreId = ref(null)
const isPickup = computed(() => fulfilment.value === 'pickup')
const pickupOffered = computed(() => pickupStores.value.length > 0)
const fulfilmentOptions = [
  { label: 'Deliver to me', value: 'deliver', icon: 'o_local_shipping' },
  { label: 'Pick up in store', value: 'pickup', icon: 'o_storefront' }
]

async function loadPickupStores () {
  try {
    const stores = await checkoutApi.pickupStores()
    pickupStores.value = Array.isArray(stores) ? stores : []
    // Pre-select the only store rather than making the buyer click the one choice available.
    if (pickupStores.value.length === 1) pickupStoreId.value = pickupStores.value[0].id
  } catch (e) {
    // No pickup stores reachable → the option simply isn't offered; delivery is unaffected.
    pickupStores.value = []
  }
}

// ---- Signed-in address book (Amazon-style: pick a saved address or add a new one) ---------------
const isAuthed = computed(() => customerAuth.isAuthenticated)
const savedAddresses = ref([])
const selectedAddressId = ref('new') // a saved address id, or 'new' for manual entry
const profile = ref(null)
const profileEmail = ref('')
const saveNewAddress = ref(true)
const settingDefaultId = ref(null) // id of the saved address whose "Set as default" is in flight

// Show the manual form for guests, signed-in users with no saved address, or when "new" is chosen.
const showAddressForm = computed(() =>
  !isAuthed.value || !savedAddresses.value.length || selectedAddressId.value === 'new'
)
// Contact is always collected inline under pickup: there is no saved "pickup address" to select from, and
// the store still needs to know who is collecting.
const showContactForm = computed(() => isPickup.value || showAddressForm.value)

function fillFromAddress (a) {
  addr.value = {
    ...emptyAddress(),
    firstName: a.firstName || '', lastName: a.lastName || '', company: a.company || '',
    addressLine1: a.addressLine1 || '', addressLine2: a.addressLine2 || '', landmark: a.landmark || '',
    city: a.city || '', stateProvince: a.stateProvince || '', postalCode: a.postalCode || '',
    countryCode: a.countryCode || '', phoneNumber: a.phoneNumber || ''
  }
}
function selectSaved (a) {
  selectedAddressId.value = a.id
  fillFromAddress(a)
}
function selectNew () {
  selectedAddressId.value = 'new'
  addr.value = {
    ...emptyAddress(),
    firstName: profile.value?.firstName || '',
    lastName: profile.value?.lastName || '',
    phoneNumber: profile.value?.phoneNumber || ''
  }
}
function formatAddr (a) {
  return [a.addressLine1, a.addressLine2, a.landmark, a.city, a.stateProvince, a.postalCode, a.countryCode]
    .filter(Boolean).join(', ')
}

// Load the signed-in buyer's profile (contact email) + saved shipping addresses, and pre-select the
// default so checkout is one tap away.
async function loadAccountAddresses () {
  try {
    const [prof, book] = await Promise.all([
      accountApi.getProfile().catch(() => null),
      accountApi.addressBook().catch(() => null)
    ])
    profile.value = prof
    if (prof && prof.email) { email.value = prof.email; profileEmail.value = prof.email }
    const shipping = Array.isArray(book && book.shipping) ? book.shipping : []
    savedAddresses.value = shipping
    if (shipping.length) selectSaved(shipping.find((a) => a.isDefault) || shipping[0])
    else selectNew()
  } catch (e) {
    selectedAddressId.value = 'new'
  }
}

// Persist the just-entered delivery address to the signed-in buyer's address book. Returns the created
// address DTO (with its new id) so the caller can select it.
function saveCurrentAddress () {
  const a = addr.value
  return accountApi.createAddress({
    addressType: 'Shipping',
    isDefault: savedAddresses.value.length === 0,
    firstName: (a.firstName || '').trim(),
    lastName: (a.lastName || '').trim(),
    company: null,
    addressLine1: (a.addressLine1 || '').trim(),
    addressLine2: (a.addressLine2 || '').trim() || null,
    landmark: (a.landmark || '').trim() || null,
    city: (a.city || '').trim(),
    stateProvince: (a.stateProvince || '').trim() || null,
    postalCode: (a.postalCode || '').trim(),
    countryCode: (a.countryCode || '').toUpperCase().trim(),
    phoneNumber: (a.phoneNumber || '').trim() || null
  })
}

// Persist the current NEW address to the signed-in buyer's book when they calculate shipping (this is what
// the "Save & calculate shipping" action now does). Best-effort and guarded so recalculating never creates
// duplicates; a no-op for guests, when the "save" toggle is off, or when a saved address is already chosen.
async function maybeSaveNewAddress () {
  // Pickup collects no address — without this it would post the empty form to the buyer's address book.
  if (isPickup.value) return
  if (!isAuthed.value || !saveNewAddress.value || selectedAddressId.value !== 'new') return
  try {
    const created = await saveCurrentAddress()
    if (created && created.id) {
      // Insert + select the saved row WITHOUT touching `addr`, so the just-priced quote stays valid.
      // The optimistic insert survives a failed refresh; the refresh then replaces it with the
      // authoritative DTO from the address book.
      savedAddresses.value = [...savedAddresses.value, created]
      selectedAddressId.value = created.id
      await refreshSavedAddresses()
      notify.success('Address saved to your account')
    }
  } catch (e) { /* non-fatal: the order can still be placed with this address */ }
}

// Reload the saved shipping addresses (refreshes the Default badges) without disturbing the current
// selection or the entered address, so an in-progress quote is preserved.
async function refreshSavedAddresses () {
  try {
    const book = await accountApi.addressBook()
    if (Array.isArray(book && book.shipping)) savedAddresses.value = book.shipping
  } catch (e) { /* ignore */ }
}

// Make an already-saved address the default (active) delivery address. Uses the dedicated set-default
// endpoint (only the id) so it works even if the address is missing a field the full-update validator
// requires — flipping the default must not depend on re-validating the whole address.
async function setSavedAddressDefault (a) {
  if (a.isDefault || settingDefaultId.value) return
  settingDefaultId.value = a.id
  try {
    await accountApi.setDefaultAddress(a.id)
    // Move the default flag in the local list right away so the badge/button update immediately and
    // reliably — independent of any re-fetch (a cached address-book GET could otherwise show the old
    // default). The chosen address becomes the only default; all others lose it (one default per type).
    savedAddresses.value = savedAddresses.value.map((x) => ({ ...x, isDefault: x.id === a.id }))
    // Also make it the selected delivery address (fills the form) — setting default selects it too.
    selectSaved(a)
    notify.success('Default delivery address updated')
  } catch (e) {
    notify.error(getApiErrorMessage(e))
  } finally {
    settingDefaultId.value = null
  }
}

// ---- Guest contact persistence ----------------------------------------------
// Save the contact + delivery details to localStorage so a guest who must create an account can sign
// in and come straight back to a fully-filled checkout (cleared once the order is placed). Restored on
// mount for everyone, so returning after login/registration re-populates the form automatically.
const CHECKOUT_KEY = 'sf.checkout.contact'
function persistContact () {
  try {
    LocalStorage.set(CHECKOUT_KEY, { email: email.value, addr: addr.value })
  } catch (e) { /* storage disabled — non-fatal */ }
}
function restoreContact () {
  try {
    const saved = LocalStorage.getItem(CHECKOUT_KEY)
    if (saved && typeof saved === 'object') {
      if (typeof saved.email === 'string' && !email.value) email.value = saved.email
      if (saved.addr && typeof saved.addr === 'object') addr.value = { ...emptyAddress(), ...saved.addr }
    }
  } catch (e) { /* ignore malformed */ }
}
function clearContact () {
  try { LocalStorage.remove(CHECKOUT_KEY) } catch (e) { /* ignore */ }
  // Order completed — clear the persisted backorder flag and any redeemed reward points (callers that
  // need the backorder flag for the confirmation notice read it before calling this).
  try { LocalStorage.remove(BACKORDER_FLAG_KEY) } catch (e) { /* ignore */ }
  pointsApplied.value = 0
  pointsDiscountAmount.value = 0
}
watch([email, addr], persistContact, { deep: true })

const emailValid = computed(() => /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.value.trim()))

// Vuelidate-style field object driving AppTextField's inline error (stacked label, shown on blur).
const emailTouched = ref(false)
const emailV = computed(() => {
  const errors = []
  if (emailTouched.value) {
    const val = email.value.trim()
    if (!val) errors.push({ $message: 'Email is required' })
    else if (!emailValid.value) errors.push({ $message: 'Enter a valid email' })
  }
  return { $error: errors.length > 0, $errors: errors, $touch: () => { emailTouched.value = true } }
})

// The phone emits E.164 when valid, otherwise raw digits; isValidPhoneNumber gates on a real number.
const phoneValid = computed(() => {
  const p = (addr.value.phoneNumber || '').trim()
  return !!p && isPossiblePhoneNumber(p)
})

// Who the order is for — needed either way, and all pickup needs.
const contactComplete = computed(() => {
  const a = addr.value
  return (
    !!(a.firstName || '').trim() &&
    !!(a.lastName || '').trim() &&
    emailValid.value &&
    phoneValid.value
  )
})

const requiredComplete = computed(() => {
  if (!contactComplete.value) return false
  // Collecting in store, or an inquiry that only collects contact details — nothing is shipped
  // anywhere, so a postal address would be noise.
  if (isPickup.value || contactOnly.value) return true
  const a = addr.value
  return (
    !!(a.addressLine1 || '').trim() &&
    !!(a.stateProvince || '').trim() &&
    !!(a.postalCode || '').trim() &&
    !!a.countryCode
  )
})


// ---- Inquiry mode (REQ-INQ-001) ---------------------------------------------
// Either the whole tenant sells by inquiry, or the cart holds a quote-only product. In inquiry mode this
// page collects contact details + a requirement and posts them; there is no quote step, no shipping
// selection, no payment and no tax — the server prices the request and mails it to the sales team.
const { isInquiryOnly, collectAddress, submitNote } = useCommerceMode()

const cartHasInquiryItem = computed(() => items.value.some((i) => i.isInquiryOnly === true))
const isInquiry = computed(() => isInquiryOnly.value || cartHasInquiryItem.value)
// No postal address is collected when the tenant asked for contact details only — the name/email/phone
// still reach the order's address row, which is where the notifications read the contact from.
const contactOnly = computed(() => isInquiry.value && !collectAddress.value)

const contactSectionTitle = computed(() => {
  if (isPickup.value) return 'Contact & pickup store'
  if (contactOnly.value) return 'Your contact details'
  if (isInquiry.value) return 'Contact & delivery address'
  return 'Contact & delivery address'
})

const companyName = ref('')
const preferredContact = ref('Email')
const requiredBy = ref('')
const inquiryMessage = ref('')
const inquiryResult = ref(null)
const contactPreferenceOptions = [
  { label: 'Email', value: 'Email' },
  { label: 'Phone', value: 'Phone' },
  { label: 'WhatsApp', value: 'WhatsApp' }
]

// An inquiry needs contact details and a cart — nothing else. Deliberately not gated on a quote: there is
// no routing, shipping or tax to resolve first, so making the buyer press "calculate" would be theatre.
const canSubmitInquiry = computed(
  () => isInquiry.value && requiredComplete.value && items.value.length > 0 && !placing.value
)

async function submitInquiry () {
  placeError.value = ''
  const ok = await formRef.value.validate()
  if (!ok || !canSubmitInquiry.value) return
  placing.value = true
  try {
    const recaptchaToken = await getRecaptchaToken('inquiry')
    const result = await checkoutApi.submitInquiry({
      cartId: null,
      sessionId: sessionId.value,
      contact: buildShipTo(),
      message: inquiryMessage.value.trim() || null,
      companyName: companyName.value.trim() || null,
      preferredContact: preferredContact.value || null,
      // <input type="date"> yields a plain YYYY-MM-DD; send it as UTC midnight.
      requiredByUtc: requiredBy.value ? new Date(requiredBy.value + 'T00:00:00Z').toISOString() : null,
      couponCode: null,
      recaptchaToken
    })
    if (result && result.success) {
      inquiryResult.value = result
      clearContact()
      // The cart was consumed server-side; refresh so the badge resets.
      await refresh().catch(() => {})
      window.scrollTo({ top: 0, behavior: 'smooth' })
    } else {
      placeError.value = 'Your request could not be submitted. Please try again.'
      notify.error(placeError.value)
    }
  } catch (err) {
    placeError.value = getApiErrorMessage(err)
    notify.error(placeError.value)
  } finally {
    placing.value = false
  }
}

// ---- Quote ------------------------------------------------------------------
const quote = ref(null)
const quoting = ref(false)
const quoteError = ref('')
const selectedShippingMethodId = ref(null)

// The summary's "Subtotal" row shows the list-price subtotal so the itemized customer-group discount
// reconciles with the (list-priced) line items above it; the group saving is then subtracted on its own
// line. Falls back to the member subtotal when there is no group saving, and to the cart before a quote.
const displaySubtotal = computed(() => {
  if (!quote.value) return subtotal.value
  return quote.value.groupDiscountTotal > 0 ? quote.value.baseSubtotal : quote.value.subtotal
})

// ---- Backorder (WO-86) ------------------------------------------------------
// A line is backordered when orderable ONLY via backorder: still purchasable (available) but out of stock
// with backorder allowed. In-stock and hard-unavailable lines are both excluded.
// Quote lines are never sold from stock (REQ-INQ-001), so they are never backordered either.
function isBackordered (item) {
  return (
    !isInquiryOnly.value &&
    item.isInquiryOnly !== true &&
    !!item.allowBackorder &&
    item.available !== false &&
    item.stockQuantity != null &&
    item.stockQuantity <= 0
  )
}
// Estimated restock date isn't on the cart line DTO today — degrade to empty until the backend sends it.
function restockDate (item) {
  const raw = item.estimatedRestockDate
  if (!raw) return ''
  const d = new Date(raw)
  if (Number.isNaN(d.getTime())) return ''
  return d.toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' })
}
const hasBackorder = computed(() => items.value.some((i) => isBackordered(i)))
const hasInStock = computed(() => items.value.some((i) => i.available !== false && !isBackordered(i)))
// The split-shipment option is only meaningful when the cart mixes in-stock and backordered items.
const hasMixedFulfilment = computed(() => hasBackorder.value && hasInStock.value)
// UI-only preference passed along in the place payload (AC-CHK-005.2); the backend may ignore it for now.
const splitShipment = ref(false)
// Snapshot of "did this order contain backordered items", captured at placement so the confirmation notice
// survives the cart being emptied afterwards (persisted for the redirect-gateway return, which reloads).
const BACKORDER_FLAG_KEY = 'sf.checkout.backorder'
const orderHadBackorder = ref(false)

const shippingOptions = computed(() => quote.value?.shippingOptions || [])
// An empty option list never means "nothing to ship": no product can be marked non-shippable, and pickup
// prices its own zero-cost option. It always means no rate source could quote this address — a broken
// shipping setup or an unserviceable destination — so it blocks placement rather than waving the order
// through with no carrier and no shipping cost.
const shippingUnavailable = computed(
  () => !!quote.value && !quoting.value && !shippingOptions.value.length
)

// The server recommends at most one option, and only when selection is automatic. When it does, show that
// one and collapse the rest; otherwise every option is primary and the expander stays empty.
const recommendedShipping = computed(() => shippingOptions.value.find((o) => o.isRecommended) || null)
const primaryShippingOptions = computed(() =>
  recommendedShipping.value ? [recommendedShipping.value] : shippingOptions.value
)
const otherShippingOptions = computed(() =>
  recommendedShipping.value ? shippingOptions.value.filter((o) => !o.isRecommended) : []
)
const showOtherShipping = ref(false)

function deliveryText (opt) {
  const days = opt.estimatedDeliveryDays
  if (days == null) return ''
  if (days === 0) return 'same day'
  return days === 1 ? 'arrives in 1 day' : `arrives in ${days} days`
}

// ---- Active calculation partners (shipping carriers + tax provider), shown like the payment partners ----
// Distinct carriers behind the current shipping options — each option already reports its carrier.
const CARRIER_META = {
  Custom: { label: 'Store rates', icon: 'o_store' },
  Pickup: { label: 'Store pickup', icon: 'o_storefront' },
  USPS: { label: 'USPS', icon: 'o_local_post_office' },
  UPS: { label: 'UPS', icon: 'o_local_shipping' },
  FedEx: { label: 'FedEx', icon: 'o_local_shipping' },
  'DHL Express': { label: 'DHL Express', icon: 'o_local_shipping' }
}
const shippingCarriers = computed(() => {
  const seen = new Set()
  const out = []
  for (const o of shippingOptions.value) {
    if (!o.carrier || seen.has(o.carrier)) continue
    seen.add(o.carrier)
    out.push({ carrier: o.carrier, ...(CARRIER_META[o.carrier] || { label: o.carrier, icon: 'o_local_shipping' }) })
  }
  return out
})

// The active tax provider reported by the quote, mapped to a label + icon for the summary.
const TAX_PROVIDER_META = {
  TaxJar: { label: 'TaxJar', icon: 'o_receipt_long' },
  StripeTax: { label: 'Stripe Tax', icon: 'o_credit_card' },
  FlatRate: { label: 'Flat rate', icon: 'o_percent' }
}
const taxProviderInfo = computed(() => {
  const p = quote.value?.taxProvider
  return p ? (TAX_PROVIDER_META[p] || { label: p, icon: 'o_account_balance' }) : null
})
const isRoutable = computed(() => !!quote.value?.isRoutable)
const guestOrderingAllowed = computed(() => (quote.value ? quote.value.guestOrderingAllowed !== false : true))
const canCollectDetails = computed(() => !!quote.value && isRoutable.value && guestOrderingAllowed.value)
const selectedShipping = computed(
  () => shippingOptions.value.find((o) => o.methodId === selectedShippingMethodId.value) || null
)
// Friendly name of the integration/carrier behind the selected shipping method (summary tooltip).
const shippingIntegrationName = computed(() => {
  const c = selectedShipping.value?.carrier
  return c ? (CARRIER_META[c] || { label: c }).label : null
})
const canQuote = computed(() =>
  requiredComplete.value && items.value.length > 0 && (!isPickup.value || !!pickupStoreId.value)
)

// Mode-aware calls to action: under pickup there is no shipping to calculate, only a total.
const quoteLabel = computed(() => {
  if (isPickup.value) return quote.value ? 'Recalculate total' : 'Calculate total'
  return quote.value ? 'Recalculate shipping' : 'Save & calculate shipping'
})

// ---- Guest → account (express registration) ---------------------------------
// When a store disallows guest ordering, turn the already-entered checkout details into an account:
// the backend creates a verified customer, emails a password and saves the delivery address. The buyer
// signs in and returns here (details preserved) to place the order.
const registering = ref(false)
const loginTo = computed(() => ({ name: 'shop-login', query: { redirect: route.fullPath } }))

async function registerAndContinue () {
  if (!canQuote.value || registering.value) return
  registering.value = true
  try {
    const recaptchaToken = await getRecaptchaToken('register')
    const a = addr.value
    const res = await customerAuthApi.registerAtCheckout({
      email: email.value.trim(),
      firstName: (a.firstName || '').trim(),
      lastName: (a.lastName || '').trim(),
      phoneNumber: (a.phoneNumber || '').trim() || null,
      addressLine1: (a.addressLine1 || '').trim(),
      addressLine2: (a.addressLine2 || '').trim() || null,
      landmark: (a.landmark || '').trim() || null,
      city: (a.city || '').trim(),
      stateProvince: (a.stateProvince || '').trim() || null,
      postalCode: (a.postalCode || '').trim(),
      countryCode: a.countryCode,
      recaptchaToken
    })
    persistContact()
    notify.success(
      `We've emailed a password to ${res.email}. Sign in to finish your order — your details are saved.`
    )
    router.push({ name: 'shop-login', query: { redirect: route.fullPath } })
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    registering.value = false
  }
}

function buildShipTo () {
  const a = addr.value
  const contact = {
    firstName: (a.firstName || '').trim(),
    lastName: (a.lastName || '').trim(),
    email: email.value.trim(),
    phoneNumber: (a.phoneNumber || '').trim() || null
  }
  // A pickup order, and a contact-only inquiry, both ship nowhere: send the contact details with no
  // postal address rather than stamping the record with one that was never used.
  if (isPickup.value || contactOnly.value) {
    return { ...contact, line1: '', line2: null, city: '', region: null, postalCode: null, countryCode: '', landmark: null }
  }
  return {
    ...contact,
    line1: (a.addressLine1 || '').trim(),
    line2: (a.addressLine2 || '').trim() || null,
    city: (a.city || '').trim(),
    region: (a.stateProvince || '').trim() || null,
    postalCode: (a.postalCode || '').trim() || null,
    countryCode: a.countryCode,
    landmark: (a.landmark || '').trim() || null
  }
}

async function runQuote () {
  if (!canQuote.value) return
  quoting.value = true
  quoteError.value = ''
  try {
    const q = await checkoutApi.quote({
      cartId: null,
      sessionId: sessionId.value,
      shipTo: buildShipTo(),
      shippingMethodId: selectedShippingMethodId.value || null,
      pickupInStore: isPickup.value,
      pickupStoreId: isPickup.value ? pickupStoreId.value : null
    })
    quote.value = q
    // Keep a valid shipping selection, defaulting to whatever the quote actually priced. This mirrors the
    // server's rule (recommended, else cheapest) — if the two disagreed, the radio would show one option
    // while the totals reflected another.
    const opts = q.shippingOptions || []
    if (!opts.some((o) => o.methodId === selectedShippingMethodId.value)) {
      const recommended = opts.find((o) => o.isRecommended)
      const cheapest = [...opts].sort((a, b) => (a.rate ?? 0) - (b.rate ?? 0))[0]
      const fallback = recommended || cheapest
      selectedShippingMethodId.value = fallback ? fallback.methodId : null
    }
    // Keep a selection made from the collapsed group visible rather than appearing to lose it.
    if (selectedShippingMethodId.value && opts.some((o) => o.methodId === selectedShippingMethodId.value && !o.isRecommended) &&
        opts.some((o) => o.isRecommended)) {
      showOtherShipping.value = true
    }
    // Shipping is calculated → persist the new address to the signed-in buyer's account (best-effort).
    await maybeSaveNewAddress()
  } catch (err) {
    quote.value = null
    quoteError.value = getApiErrorMessage(err)
  } finally {
    quoting.value = false
  }
}

// The buyer explicitly calculates shipping via the button. Any change to a pricing-relevant field
// afterwards invalidates the quote so they must recalculate before Place order re-enables.
const quoteSignature = computed(() => {
  const a = addr.value
  return JSON.stringify([
    (a.addressLine1 || '').trim(),
    (a.addressLine2 || '').trim(),
    (a.city || '').trim(),
    (a.stateProvince || '').trim(),
    (a.postalCode || '').trim(),
    a.countryCode || '',
    email.value.trim(),
    // Switching fulfilment, or collecting from a different store, re-prices everything (tax included).
    fulfilment.value,
    pickupStoreId.value || ''
  ])
})

watch(quoteSignature, () => {
  quote.value = null
  quoteError.value = ''
  selectedShippingMethodId.value = null
  // The payment section (and the Square card's container) unmounts with the quote — drop the card field so a
  // fresh quote re-attaches a new one rather than leaving it bound to a removed element.
  square.destroy()
})

// Selecting a different shipping method re-quotes immediately for real-time totals.
function onShippingChange (methodId) {
  selectedShippingMethodId.value = methodId
  runQuote()
}

// ---- Payment ----------------------------------------------------------------
// Label + icon for every supported gateway. The checkout shows only the methods the quote reports as
// available (active/enabled gateways + Cash on Delivery when the fulfilling store allows it).
const PAYMENT_METHOD_META = {
  CashOnDelivery: { label: 'Cash on delivery', icon: 'o_payments', hint: 'Pay in cash when your order is delivered' },
  Stripe: { label: 'Stripe', icon: 'o_credit_card', hint: 'Visa, Mastercard, Amex & more — via Stripe' },
  PayPal: { label: 'PayPal', icon: 'o_account_balance_wallet', hint: 'Pay with your PayPal account' },
  Razorpay: { label: 'Razorpay', icon: 'o_payment', hint: 'Cards, UPI, netbanking & wallets' },
  Square: { label: 'Square', icon: 'o_qr_code_2', hint: 'Secure card payment via Square' },
  AuthorizeNet: { label: 'Authorize.Net', icon: 'o_credit_score', hint: 'Secure card payment' },
  BankTransfer: { label: 'Bank transfer', icon: 'o_account_balance', hint: 'Transfer to our bank account; we confirm once received' }
}
// The quote reports each offered method with its live/sandbox mode: { method, isProduction }
// (isProduction is null for manual methods like COD/Bank transfer, which have no gateway environment).
const paymentMethods = computed(() =>
  (quote.value?.availablePaymentMethods || [])
    .filter((m) => PAYMENT_METHOD_META[m.method])
    .map((m) => ({ value: m.method, isProduction: m.isProduction, feePercent: m.feePercent || 0, ...PAYMENT_METHOD_META[m.method] }))
)
const paymentMethod = ref(null)

// Transaction fee for the selected gateway: a percent of the order total, added to what the buyer pays.
// The server re-resolves and re-applies this authoritatively at placement; this mirrors it for the preview.
const selectedPaymentFeePercent = computed(() => {
  const m = paymentMethods.value.find((x) => x.value === paymentMethod.value)
  return m ? m.feePercent || 0 : 0
})
const paymentFee = computed(() => {
  if (!quote.value) return 0
  return Math.round(quote.value.total * selectedPaymentFeePercent.value) / 100
})
// ---- Reward points (WO-27) --------------------------------------------------
// Signed-in buyers with a positive balance can redeem points for a discount. The balance comes from
// GET /api/customer/points; apply/remove hit the checkout endpoints and return the resulting discount, which
// is shown as its own summary row and folded into the grand total here (the backend re-applies it to the
// order total authoritatively at placement, so the quote total stays pre-points).
const pointsBalance = ref(0)
const pointsApplied = ref(0)
const pointsDiscountAmount = ref(0)
const pointsInput = ref(null)
const pointsBusy = ref(false)
const showLoyalty = computed(() => isAuthed.value && (pointsBalance.value > 0 || pointsApplied.value > 0))
const pointsDiscount = computed(() => (pointsApplied.value > 0 ? pointsDiscountAmount.value || 0 : 0))
const canApplyPoints = computed(() => {
  const n = Number(pointsInput.value)
  return Number.isInteger(n) && n > 0 && n <= pointsBalance.value && !pointsBusy.value
})

const grandTotal = computed(() => {
  if (!quote.value) return 0
  const total = quote.value.total + paymentFee.value - pointsDiscount.value
  return total > 0 ? total : 0
})
// Keep the selection valid as availability changes — default to the first offered method.
watch(paymentMethods, (methods) => {
  if (!methods.length) paymentMethod.value = null
  else if (!methods.some((m) => m.value === paymentMethod.value)) paymentMethod.value = methods[0].value
}, { immediate: true })

// ---- Square on-site card field (Web Payments SDK) ---------------------------
// Square takes the card on-site: a hosted field is rendered here and tokenized into a single-use nonce on
// Place order (the nonce is sent as the paymentToken; the store never sees the card number).
const squareConfig = square.config
const squareCardError = ref('')
// The quote offers Square as soon as its credential exists, but on-site card entry also needs the public
// Application Id + Location Id. When those are missing the field can't render, so placement is blocked.
const squareNotConfigured = computed(
  () => paymentMethod.value === 'Square' && !!squareConfig.value && squareConfig.value.configured === false
)

// Mount the card field once Square is the chosen method and the payment section is on screen.
async function ensureSquareCard () {
  if (paymentMethod.value !== 'Square' || !canCollectDetails.value) return
  squareCardError.value = ''
  await nextTick()
  const ok = await square.mount('#sq-card-container')
  // A not-configured integration is surfaced by squareNotConfigured; only report an actual load failure here.
  if (!ok && !(squareConfig.value && squareConfig.value.configured === false)) {
    squareCardError.value = 'Could not load the Square card field. Check your connection and try again.'
  }
}

watch([paymentMethod, canCollectDetails], ensureSquareCard)

// ---- Authorize.Net on-site card fields (Accept.js) --------------------------
// Authorize.Net takes the card in plain fields; on Place order Accept.js tokenizes them into a single-use
// nonce (sent as the paymentToken) in the browser, so the raw card number never reaches our server.
const authNetConfig = authNet.config
const authNetCard = ref({ number: '', expiry: '', cvc: '' })
// Authorize.Net offers two instruments on the same credential: a card or a bank account (ACH/eCheck). The
// buyer picks one; both tokenize through Accept.js into the same kind of nonce and the server is told which.
const authNetInstrument = ref('card') // 'card' | 'ach'
const authNetInstrumentOptions = [
  { label: 'Credit / debit card', value: 'card', icon: 'o_credit_card' },
  { label: 'Bank account (ACH)', value: 'ach', icon: 'o_account_balance' }
]
// Labels shown to the buyer → the accountType values Accept.js expects for bankData.
const authNetAccountTypes = [
  { label: 'Checking', value: 'checking' },
  { label: 'Savings', value: 'savings' },
  { label: 'Business checking', value: 'businessChecking' }
]
const authNetBank = ref({ nameOnAccount: '', routingNumber: '', accountNumber: '', accountType: 'checking' })
// Offered as soon as its credential exists, but on-site entry also needs the public API Login ID + Client Key.
// When those are missing Accept.js can't tokenize, so placement is blocked (mirrors squareNotConfigured).
const authNetNotConfigured = computed(
  () => paymentMethod.value === 'AuthorizeNet' && !!authNetConfig.value && authNetConfig.value.configured === false
)

// Start loading Accept.js as soon as Authorize.Net is chosen and the payment section is on screen, so the
// library is fully initialised before Place order tokenizes (mirrors Square mounting its field on select).
async function ensureAuthNetSdk () {
  if (paymentMethod.value !== 'AuthorizeNet' || !canCollectDetails.value) return
  authNet.preload().catch(() => {})
}
watch([paymentMethod, canCollectDetails], ensureAuthNetSdk)

// Load the signed-in buyer's reward-points balance. A missing/disabled programme (404/501) simply leaves the
// balance at 0 so the loyalty UI never shows — never a red error on the checkout.
async function loadPoints () {
  if (!isAuthed.value) return
  try {
    const res = await pointsApi.balance()
    pointsBalance.value = Number(res?.balance ?? res?.pointsBalance ?? res?.points ?? 0) || 0
  } catch (e) {
    pointsBalance.value = 0
  }
}

async function applyPoints () {
  if (!canApplyPoints.value) return
  pointsBusy.value = true
  try {
    const res = await pointsApi.apply({ points: Number(pointsInput.value), sessionId: sessionId.value })
    pointsApplied.value = Number(res?.pointsApplied) || 0
    pointsDiscountAmount.value = Number(res?.pointsDiscountAmount) || 0
    if (res && typeof res.remainingBalance === 'number') pointsBalance.value = res.remainingBalance
    pointsInput.value = null
    notify.success(`${pointsApplied.value} reward points applied`)
    // Refresh the summary so shipping/tax/total are server-fresh; the points row overlays on top.
    if (quote.value) await runQuote()
  } catch (err) {
    // Insufficient points, exceeds subtotal, etc. — surface the server message.
    notify.error(getApiErrorMessage(err))
  } finally {
    pointsBusy.value = false
  }
}

async function removePoints () {
  if (pointsBusy.value) return
  pointsBusy.value = true
  try {
    await pointsApi.remove({ sessionId: sessionId.value })
    pointsApplied.value = 0
    pointsDiscountAmount.value = 0
    notify.info('Reward points removed')
    await loadPoints() // authoritative balance after the points are released
    if (quote.value) await runQuote()
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    pointsBusy.value = false
  }
}

// ---- Place ------------------------------------------------------------------
const placing = ref(false)
const placeError = ref('')
const orderResult = ref(null)
// Redirect-payment (Stripe Checkout) return handling.
const confirming = ref(false)
const retrying = ref(false)
const retryState = ref(null) // { orderId, message } while a payment is cancelled/unconfirmed

const canPlace = computed(
  () =>
    !!quote.value &&
    isRoutable.value &&
    guestOrderingAllowed.value &&
    items.value.length > 0 &&
    !!selectedShippingMethodId.value &&
    !!paymentMethod.value &&
    !squareNotConfigured.value &&
    !authNetNotConfigured.value &&
    !placing.value
)

async function placeOrder () {
  placeError.value = ''
  const ok = await formRef.value.validate()
  if (!ok || !canPlace.value) return
  placing.value = true
  // Snapshot backorder state now — the cart is emptied after a successful placement, so the confirmation
  // notice reads this rather than the (then-empty) cart. Persisted too, so it survives a gateway redirect.
  orderHadBackorder.value = hasBackorder.value
  try {
    if (hasBackorder.value) LocalStorage.set(BACKORDER_FLAG_KEY, true)
    else LocalStorage.remove(BACKORDER_FLAG_KEY)
  } catch (e) { /* storage disabled — non-fatal */ }
  try {
    // Signed-in buyer entering a new address who opted to save it → add it to their account (best-effort).
    if (!isPickup.value && isAuthed.value && selectedAddressId.value === 'new' && saveNewAddress.value) {
      await saveCurrentAddress().catch(() => {})
    }
    // Square: tokenize the entered card into a single-use nonce (source id) before placing — the server
    // charges this token. A tokenization error (empty/invalid card) stops placement with a clear message;
    // the finally block resets `placing` on the early return.
    let paymentToken = null
    if (paymentMethod.value === 'Square') {
      try {
        paymentToken = await square.tokenize()
      } catch (e) {
        placeError.value = e.message || 'Please check your card details and try again.'
        notify.error(placeError.value)
        return
      }
    }
    // Authorize.Net: tokenize the entered card OR bank account into an Accept.js nonce before placing — the
    // server charges this token. A tokenization error (empty/invalid details) stops placement with a clear
    // message. The instrument chosen here is sent alongside so the server applies the right (card/eCheck) rules.
    if (paymentMethod.value === 'AuthorizeNet') {
      try {
        paymentToken = authNetInstrument.value === 'ach'
          ? await authNet.tokenizeBank(authNetBank.value)
          : await authNet.tokenize(authNetCard.value)
      } catch (e) {
        placeError.value = e.message || 'Please check your payment details and try again.'
        notify.error(placeError.value)
        return
      }
    }
    // reCAPTCHA token for the guest-checkout form (no-op when reCAPTCHA is disabled/unconfigured).
    const recaptchaToken = await getRecaptchaToken('guestCheckout')
    const result = await checkoutApi.place({
      cartId: null,
      sessionId: sessionId.value,
      shipTo: buildShipTo(),
      selectedShippingMethodId: selectedShippingMethodId.value,
      paymentMethod: paymentMethod.value,
      paymentToken,
      // Only Authorize.Net distinguishes instruments; 'BankAccount' selects the ACH/eCheck path server-side.
      paymentInstrument:
        paymentMethod.value === 'AuthorizeNet' && authNetInstrument.value === 'ach' ? 'BankAccount' : null,
      couponCode: null,
      recaptchaToken,
      pickupInStore: isPickup.value,
      pickupStoreId: isPickup.value ? pickupStoreId.value : null,
      // UI-only preference for mixed carts (AC-CHK-005.2); harmless extra field the backend may ignore.
      splitShipment: hasMixedFulfilment.value ? splitShipment.value : false
    })
    if (result && result.redirectUrl) {
      // Redirect gateway (Stripe Checkout): hand off to the hosted payment page. The buyer returns to
      // this page with ?order=…&session_id=… (success) or ?order=…&cancelled=1, handled on mount.
      window.location.href = result.redirectUrl
      return
    }
    if (result && result.clientPayment) {
      // On-site widget gateway (Razorpay Checkout): open the widget; payment is verified in its handler.
      await openClientPaymentWidget(result)
      return
    }
    if (result && result.success) {
      orderResult.value = result
      // Order placed — the saved checkout details are no longer needed.
      clearContact()
      // The cart is now checked out; refreshing yields a fresh empty cart (badge resets).
      await refresh().catch(() => {})
      window.scrollTo({ top: 0, behavior: 'smooth' })
    } else {
      placeError.value = (result && result.error) || 'Payment could not be processed. Please try again.'
      notify.error(placeError.value)
    }
  } catch (err) {
    placeError.value = getApiErrorMessage(err)
    notify.error(placeError.value)
  } finally {
    placing.value = false
  }
}

// ---- On-site payment widget (Razorpay Checkout) -----------------------------
// checkout.js is loaded on demand — only when a Razorpay payment is actually placed — and once per page.
let razorpayScriptPromise = null
function loadRazorpayScript () {
  if (window.Razorpay) return Promise.resolve(true)
  if (!razorpayScriptPromise) {
    razorpayScriptPromise = new Promise((resolve) => {
      const s = document.createElement('script')
      s.src = 'https://checkout.razorpay.com/v1/checkout.js'
      s.async = true
      s.onload = () => resolve(true)
      s.onerror = () => { razorpayScriptPromise = null; resolve(false) }
      document.head.appendChild(s)
    })
  }
  return razorpayScriptPromise
}

// Opens the provider's on-site widget for a placed-but-unpaid order. The server already created the
// provider order and returned its config in `placeResult.clientPayment`; the buyer pays inside the widget
// and its result is verified server-side in confirmClientPayment. The order stays pending until then.
async function openClientPaymentWidget (placeResult) {
  const cp = placeResult.clientPayment
  if (!cp || cp.provider !== 'razorpay') {
    placeError.value = 'This payment method is not supported here. Please choose another.'
    notify.error(placeError.value)
    return
  }

  const loaded = await loadRazorpayScript()
  if (!loaded || !window.Razorpay) {
    placeError.value = 'Could not load Razorpay Checkout. Check your connection and try again.'
    notify.error(placeError.value)
    retryState.value = { orderId: placeResult.orderId, message: placeError.value }
    return
  }

  const rzp = new window.Razorpay({
    key: cp.keyId,
    order_id: cp.gatewayOrderId,
    amount: cp.amountMinor,
    currency: cp.currencyCode,
    description: `Order ${cp.orderNumber}`,
    prefill: {
      name: cp.customerName || '',
      email: cp.customerEmail || '',
      contact: cp.customerPhone || ''
    },
    handler (response) {
      // Buyer completed payment — verify + capture server-side.
      confirmClientPayment(placeResult.orderId, response)
    },
    modal: {
      // Buyer dismissed the widget without paying — the order stays pending and is retryable.
      ondismiss () {
        retryState.value = {
          orderId: placeResult.orderId,
          message: 'Your payment was not completed. You can try again.'
        }
      }
    }
  })
  rzp.on('payment.failed', (response) => {
    const err = (response && response.error) || {}
    // Full gateway error (code / step / reason / source) pinpoints a rejection; description is buyer-facing.
    console.error('Razorpay payment.failed:', err)
    const msg = err.description || 'Your payment could not be processed.'
    notify.error(msg)
    retryState.value = { orderId: placeResult.orderId, message: msg }
  })
  rzp.open()
}

// Verifies an on-site widget result server-side (signature + capture) and finalizes on success.
async function confirmClientPayment (orderId, response) {
  confirming.value = true
  placeError.value = ''
  try {
    const result = await checkoutApi.confirmClientPayment(orderId, {
      razorpay_payment_id: response.razorpay_payment_id,
      razorpay_order_id: response.razorpay_order_id,
      razorpay_signature: response.razorpay_signature
    })
    if (result && result.success) {
      retryState.value = null
      orderResult.value = result
      clearContact()
      await refresh().catch(() => {})
      window.scrollTo({ top: 0, behavior: 'smooth' })
    } else {
      retryState.value = { orderId, message: (result && result.error) || 'We could not confirm your payment.' }
    }
  } catch (err) {
    retryState.value = { orderId, message: getApiErrorMessage(err) }
  } finally {
    confirming.value = false
  }
}

// Handle the buyer's return from a redirect gateway (Stripe Checkout). Success carries ?order&session_id
// (verify the payment server-side and finalize); ?cancelled shows the retry card. The payment params are
// stripped from the URL so a refresh doesn't re-trigger.
async function handlePaymentReturn () {
  const q = route.query || {}
  const orderId = q.order
  if (!orderId) return

  const cleanQuery = { ...q }
  delete cleanQuery.order
  delete cleanQuery.session_id
  delete cleanQuery.cancelled
  router.replace({ query: cleanQuery }).catch(() => {})

  if (q.cancelled) {
    retryState.value = { orderId, message: 'Your payment was cancelled before it completed.' }
    return
  }

  confirming.value = true
  try {
    const result = await checkoutApi.confirm(orderId)
    if (result && result.success) {
      // Restore the backorder snapshot persisted before the redirect so the confirmation notice still shows.
      try { if (LocalStorage.getItem(BACKORDER_FLAG_KEY)) orderHadBackorder.value = true } catch (e) { /* ignore */ }
      orderResult.value = result
      clearContact()
      await refresh().catch(() => {})
      window.scrollTo({ top: 0, behavior: 'smooth' })
    } else {
      retryState.value = { orderId, message: (result && result.error) || 'We could not confirm your payment.' }
    }
  } catch (err) {
    retryState.value = { orderId, message: getApiErrorMessage(err) }
  } finally {
    confirming.value = false
  }
}

// Re-open a payment session for the pending order and send the buyer back to the gateway.
async function retryPayment () {
  if (!retryState.value) return
  retrying.value = true
  try {
    const result = await checkoutApi.retryPayment(retryState.value.orderId)
    if (result && result.redirectUrl) {
      window.location.href = result.redirectUrl
      return
    }
    if (result && result.clientPayment) {
      // On-site widget gateway (Razorpay Checkout): re-open the widget for the same pending order.
      retryState.value = null
      await openClientPaymentWidget(result)
      return
    }
    if (result && result.success) {
      // The order was already paid — show the confirmation instead.
      orderResult.value = result
      retryState.value = null
      await refresh().catch(() => {})
    } else {
      notify.error((result && result.error) || 'Could not restart payment. Please try again.')
    }
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    retrying.value = false
  }
}

onMounted(() => {
  loadCurrencies()
  refresh().catch((err) => notify.error(getApiErrorMessage(err)))
  // Returning from a redirect payment (Stripe Checkout)? Verify/settle it before anything else.
  handlePaymentReturn()
  // Which stores allow collection — decides whether the fulfilment choice is offered at all.
  loadPickupStores()
  // Warm the public Square config so the card field (or the not-configured notice) resolves without a click.
  square.isConfigured().catch(() => {})
  // Same for Authorize.Net, so its card fields (or the not-configured notice) resolve without a click.
  authNet.isConfigured().catch(() => {})
  // Signed-in buyers get their profile + saved addresses (pick-or-add); guests restore any draft.
  if (isAuthed.value) loadAccountAddresses()
  else restoreContact()
  // Reward-points balance for signed-in buyers (WO-27) — no-op/hidden for guests or a disabled programme.
  loadPoints()
})
</script>

<style scoped lang="scss">
.storefront-container {
  max-width: 1200px;
  margin: 0 auto;
}

.sf-address-option {
  border: 1px solid rgba(0, 0, 0, 0.16);
  transition: border-color 0.15s ease, background 0.15s ease;
}

// Let a store's address wrap onto a few readable lines instead of one long strip.
.sf-store-tooltip {
  max-width: 260px;
}
.sf-address-option--active {
  border-color: var(--q-primary);
  background: rgba(0, 0, 0, 0.02);
}
body.body--dark .sf-address-option {
  border-color: rgba(255, 255, 255, 0.22);
}
body.body--dark .sf-address-option--active {
  background: rgba(255, 255, 255, 0.05);
}

.sf-payment-option {
  border: 1px solid rgba(0, 0, 0, 0.16);
  transition: border-color 0.15s ease, background 0.15s ease, box-shadow 0.15s ease;
}
.sf-payment-option:hover {
  border-color: var(--q-primary);
}
.sf-payment-option--active {
  border-color: var(--q-primary);
  background: rgba(0, 0, 0, 0.02);
  box-shadow: inset 0 0 0 1px var(--q-primary);
}
.sf-payment-icon {
  background: rgba(0, 0, 0, 0.05);
  color: var(--q-primary);
}
body.body--dark .sf-payment-option {
  border-color: rgba(255, 255, 255, 0.22);
}
body.body--dark .sf-payment-option--active {
  background: rgba(255, 255, 255, 0.05);
}
body.body--dark .sf-payment-icon {
  background: rgba(255, 255, 255, 0.08);
}

.sf-square-card {
  border: 1px solid rgba(0, 0, 0, 0.16);
  border-radius: 6px;
  padding: 8px 12px;
  min-height: 44px;
}
body.body--dark .sf-square-card {
  border-color: rgba(255, 255, 255, 0.22);
}

.confirm-card {
  max-width: 640px;
}
.sf-txn-id {
  font-family: 'Roboto Mono', monospace;
  word-break: break-all;
}
.sf-partner-chip {
  background: rgba(0, 0, 0, 0.05);
}
body.body--dark .sf-partner-chip {
  background: rgba(255, 255, 255, 0.08);
}

.summary-card {
  position: sticky;
  top: 88px;
}

// ---- Backorder (WO-86) ----
.sf-badge--backorder {
  background: var(--sf-badge-hot);
}
.sf-backorder-note {
  background: rgba(249, 115, 22, 0.08);
  border: 1px solid rgba(249, 115, 22, 0.28);
  border-radius: var(--sf-radius, 4px);
  padding: 8px 10px;
  color: #b45309;

  .q-icon {
    color: var(--sf-badge-hot);
  }
}
.sf-backorder-banner {
  background: rgba(249, 115, 22, 0.1);
  color: #b45309;
}
body.body--dark .sf-backorder-note,
body.body--dark .sf-backorder-banner {
  color: #fdba74;
}
</style>
