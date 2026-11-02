<template>
  <q-page class="app-page">
    <AppDetailHeader
      :title="isCreate ? 'New product' : (product?.name || 'Product')"
      :breadcrumbs="[
        { label: 'Home', icon: 'o_home', to: '/dashboard' },
        { label: 'Products', to: { name: 'catalog-products' } },
        { label: isCreate ? 'New product' : (product?.name || 'Product') }
      ]"
      :status="!isCreate && product ? (form.isPublished ? 'Published' : 'Draft') : ''"
      :status-color="form.isPublished ? 'positive' : 'grey'"
      show-back
      @back="router.push({ name: 'catalog-products' })"
    >
      <template #actions>
        <q-btn
          v-if="!isCreate && product"
          outline
          color="primary"
          no-caps
          icon="o_visibility"
          label="Preview in store"
          type="a"
          :href="storePreviewUrl"
          target="_blank"
          rel="noopener"
          class="q-mr-sm"
        >
          <q-tooltip>Open this product's storefront page in a new tab</q-tooltip>
        </q-btn>
        <q-chip
          v-if="saveStatus"
          :icon="saveStatus.icon"
          :color="saveStatus.chip"
          :text-color="saveStatus.text"
          square
          dense
          class="q-mr-sm text-caption"
        >
          <q-spinner v-if="saveStatus.spin" size="14px" class="q-mr-xs" />
          {{ saveStatus.label }}
        </q-chip>
      </template>
    </AppDetailHeader>

    <q-inner-loading :showing="loading" color="primary" />

    <q-banner v-if="!loading && !isCreate && !product" class="bg-grey-2 rounded-borders">
      Product not found.
    </q-banner>

    <template v-if="isCreate || product">
      <!-- Auto-save hint (edit only) -->
      <div v-if="!isCreate" class="row items-center text-caption text-grey-5 q-mb-sm q-px-xs">
        <q-icon name="o_cloud_sync" size="16px" class="q-mr-xs" />
        Changes are saved automatically as you edit — no need to press save.
      </div>

      <q-card flat bordered class="app-section">
        <q-tabs
          v-model="tab"
          align="left"
          active-color="primary"
          indicator-color="primary"
          class="text-grey-7 app-product-tabs"
          no-caps
          inline-label
        >
          <q-tab name="general" icon="o_info" label="General" />
          <q-tab name="pricing" icon="o_sell" label="Pricing" :disable="isCreate" />
          <q-tab name="inventory" icon="o_inventory_2" label="Inventory" :disable="isCreate" />
          <q-tab v-if="isVariantType" name="variants" :disable="isCreate">
            <div class="row items-center no-wrap">
              <q-icon name="o_style" class="q-mr-xs" />Variants
              <q-badge v-if="!isCreate && variants.length" color="grey-5" class="q-ml-xs">{{ variants.length }}</q-badge>
            </div>
          </q-tab>
          <q-tab v-if="isGroupedType" name="subproducts" :disable="isCreate">
            <div class="row items-center no-wrap">
              <q-icon name="o_widgets" class="q-mr-xs" />Sub products
              <q-badge v-if="!isCreate && groupedMemberIds.length" color="grey-5" class="q-ml-xs">{{ groupedMemberIds.length }}</q-badge>
            </div>
          </q-tab>
          <q-tab name="shipping" icon="o_local_shipping" label="Shipping" :disable="isCreate" />
          <q-tab name="organization" :disable="isCreate">
            <div class="row items-center no-wrap">
              <q-icon name="o_account_tree" class="q-mr-xs" />Organization
              <q-badge v-if="!isCreate && orgCount" color="grey-5" class="q-ml-xs">{{ orgCount }}</q-badge>
            </div>
          </q-tab>
          <q-tab name="related" :disable="isCreate">
            <div class="row items-center no-wrap">
              <q-icon name="o_link" class="q-mr-xs" />Related products
              <q-badge v-if="!isCreate && relatedIds.length" color="grey-5" class="q-ml-xs">{{ relatedIds.length }}</q-badge>
            </div>
          </q-tab>
          <q-tab name="media" :disable="isCreate">
            <div class="row items-center no-wrap">
              <q-icon name="o_photo_library" class="q-mr-xs" />Media
              <q-badge v-if="!isCreate && mediaCount" color="grey-5" class="q-ml-xs">{{ mediaCount }}</q-badge>
            </div>
          </q-tab>
          <q-tab name="seo" icon="o_search" label="SEO" :disable="isCreate" />
          <q-tab name="reviews" :disable="isCreate">
            <div class="row items-center no-wrap">
              <q-icon name="o_star_border" class="q-mr-xs" />Reviews
              <q-badge v-if="!isCreate && reviewCount" color="grey-5" class="q-ml-xs">{{ reviewCount }}</q-badge>
            </div>
          </q-tab>
          <q-tab name="faq" :disable="isCreate">
            <div class="row items-center no-wrap">
              <q-icon name="o_help_outline" class="q-mr-xs" />FAQ
              <q-badge v-if="!isCreate && questionCount" color="grey-5" class="q-ml-xs">{{ questionCount }}</q-badge>
            </div>
          </q-tab>
        </q-tabs>

        <q-separator />

        <q-tab-panels v-model="tab" animated keep-alive>
          <!-- ============ GENERAL ============ -->
          <q-tab-panel name="general" class="q-gutter-y-xs">
            <div class="row q-col-gutter-sm">
              <div class="col-12 col-md-4">
                <AppSelect v-model="form.productType" label="Type" required :options="productTypeOptions" :v="v$.productType" :disable="!canWrite" hint="Determines which extra fields apply" />
              </div>
              <div class="col-12 col-md-8">
                <AppTextField v-model="form.name" label="Name" required :v="v$.name" placeholder="e.g. Men's Red Polo Shirt" :disable="!canWrite" />
              </div>
            </div>

            <div class="row q-col-gutter-sm">
              <div class="col-12 col-md-6">
                <AppTextField v-model="form.slug" label="Slug" :v="v$.slug" placeholder="e.g. mens-red-polo-shirt" hint="Storefront URL — auto-filled from the name until you edit it" :disable="!canWrite" />
              </div>
              <div class="col-12 col-md-6">
                <AppTextField v-model="form.sku" label="SKU" :v="v$.sku" placeholder="e.g. MENS-RED-POLO-SHIRT" hint="Stock code — auto-filled from the name until you edit it" :disable="!canWrite" />
              </div>
            </div>

            <AppTextField
              v-model="form.shortDescription"
              label="Short description"
              type="textarea"
              autogrow
              placeholder="A brief one- or two-line summary shown on product cards and search results"
              :disable="!canWrite"
            />
            <AppRichText
              v-model="form.fullDescription"
              label="Full description"
              placeholder="Describe the product in detail — features, materials, sizing…"
              hint="Rich text shown on the product page"
              :disable="!canWrite"
            />

            <q-separator class="q-my-sm" />
            <div class="row q-col-gutter-sm items-start">
              <div class="col-12 col-md-4">
                <AppSelect v-model="form.taxCategoryId" label="Tax category" required :options="taxCategoryOptions" :v="v$.taxCategoryId" :disable="!canWrite" hint="Drives tax calculation at checkout" />
                <div v-if="!taxCategoryOptions.length" class="text-caption text-negative">
                  No tax categories exist yet. A default “Standard” category is normally seeded automatically.
                </div>
              </div>
              <div class="col-12 col-md-4">
                <AppSelect v-model="form.manufacturerId" label="Brand" :options="manufacturerOptions" clearable placeholder="None" :disable="!canWrite" />
              </div>
              <div class="col-12 col-md-4">
                <AppTextField v-model="form.weight" label="Weight (in pounds)" type="number" step="0.01" min="0" :v="v$.weight" hint="Used for shipping rates" :disable="!canWrite" />
              </div>
            </div>

            <div class="row q-col-gutter-sm items-start">
              <div class="col-12 col-md-6">
                <AppTextField v-model="form.gtin" label="GTIN" :v="v$.gtin" maxlength="50" placeholder="UPC / EAN / ISBN" hint="Barcode — also matched by the product search" :disable="!canWrite" />
              </div>
              <div class="col-12 col-md-6">
                <AppTextField v-model="form.taxCode" label="Tax code" :v="v$.taxCode" maxlength="50" placeholder="e.g. P0000000" hint="Tax provider product code (Avalara / TaxJar / Stripe Tax)" :disable="!canWrite" />
              </div>
            </div>

            <div class="row q-col-gutter-sm items-center">
              <div class="col-6 col-md-3">
                <AppTextField v-model="form.displayOrder" label="Display order" type="number" hint="Lower shows first" :disable="!canWrite" />
              </div>
              <div class="col-auto q-mt-md">
                <q-toggle v-model="form.isPublished" label="Published" color="primary" :disable="!canWrite" />
              </div>
              <div class="col-auto q-mt-md">
                <q-toggle v-model="form.isFeatured" label="Featured" color="primary" :disable="!canWrite" />
              </div>
              <div class="col-auto q-mt-md">
                <q-toggle
                  v-model="form.isInquiryOnly"
                  label="Quote only"
                  color="primary"
                  :disable="!canWrite"
                >
                  <q-tooltip>
                    Buyers request a quote instead of paying. The item can still be added to the cart
                    and priced, but it checks out as an inquiry — and cannot share a cart with
                    buyable items.
                  </q-tooltip>
                </q-toggle>
              </div>
              <div class="col-auto q-mt-md">
                <q-toggle v-model="form.isPurchasable" label="Can purchase" color="primary" :disable="!canWrite">
                  <q-tooltip>Off = the product is shown but the buy button is hidden</q-tooltip>
                </q-toggle>
              </div>
              <div class="col-auto q-mt-md">
                <q-toggle v-model="form.isFflRequired" label="FFL required" color="primary" :disable="!canWrite">
                  <q-tooltip>Firearm — must ship to a Federal Firearms Licensee</q-tooltip>
                </q-toggle>
              </div>
            </div>

            <!-- Store listing (edit only — a sub-resource with its own endpoint) -->
            <template v-if="!isCreate && stores.length > 1">
              <q-separator class="q-my-sm" />
              <AppFieldLabel label="Stores">
                <template #hint>Stores this product is listed in — leave empty to list it in every store</template>
              </AppFieldLabel>
              <q-select
                v-model="storeIds"
                dense outlined multiple use-chips emit-value map-options clearable
                :options="storeOptions"
                :disable="!canWrite"
                placeholder="All stores"
                @update:model-value="queueStores"
              />
            </template>

            <!-- Specification attributes (edit only): the PHP Yes/No + dropdown fields such as Allow
                 International Shipping, Restrict Non High Capacity Mags States, Shipping Package Size and
                 Condition. One pick per attribute; the same values drive the category Filters. -->
            <template v-if="!isCreate && specAttributes.length">
              <q-separator class="q-my-sm" />
              <AppFieldLabel label="Product specifications">
                <template #hint>Defined under Catalog → Attributes (specification attributes) — also used as category filters</template>
              </AppFieldLabel>
              <div class="row q-col-gutter-sm">
                <div v-for="a in specAttributes" :key="a.id" class="col-12 col-sm-6 col-md-4">
                  <AppSelect
                    :model-value="specPicks[a.id] || null"
                    :label="a.name"
                    :options="a.options.map((o) => ({ label: o.value, value: o.id }))"
                    clearable
                    placeholder="Not set"
                    :disable="!canWrite"
                    @update:model-value="(v) => onSpecPick(a.id, v)"
                  />
                </div>
              </div>
            </template>

            <!-- Merchant-defined custom fields (edit only) -->
            <template v-if="!isCreate && customFields.length">
              <q-separator class="q-my-sm" />
              <AppFieldLabel label="Custom product information">
                <template #hint>Fields defined under Catalog → Custom fields</template>
              </AppFieldLabel>
              <div class="row q-col-gutter-sm">
                <div v-for="f in customFields" :key="f.id" :class="f.fieldType === 'TextArea' ? 'col-12' : 'col-12 col-md-6'">
                  <AppTextField
                    v-model="customValues[f.id]"
                    :label="f.name"
                    :required="f.isRequired"
                    :type="f.fieldType === 'TextArea' ? 'textarea' : 'text'"
                    :autogrow="f.fieldType === 'TextArea'"
                    :error="f.isRequired && customFieldsTouched && !(customValues[f.id] || '').trim()"
                    :error-message="`${f.name} is required`"
                    :disable="!canWrite"
                    @update:model-value="queueCustomFields"
                  />
                </div>
              </div>
            </template>

            <div v-if="form.isInquiryOnly" class="row q-col-gutter-sm items-center">
              <div class="col-12 col-md-4">
                <AppTextField
                  v-model="form.inquiryButtonLabel"
                  label="Request button label"
                  maxlength="80"
                  hint="Leave blank to use the tenant default from Commerce Mode"
                  :disable="!canWrite"
                />
              </div>
            </div>

            <div v-if="form.isFeatured" class="row q-col-gutter-sm items-center">
              <div class="col-6 col-md-3">
                <AppTextField v-model="form.featuredDisplayOrder" label="Featured order" type="number" hint="Order among featured products" :disable="!canWrite" />
              </div>
            </div>
          </q-tab-panel>

          <!-- ============ PRICING ============ -->
          <q-tab-panel name="pricing" class="q-gutter-y-sm">
            <div class="row q-col-gutter-sm">
              <div class="col-6 col-md-4">
                <AppTextField v-model="form.price" label="Price" type="number" step="0.01" placeholder="0.00" hint="Base price in the store currency" :disable="!canWrite" />
              </div>
            </div>

            <div class="row q-col-gutter-sm">
              <div class="col-6 col-md-4">
                <AppTextField v-model="form.cost" label="Cost" type="number" step="0.01" min="0" placeholder="0.00" :v="v$.cost" hint="Internal — never shown to buyers" :disable="!canWrite" />
              </div>
              <div class="col-6 col-md-4">
                <AppTextField v-model="form.msrp" label="MSRP" type="number" step="0.01" min="0" placeholder="0.00" :v="v$.msrp" hint="Suggested retail / “compare at” price" :disable="!canWrite" />
              </div>
            </div>

            <q-separator class="q-my-sm" />
            <AppFieldLabel label="Sale">
              <template #hint>Scheduled sale price — applies while now is inside the window (leave a date blank for no limit)</template>
            </AppFieldLabel>
            <div class="row q-col-gutter-sm">
              <div class="col-12 col-md-4">
                <AppTextField v-model="form.salePrice" label="Sale price" type="number" step="0.01" min="0" placeholder="0.00" :v="v$.salePrice" :disable="!canWrite" />
              </div>
              <div class="col-6 col-md-4">
                <AppTextField v-model="form.saleStartDateUtc" label="Sale starts" type="datetime-local" clearable :disable="!canWrite || !hasSalePrice" />
              </div>
              <div class="col-6 col-md-4">
                <AppTextField v-model="form.saleEndDateUtc" label="Sale ends" type="datetime-local" clearable :v="v$.saleEndDateUtc" :disable="!canWrite || !hasSalePrice" />
              </div>
            </div>

            <!-- Downloadable settings -->
            <template v-if="form.productType === 'Downloadable'">
              <q-separator class="q-my-sm" />
              <AppFieldLabel label="Download settings" />
              <div class="row q-col-gutter-sm">
                <div class="col-6"><AppTextField v-model="form.downloadExpiryDays" label="Download expiry (days)" type="number" placeholder="e.g. 30" :disable="!canWrite" /></div>
                <div class="col-6"><AppTextField v-model="form.downloadLimit" label="Download limit" type="number" placeholder="e.g. 5" :disable="!canWrite" /></div>
              </div>
            </template>

            <!-- Gift card settings -->
            <template v-if="form.productType === 'GiftCard'">
              <q-separator class="q-my-sm" />
              <AppFieldLabel label="Gift card settings" />
              <div class="row q-col-gutter-sm">
                <div class="col-12 col-md-6"><AppSelect v-model="form.giftCardType" label="Gift card type" :options="giftCardTypeOptions" :disable="!canWrite" /></div>
                <div class="col-12 col-md-6"><AppTextField v-model="form.giftCardAmount" label="Gift card amount" type="number" step="0.01" placeholder="0.00" :disable="!canWrite" /></div>
              </div>
            </template>

            <q-separator class="q-my-sm" />
            <div class="row items-center justify-between q-mb-xs">
              <AppFieldLabel label="Tier prices">
                <template #hint>Quantity-break pricing — a lower price once the buyer's quantity reaches the threshold</template>
              </AppFieldLabel>
              <q-btn v-if="canWrite" flat dense no-caps color="primary" icon="o_add" label="Add tier" @click="addTier" />
            </div>
            <div v-if="!tiers.length" class="text-grey-6 text-caption q-mb-sm">No tier prices — every quantity uses the base price.</div>
            <div v-for="(tier, i) in tiers" :key="i" class="row q-col-gutter-sm items-center q-mb-xs no-wrap">
              <div class="col"><q-input v-model.number="tier.minQuantity" dense outlined type="number" label="Min quantity" :disable="!canWrite" @update:model-value="queueTiers" /></div>
              <div class="col"><q-input v-model.number="tier.price" dense outlined type="number" step="0.01" label="Price" :disable="!canWrite" @update:model-value="queueTiers" /></div>
              <div class="col-auto"><q-btn v-if="canWrite" flat round dense icon="o_delete" color="negative" @click="removeTier(i)" /></div>
            </div>
          </q-tab-panel>

          <!-- ============ INVENTORY ============ -->
          <!-- PHP parity: one "Qty in stock" + "Notification level" per product. Stock still lives in a
               store-level row (what checkout reserves against) — the single fields edit the primary
               store's row; the per-store breakdown is only needed when stock is held in several stores. -->
          <q-tab-panel name="inventory" class="q-gutter-y-sm">
            <div v-if="!stores.length" class="text-grey-6 text-caption">
              No stores exist yet — create a store first, then stock can be held against it.
            </div>

            <template v-else-if="!isVariantType">
              <div class="row q-col-gutter-sm items-start">
                <div class="col-6 col-md-4">
                  <AppTextField
                    :model-value="stockSplit ? stockRollup : primaryEntry.qty"
                    label="Qty in stock"
                    type="number"
                    min="0"
                    :readonly="stockSplit"
                    :hint="stockSplit ? 'Held in several stores — edit per store below' : 'On-hand quantity'"
                    :disable="!canWrite"
                    @update:model-value="(v) => onInvInput(null, primaryStoreId, 'qty', v)"
                  />
                </div>
                <div class="col-6 col-md-4">
                  <AppTextField
                    :model-value="primaryEntry.threshold"
                    label="Notification level"
                    type="number"
                    min="0"
                    placeholder="0"
                    :readonly="stockSplit"
                    :hint="stockSplit ? 'Set per store below' : 'Low-stock alert when the quantity drops to this level'"
                    :disable="!canWrite"
                    @update:model-value="(v) => onInvInput(null, primaryStoreId, 'threshold', v)"
                  />
                </div>
                <div class="col-auto q-pt-md">
                  <q-spinner v-if="primaryEntry.saving" size="18px" color="primary" class="q-mt-sm" />
                  <q-icon v-else-if="primaryEntry.saved" name="o_check_circle" color="positive" size="20px" class="q-mt-sm"><q-tooltip>Saved</q-tooltip></q-icon>
                </div>
              </div>

              <q-expansion-item
                v-if="stores.length > 1"
                v-model="stockByStoreOpen"
                dense
                dense-toggle
                icon="o_store"
                label="Stock by store"
                caption="Only needed when stock is physically held in more than one store"
                header-class="text-grey-8 q-px-none"
              >
                <q-markup-table flat bordered dense class="q-mt-xs inv-table">
                  <thead>
                    <tr>
                      <th class="text-left">Store</th>
                      <th class="text-right" style="width: 130px">Stock qty</th>
                      <th class="text-right" style="width: 150px">Low-stock alert</th>
                      <th style="width: 40px"></th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr v-for="s in stores" :key="s.id">
                      <td class="text-left">{{ s.name }}</td>
                      <td>
                        <q-input :model-value="invEntry(null, s.id).qty" dense borderless type="number" min="0" input-class="text-right" :disable="!canWrite" @update:model-value="(v) => onInvInput(null, s.id, 'qty', v)" />
                      </td>
                      <td>
                        <q-input :model-value="invEntry(null, s.id).threshold" dense borderless type="number" min="0" input-class="text-right" placeholder="0" :disable="!canWrite" @update:model-value="(v) => onInvInput(null, s.id, 'threshold', v)" />
                      </td>
                      <td class="text-center">
                        <q-spinner v-if="invEntry(null, s.id).saving" size="16px" color="primary" />
                        <q-icon v-else-if="invEntry(null, s.id).saved" name="o_check_circle" color="positive" size="18px"><q-tooltip>Saved</q-tooltip></q-icon>
                      </td>
                    </tr>
                  </tbody>
                </q-markup-table>
              </q-expansion-item>
            </template>

            <div v-else class="text-grey-6 text-caption">
              This product has variants — set stock per variant and store on the
              <a class="text-primary cursor-pointer" @click="tab = 'variants'">Variants</a> tab.
            </div>

            <q-separator class="q-my-sm" />
            <AppFieldLabel label="Ordering &amp; availability" />
            <div class="row q-col-gutter-sm">
              <div class="col-6 col-md-3">
                <AppTextField v-model="form.minOrderQuantity" label="Min qty allowed" type="number" min="1" placeholder="No minimum" :v="v$.minOrderQuantity" :disable="!canWrite" />
              </div>
              <div class="col-6 col-md-3">
                <AppTextField v-model="form.maxOrderQuantity" label="Max qty allowed" type="number" min="1" placeholder="No maximum" :v="v$.maxOrderQuantity" :disable="!canWrite" />
              </div>
              <div class="col-12 col-md-6">
                <AppSelect v-model="form.stockStatusOverride" label="Stock status" :options="stockStatusOverrideOptions" :disable="!canWrite" hint="Override what the storefront shows, regardless of stock" />
              </div>
            </div>

            <div class="row items-center q-gutter-md q-mt-none">
              <q-toggle v-model="form.allowBackorder" label="Allow backorder" color="primary" :disable="!canWrite" />
              <div v-if="form.allowBackorder" style="max-width: 220px">
                <AppFieldLabel label="Estimated restock date">
                  <template #hint>Shown to buyers on backordered items</template>
                </AppFieldLabel>
                <q-input v-model="form.estimatedRestockDate" dense outlined type="date" :disable="!canWrite" clearable />
              </div>
            </div>
            <AppTextField
              v-if="form.allowBackorder"
              v-model="form.backorderMessage"
              label="Backorder message"
              maxlength="500"
              placeholder="e.g. Ships in 2–3 weeks"
              hint="Shown on the product page while backordered"
              :v="v$.backorderMessage"
              :disable="!canWrite"
            />
          </q-tab-panel>

          <!-- ============ VARIANTS ============ -->
          <q-tab-panel v-if="isVariantType" name="variants" class="q-gutter-y-sm">
            <AppFieldLabel label="Attributes used for generation">
              <template #hint>Pick the attributes (with values) whose combinations become variants</template>
            </AppFieldLabel>
            <div class="row items-start q-col-gutter-sm">
              <div class="col">
                <q-select
                  v-model="variantAttributeIds"
                  dense outlined multiple use-chips emit-value map-options
                  :options="variantAttributeOptions"
                  :disable="!canWrite"
                  placeholder="Select attributes"
                />
              </div>
              <div class="col-auto">
                <q-btn v-if="canWrite" unelevated color="primary" icon="o_auto_awesome" label="Generate variants" no-caps :loading="generating" @click="generate" />
              </div>
            </div>

            <div v-if="variants.length" class="row items-center q-col-gutter-sm q-mt-xs">
              <div class="col-auto text-caption text-grey-7">Editing stock for store:</div>
              <div class="col-12 col-sm-4">
                <AppSelect v-model="invStoreId" :options="storeOptions" :disable="!canWrite || !stores.length" placeholder="Select a store" />
              </div>
              <div v-if="!stores.length" class="col-auto text-caption text-negative">Create a store to hold stock.</div>
            </div>

            <q-table
              v-if="variants.length"
              flat bordered dense
              class="q-mt-sm app-table-card"
              row-key="id"
              :rows="variants"
              :columns="variantColumns"
              :filter="variantSearch"
              v-model:pagination="variantPagination"
              :rows-per-page-options="[10, 25, 50, 0]"
              binary-state-sort
            >
              <template #top>
                <div class="app-section__title col ellipsis">Variants</div>
                <q-input
                  v-model="variantSearch"
                  dense outlined debounce="300"
                  placeholder="Search combination or SKU"
                  style="min-width: 240px"
                >
                  <template #prepend><q-icon name="o_search" /></template>
                  <template v-if="variantSearch" #append>
                    <q-icon name="o_close" class="cursor-pointer" @click="variantSearch = ''" />
                  </template>
                </q-input>
              </template>

              <template #body-cell-combination="p">
                <q-td :props="p" class="text-left">{{ variantLabel(p.row) }}</q-td>
              </template>
              <template #body-cell-sku="p">
                <q-td :props="p"><q-input v-model="p.row.sku" dense borderless :disable="!canWrite" @update:model-value="queueVariant(p.row)" /></q-td>
              </template>
              <template #body-cell-price="p">
                <q-td :props="p"><q-input v-model.number="p.row.price" dense borderless type="number" step="0.01" input-class="text-right" :disable="!canWrite" @update:model-value="queueVariant(p.row)" /></q-td>
              </template>
              <template #body-cell-gtin="p">
                <q-td :props="p"><q-input v-model="p.row.gtin" dense borderless maxlength="50" :disable="!canWrite" @update:model-value="queueVariant(p.row)" /></q-td>
              </template>
              <template #body-cell-cost="p">
                <q-td :props="p"><q-input v-model.number="p.row.cost" dense borderless type="number" step="0.01" min="0" input-class="text-right" :disable="!canWrite" @update:model-value="queueVariant(p.row)" /></q-td>
              </template>
              <template #body-cell-msrp="p">
                <q-td :props="p"><q-input v-model.number="p.row.msrp" dense borderless type="number" step="0.01" min="0" input-class="text-right" :disable="!canWrite" @update:model-value="queueVariant(p.row)" /></q-td>
              </template>
              <template #body-cell-weight="p">
                <q-td :props="p"><q-input v-model.number="p.row.weight" dense borderless type="number" step="0.01" min="0" input-class="text-right" placeholder="Inherit" :disable="!canWrite" @update:model-value="queueVariant(p.row)" /></q-td>
              </template>
              <template #body-cell-stock="p">
                <q-td :props="p">
                  <q-input
                    :model-value="invStoreId ? invEntry(p.row.id, invStoreId).qty : null"
                    dense borderless type="number" min="0" input-class="text-right"
                    :disable="!canWrite || !invStoreId"
                    @update:model-value="(v) => onInvInput(p.row.id, invStoreId, 'qty', v)"
                  >
                    <template #append>
                      <q-spinner v-if="invStoreId && invEntry(p.row.id, invStoreId).saving" size="14px" color="primary" />
                      <q-icon v-else-if="invStoreId && invEntry(p.row.id, invStoreId).saved" name="o_check_circle" color="positive" size="14px" />
                    </template>
                  </q-input>
                </q-td>
              </template>
              <template #body-cell-threshold="p">
                <q-td :props="p">
                  <q-input
                    :model-value="invStoreId ? invEntry(p.row.id, invStoreId).threshold : null"
                    dense borderless type="number" min="0" input-class="text-right" placeholder="0"
                    :disable="!canWrite || !invStoreId"
                    @update:model-value="(v) => onInvInput(p.row.id, invStoreId, 'threshold', v)"
                  />
                </q-td>
              </template>
              <template #body-cell-reserved="p">
                <q-td :props="p" class="text-right text-grey-8">{{ invStoreId ? invEntry(p.row.id, invStoreId).reserved : 0 }}</q-td>
              </template>
              <template #body-cell-alerted="p">
                <q-td :props="p" class="text-center">
                  <q-icon v-if="invStoreId && invEntry(p.row.id, invStoreId).alerted" name="o_warning" color="warning" size="18px"><q-tooltip>Low-stock alert active</q-tooltip></q-icon>
                  <span v-else class="text-grey-5">—</span>
                </q-td>
              </template>
              <template #body-cell-isEnabled="p">
                <q-td :props="p" class="text-center"><q-toggle v-model="p.row.isEnabled" color="primary" :disable="!canWrite" @update:model-value="queueVariant(p.row)" /></q-td>
              </template>
              <template #body-cell-act="p">
                <q-td :props="p" class="text-center">
                  <q-spinner v-if="p.row._saving" size="16px" color="primary" />
                  <q-icon v-else-if="p.row._saved" name="o_check_circle" color="positive" size="18px"><q-tooltip>Saved</q-tooltip></q-icon>
                  <q-btn v-if="canWrite" flat round dense icon="o_delete" color="negative" @click="removeVariant(p.row)" />
                </q-td>
              </template>

              <template #bottom-row>
                <q-tr class="variant-total-row">
                  <q-td colspan="7" class="text-right text-weight-medium">Totals @ store</q-td>
                  <q-td class="text-right text-weight-bold">{{ invStoreId ? variantStockTotal : '—' }}</q-td>
                  <q-td />
                  <q-td class="text-right text-weight-bold">{{ invStoreId ? variantReservedTotal : '—' }}</q-td>
                  <q-td colspan="3" />
                </q-tr>
              </template>
            </q-table>
            <div v-else class="text-grey-6 q-mt-sm">No variants yet. Assign attributes (with values) then Generate.</div>
          </q-tab-panel>

          <!-- ============ SHIPPING ============ -->
          <q-tab-panel name="shipping" class="q-gutter-y-sm">
            <AppFieldLabel label="Package">
              <template #hint>Dimensions in inches — used with the weight (General tab) for carrier rate quotes</template>
            </AppFieldLabel>
            <div class="row q-col-gutter-sm">
              <div class="col-6 col-md-4">
                <AppTextField v-model="form.length" label="Length (in)" type="number" step="0.01" min="0" :v="v$.length" :disable="!canWrite" />
              </div>
              <div class="col-6 col-md-4">
                <AppTextField v-model="form.width" label="Width (in)" type="number" step="0.01" min="0" :v="v$.width" :disable="!canWrite" />
              </div>
              <div class="col-6 col-md-4">
                <AppTextField v-model="form.height" label="Height (in)" type="number" step="0.01" min="0" :v="v$.height" :disable="!canWrite" />
              </div>
            </div>

            <q-separator class="q-my-sm" />
            <AppFieldLabel label="Freight (LTL)">
              <template #hint>Used by freight carriers such as FreightView</template>
            </AppFieldLabel>
            <div class="row q-col-gutter-sm items-center">
              <div class="col-6 col-md-3">
                <AppSelect v-model="form.freightClass" label="Freight class" :options="freightClassOptions" clearable placeholder="None" :disable="!canWrite" />
              </div>
              <div class="col-6 col-md-3">
                <AppSelect v-model="form.packageType" label="Package type" :options="packageTypeOptions" clearable placeholder="None" :disable="!canWrite" />
              </div>
              <div class="col-6 col-md-3">
                <AppTextField v-model="form.packagePieces" label="Number of pieces" type="number" min="1" :v="v$.packagePieces" :disable="!canWrite" />
              </div>
              <div class="col-6 col-md-3 q-mt-md">
                <q-toggle v-model="form.isHazardous" label="Hazardous" color="primary" :disable="!canWrite" />
              </div>
            </div>
          </q-tab-panel>

          <!-- ============ ORGANIZATION ============ -->
          <q-tab-panel name="organization" class="q-gutter-y-sm">
            <AppFieldLabel label="Categories">
              <template #hint>Where this product appears in the storefront category tree</template>
            </AppFieldLabel>
            <q-select
              v-model="categoryIds"
              dense outlined multiple use-chips emit-value map-options
              :options="categoryOptions"
              :disable="!canWrite"
              placeholder="Assign one or more categories"
              @update:model-value="queueCategories"
            />

            <q-separator class="q-my-md" />
            <AppFieldLabel label="Tags">
              <template #hint>Free-form keywords for search and merchandising — type and press Enter</template>
            </AppFieldLabel>
            <q-select
              v-model="tagNames"
              dense outlined multiple use-input use-chips hide-dropdown-icon
              new-value-mode="add-unique"
              :disable="!canWrite"
              placeholder="Add tags"
              @update:model-value="queueTags"
            />

            <q-separator class="q-my-md" />
            <AppFieldLabel label="Custom input fields">
              <template #hint>Custom-input attributes the shopper fills in on the product page (e.g. engraving text)</template>
            </AppFieldLabel>
            <q-select
              v-model="customInputAttributeIds"
              dense outlined multiple use-chips emit-value map-options
              :options="customInputAttributeOptions"
              :disable="!canWrite"
              placeholder="Assign custom input fields"
            />
            <div v-if="!customInputAttributeOptions.length" class="text-caption text-grey-6">
              No custom-input attributes exist yet — create one under Catalog → Attributes with the “Custom input” display type.
            </div>
          </q-tab-panel>

          <!-- ============ SUB PRODUCTS (Grouped) ============ -->
          <q-tab-panel v-if="isGroupedType" name="subproducts" class="q-gutter-y-sm">
            <ProductPickerPanel
              v-model="groupedMemberIds"
              :exclude-id="product?.id || null"
              :exclude-types="['Grouped']"
              selected-label="Sub products"
              selected-hint="Sold together on this product's page, in this order"
              empty-text="No sub products yet — search below and tick the products this group contains."
              :disable="!canWrite"
              @update:model-value="queueGroupedMembers"
            />
          </q-tab-panel>

          <!-- ============ RELATED PRODUCTS ============ -->
          <q-tab-panel name="related" class="q-gutter-y-sm">
            <ProductPickerPanel
              v-model="relatedIds"
              :exclude-id="product?.id || null"
              selected-label="Related products"
              selected-hint="Shown as “Related products” on this product's storefront page"
              empty-text="No related products yet — search by SKU or name below and tick products to relate them."
              :disable="!canWrite"
              @update:model-value="queueRelated"
            />
          </q-tab-panel>

          <!-- ============ MEDIA ============ -->
          <q-tab-panel name="media" class="q-gutter-y-sm">
            <!-- Images: centralized media library (two-step upload → ProductPicture) -->
            <AppFieldLabel label="Images">
              <template #hint>Uploaded to the media library; display order follows the upload sequence</template>
            </AppFieldLabel>
            <div v-if="imagePictures.length" class="row q-col-gutter-sm q-mb-sm">
              <div v-for="pic in imagePictures" :key="pic.id" class="col-auto">
                <div class="product-pic">
                  <img :src="$media(pic.url)" :alt="pic.altText || (product && product.name)" class="product-pic__img" @click="openPicture(pic)">
                  <q-btn v-if="canWrite" round dense size="xs" color="negative" icon="o_close" class="product-pic__remove" @click="removePicture(pic)" />
                </div>
              </div>
            </div>
            <div v-else class="text-grey-6 text-caption q-mb-sm">No images yet.</div>

            <q-file
              v-if="canWrite"
              v-model="imageFiles"
              multiple dense outlined
              accept="image/*"
              label="Upload images"
              hint="PNG, JPG, GIF, WEBP — added to the media library"
              :loading="uploadingImages"
              @update:model-value="onImageFiles"
            >
              <template #prepend><q-icon name="o_add_photo_alternate" /></template>
            </q-file>

            <!-- Videos: embed URLs (legacy gallery — embeds aren't file uploads) -->
            <q-separator class="q-my-md" />
            <AppFieldLabel label="Videos">
              <template #hint>Paste an embed URL (YouTube, Vimeo…) and press Enter</template>
            </AppFieldLabel>
            <q-list v-if="videoImages.length" bordered separator class="q-mb-sm rounded-borders">
              <q-item v-for="img in videoImages" :key="img.id">
                <q-item-section avatar><q-icon name="o_movie" color="grey-7" /></q-item-section>
                <q-item-section class="ellipsis">{{ img.url }}</q-item-section>
                <q-item-section side>
                  <q-btn v-if="canWrite" flat round dense icon="o_delete" color="negative" @click="removeImage(img)" />
                </q-item-section>
              </q-item>
            </q-list>
            <q-input
              v-if="canWrite"
              v-model="newVideoUrl"
              dense outlined
              placeholder="e.g. https://www.youtube.com/embed/…"
              :loading="addingVideo"
              @keyup.enter="addVideo"
            >
              <template #prepend><q-icon name="o_movie" /></template>
              <template #append>
                <q-btn flat dense round icon="o_add" :disable="!newVideoUrl" @click="addVideo"><q-tooltip>Add video</q-tooltip></q-btn>
              </template>
            </q-input>

            <!-- Attachments: public files (manuals, spec sheets…) listed on the storefront product page -->
            <q-separator class="q-my-md" />
            <AppFieldLabel label="Attachments">
              <template #hint>Files shoppers can download from the product page — manuals, spec sheets, warranty cards</template>
            </AppFieldLabel>
            <q-list v-if="attachments.length" bordered separator class="q-mb-sm rounded-borders">
              <q-item v-for="(att, i) in attachments" :key="att.mediaId">
                <q-item-section avatar><q-icon :name="attachmentIcon(att)" color="grey-7" /></q-item-section>
                <q-item-section>
                  <q-input
                    v-model="att.title"
                    dense borderless
                    :placeholder="att.fileName || 'Title'"
                    maxlength="300"
                    :disable="!canWrite"
                    @update:model-value="queueAttachments"
                  />
                  <q-item-label caption>
                    <a :href="$media(att.url)" target="_blank" rel="noopener" class="text-primary">{{ att.fileName }}</a>
                    <span v-if="att.fileSizeBytes"> · {{ formatBytes(att.fileSizeBytes) }}</span>
                  </q-item-label>
                </q-item-section>
                <q-item-section v-if="canWrite" side>
                  <div class="row no-wrap items-center">
                    <q-btn flat round dense icon="o_arrow_upward" :disable="i === 0" @click="moveAttachment(i, -1)"><q-tooltip>Move up</q-tooltip></q-btn>
                    <q-btn flat round dense icon="o_arrow_downward" :disable="i === attachments.length - 1" @click="moveAttachment(i, 1)"><q-tooltip>Move down</q-tooltip></q-btn>
                    <q-btn flat round dense icon="o_delete" color="negative" @click="removeAttachment(i)"><q-tooltip>Remove</q-tooltip></q-btn>
                  </div>
                </q-item-section>
              </q-item>
            </q-list>
            <div v-else class="text-grey-6 text-caption q-mb-sm">No attachments yet.</div>
            <q-file
              v-if="canWrite"
              v-model="attachmentFiles"
              multiple dense outlined
              label="Upload attachments"
              hint="PDF, DOC, XLS, images… — added to the media library"
              :loading="uploadingAttachments"
              @update:model-value="onAttachmentFiles"
            >
              <template #prepend><q-icon name="o_attach_file" /></template>
            </q-file>

            <MediaSeoDialog v-model="lightboxOpen" :media-id="selectedMediaId" :fallback-url="lightboxUrl" @saved="onPictureSaved" />
          </q-tab-panel>

          <!-- ============ SEO ============ -->
          <q-tab-panel name="seo">
            <AppSeoPanel
              entity-type="Product"
              :entity-id="product?.id"
              :slug="form.slug"
              :fallback-title="form.name"
              :fallback-description="form.shortDescription || form.fullDescription"
              fallback-label="product name"
              :disable="!canWrite"
              :run-save="runSave"
            />
          </q-tab-panel>

          <!-- ============ REVIEWS ============ -->
          <q-tab-panel name="reviews">
            <ProductReviewsPanel
              v-if="!isCreate && product"
              :product-id="pid"
              :reviews-enabled="reviewsEnabled"
              :can-write="canWrite"
              @count="reviewCount = $event"
              @update:reviews-enabled="onReviewsEnabledToggled"
            />
          </q-tab-panel>

          <!-- ============ FAQ (PRODUCT Q&A) ============ -->
          <q-tab-panel name="faq">
            <ProductQuestionsPanel
              v-if="!isCreate && product"
              :product-id="pid"
              :product-name="product?.name"
              :can-write="canWrite"
              @count="questionCount = $event"
            />
          </q-tab-panel>
        </q-tab-panels>

        <!-- Create action: the only save button — you can't auto-save a product that doesn't exist yet -->
        <template v-if="isCreate">
          <q-separator />
          <q-card-actions class="q-pa-md">
            <div class="text-caption text-grey-7">
              Create the product to unlock pricing, organization, media{{ isVariantType ? ' and variants' : '' }} — all auto-saved from then on.
            </div>
            <q-space />
            <q-btn v-if="canWrite" unelevated color="primary" no-caps icon="o_check" label="Create product" :loading="creating" @click="createProduct" />
          </q-card-actions>
        </template>
      </q-card>
    </template>

    <AppRecordMeta entity-type="product" :record-id="product?.id" />
  </q-page>
</template>

<script setup>
/*
 * Product create + manage page (WO-15/WO-123; auto-save redesign). One page, two modes:
 * - Create: only the General tab is enabled; the other tabs are locked until the product exists.
 *   A single "Create product" action persists it, then redirects to the manage view.
 * - Manage (edit): tabbed sections (General / Pricing & inventory / Organization / Media / Variants)
 *   with NO save buttons — every field auto-saves on change (debounced), surfaced by the live status
 *   chip in the header ("Saving…" / "All changes saved"). Add / generate / delete stay as explicit
 *   operations; the core product and each sub-resource persist via their own replace-semantics endpoints.
 */
import { ref, reactive, computed, onMounted, watch, nextTick } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { debounce } from 'quasar'
import { getApiErrorMessage } from 'services/api'
import {
  productApi, inventoryApi, mediaApi, categoryApi, productAttributeApi, manufacturerApi, taxCategoryApi, productCustomFieldApi,
  specificationAttributeApi,
  productTypeOptions, giftCardTypeOptions, stockStatusOverrideOptions, freightClassOptions, packageTypeOptions
} from 'modules/catalog/api'
import { storeApi } from 'modules/stores/api'
import { usePermissions } from 'composables/usePermissions'
import { useNotify } from 'composables/useNotify'
import { deleteConfirmation } from 'dialogs/delete_confirmation'
import useVuelidate from '@vuelidate/core'
import { required, maxLength, minValue, helpers } from 'validators'
import AppDetailHeader from 'components/common/AppDetailHeader.vue'
import AppTextField from 'components/common/AppTextField.vue'
import AppSelect from 'components/common/AppSelect.vue'
import AppRichText from 'components/common/AppRichText.vue'
import AppFieldLabel from 'components/common/AppFieldLabel.vue'
import ProductReviewsPanel from 'modules/catalog/components/ProductReviewsPanel.vue'
import ProductQuestionsPanel from 'modules/catalog/components/ProductQuestionsPanel.vue'
import ProductPickerPanel from 'modules/catalog/components/ProductPickerPanel.vue'
import { slugify } from 'src/utils/slug'

const route = useRoute()
const router = useRouter()
const notify = useNotify()
const { has } = usePermissions()
const canWrite = computed(() => has('Catalog.Write'))

const isCreate = computed(() => route.name === 'catalog-product-new')
const pid = computed(() => route.params.id)

const product = ref(null)
const loading = ref(false)
const tab = ref('general')

// Reviews / FAQ (product-scoped moderation) tab state. The counts feed the tab badges (emitted by each
// panel once it loads); reviewsEnabled is read off the product DTO and flipped via the panel's own endpoint.
const reviewCount = ref(0)
const questionCount = ref(0)
const reviewsEnabled = computed(() => !!product.value?.reviewsEnabled)
function onReviewsEnabledToggled (val) {
  if (product.value) product.value.reviewsEnabled = val
}

// ---- Core scalar form ---------------------------------------------------------
const EMPTY = {
  productType: 'Simple', name: '', slug: '', sku: '', shortDescription: '', fullDescription: '',
  price: null, stockQuantity: 0, allowBackorder: false, estimatedRestockDate: null,
  taxCategoryId: '', manufacturerId: null,
  isPublished: false, displayOrder: 0, isFeatured: false, featuredDisplayOrder: 0,
  isInquiryOnly: false, inquiryButtonLabel: '',
  downloadExpiryDays: null, downloadLimit: null,
  giftCardType: 'Fixed', giftCardAmount: null,
  gtin: '', taxCode: '', isPurchasable: true, isFflRequired: false,
  cost: null, msrp: null, salePrice: null, saleStartDateUtc: '', saleEndDateUtc: '',
  minOrderQuantity: null, maxOrderQuantity: null, stockStatusOverride: '', backorderMessage: '',
  weight: null, length: null, width: null, height: null,
  packagePieces: 1, packageType: null, freightClass: null, isHazardous: false
}
const CORE_KEYS = Object.keys(EMPTY)
const form = reactive({ ...EMPTY })

// Storefront product URL for the "Preview in store" header action (slug preferred, id fallback), opened
// in a new tab so editing is never interrupted.
const storePreviewUrl = computed(() =>
  router.resolve({
    name: 'shop-product',
    // By id, never the slug field: the field can hold an unsaved/edited slug the API doesn't know yet.
    params: { idOrSlug: product.value?.id || route.params.id },
    // Admin preview: drafts render too (see storefront product.vue / AdminProductsController.Preview).
    query: { preview: '1' }
  }).href
)

const rules = {
  name: { required, maxLength: maxLength(400) },
  productType: { required },
  taxCategoryId: { required },
  slug: { maxLength: maxLength(400) },
  sku: { maxLength: maxLength(400) },
  gtin: { maxLength: maxLength(50) },
  taxCode: { maxLength: maxLength(50) },
  backorderMessage: { maxLength: maxLength(500) },
  cost: { minValue: optionalMin(0) },
  msrp: { minValue: optionalMin(0) },
  salePrice: { minValue: optionalMin(0) },
  saleEndDateUtc: {
    afterStart: helpers.withMessage('Must be after the sale start',
      (v) => !v || !form.saleStartDateUtc || v >= form.saleStartDateUtc)
  },
  minOrderQuantity: { minValue: optionalMin(1) },
  maxOrderQuantity: {
    minValue: optionalMin(1),
    atLeastMin: helpers.withMessage('Must be at least the min qty',
      (v) => isBlank(v) || isBlank(form.minOrderQuantity) || Number(v) >= Number(form.minOrderQuantity))
  },
  weight: { minValue: optionalMin(0) },
  length: { minValue: optionalMin(0) },
  width: { minValue: optionalMin(0) },
  height: { minValue: optionalMin(0) },
  packagePieces: { required, minValue: minValue(1) }
}
const v$ = useVuelidate(rules, form)

function isBlank (v) { return v === '' || v === null || v === undefined }
// minValue that lets an empty optional field through.
function optionalMin (min) {
  return helpers.withMessage(`Must be at least ${min}`, (v) => isBlank(v) || Number(v) >= min)
}

const hasSalePrice = computed(() => !isBlank(form.salePrice))

const isVariantType = computed(() => form.productType === 'WithVariants')
const isGroupedType = computed(() => form.productType === 'Grouped')

// ---- Options ------------------------------------------------------------------
const manufacturerOptions = ref([])
const taxCategoryOptions = ref([])
const categoryOptions = ref([])
const attributeOptions = ref([])
const attributeValueMap = ref({})
const attributeTypes = ref({})      // attributeId -> displayType, so the two pickers below can split the list

// ---- Sub-resources ------------------------------------------------------------
const storeIds = ref([])            // [] = listed in every store
const customFields = ref([])        // active ProductCustomFieldDto[]
const customValues = reactive({})   // fieldId -> value
const customFieldsTouched = ref(false)
const categoryIds = ref([])
const tagNames = ref([])
const tiers = ref([])
const attributeIds = ref([])
const variants = ref([])
const pictures = ref([])            // Media-backed product images [{ id, mediaId, url, altText, displayOrder }]
const imageFiles = ref(null)        // q-file selection, processed then cleared
const uploadingImages = ref(false)
const lightboxOpen = ref(false)
const lightboxUrl = ref('')
const selectedMediaId = ref(null)
const newVideoUrl = ref('')
const addingVideo = ref(false)
const generating = ref(false)

// PHP-parity sub-resources: specification picks, related products, grouped "sub products", attachments.
const specAttributes = ref([])      // SpecificationAttributeDto[] (with options)
const specOptionIds = ref([])       // assigned option ids — the persisted set
const relatedIds = ref([])          // RelationshipType = Related, in display order
const groupedMemberIds = ref([])    // Grouped product members, in display order
const attachments = ref([])         // [{ mediaId, title, fileName, url, mimeType, fileSizeBytes }]
const attachmentFiles = ref(null)
const uploadingAttachments = ref(false)

// ---- Per-store inventory (the authoritative fulfillment stock; catalog StockQuantity is a display
// rollup only). Levels are edited per (variant?, store) and saved via the inventory upsert endpoint.
const stores = ref([])              // [{ id, name }]
const invLevels = ref([])           // InventoryLevelDto[] for this product (all variants + stores)
const invModel = reactive({})       // `${variantId||''}|${storeId}` -> { qty, threshold, saving, saved }
const invStoreId = ref(null)        // store selected on the Variants tab
const invSavers = {}
const EMPTY_ENTRY = Object.freeze({ qty: 0, threshold: 0, reserved: 0, alerted: false, saving: false, saved: false })

const storeOptions = computed(() => stores.value.map((s) => ({ label: s.name, value: s.id })))
const stockRollup = computed(() => invLevels.value.reduce((sum, l) => sum + (Number(l.stockQuantity) || 0), 0))

// The Inventory tab's single "Qty in stock" / "Notification level" edit the primary (first) store's
// product-level row. If stock is also held in another store the single field can't represent it, so it
// turns read-only (showing the total) and the per-store breakdown opens.
const stockByStoreOpen = ref(false)
const primaryStoreId = computed(() => stores.value[0]?.id || null)
const primaryEntry = computed(() => (primaryStoreId.value ? invEntry(null, primaryStoreId.value) : EMPTY_ENTRY))
const stockSplit = computed(() => invLevels.value.some((l) =>
  !l.productVariantId && l.storeId !== primaryStoreId.value && Number(l.stockQuantity) > 0))
watch(stockSplit, (v) => { if (v) stockByStoreOpen.value = true }, { immediate: true })

const invKey = (variantId, storeId) => `${variantId || ''}|${storeId}`
function invEntry (variantId, storeId) {
  return invModel[invKey(variantId, storeId)] || EMPTY_ENTRY
}

// Seed the editable model from the loaded levels for every (variant? × store) cell shown in the UI.
function rebuildInvModel () {
  for (const k of Object.keys(invModel)) delete invModel[k]
  const variantKeys = isVariantType.value ? variants.value.map((v) => v.id) : [null]
  for (const vId of variantKeys) {
    for (const s of stores.value) {
      const lvl = invLevels.value.find((l) => (l.productVariantId || null) === (vId || null) && l.storeId === s.id)
      invModel[invKey(vId, s.id)] = {
        qty: lvl ? lvl.stockQuantity : 0,
        threshold: lvl ? lvl.lowStockThreshold : 0,
        reserved: lvl ? lvl.reservedQuantity : 0,
        alerted: lvl ? lvl.lowStockAlerted : false,
        saving: false,
        saved: false
      }
    }
  }
}

function upsertLocalLevel (dto) {
  const i = invLevels.value.findIndex(
    (l) => (l.productVariantId || null) === (dto.productVariantId || null) && l.storeId === dto.storeId
  )
  if (i >= 0) invLevels.value.splice(i, 1, dto)
  else invLevels.value.push(dto)
}

function onInvInput (variantId, storeId, field, val) {
  if (!storeId) return
  const key = invKey(variantId, storeId)
  if (!invModel[key]) invModel[key] = { qty: 0, threshold: 0, reserved: 0, alerted: false, saving: false, saved: false }
  invModel[key][field] = val
  queueInv(variantId, storeId)
}

function queueInv (variantId, storeId) {
  const key = invKey(variantId, storeId)
  if (!invSavers[key]) {
    invSavers[key] = debounce(async () => {
      const entry = invModel[key]
      if (!entry) return
      entry.saving = true
      entry.saved = false
      try {
        const dto = await inventoryApi.upsertLevel({
          productId: pid.value,
          productVariantId: variantId || null,
          storeId,
          stockQuantity: Math.max(0, Number(entry.qty) || 0),
          lowStockThreshold: entry.threshold === '' || entry.threshold == null ? null : Math.max(0, Number(entry.threshold) || 0)
        })
        upsertLocalLevel(dto)
        // Reserved/alerted are server-owned; refresh them from the response.
        entry.reserved = dto.reservedQuantity
        entry.alerted = dto.lowStockAlerted
        savedOnce.value = true
        entry.saved = true
        setTimeout(() => { if (invModel[key]) invModel[key].saved = false }, 2000)
      } catch (err) {
        saveError.value = true
        notify.error(getApiErrorMessage(err))
      } finally {
        entry.saving = false
      }
    }, 700)
  }
  invSavers[key]()
}

async function loadInventoryData () {
  try {
    const [storeResult, levels] = await Promise.all([
      storeApi.list({ page: 1, pageSize: 200 }).catch(() => []),
      inventoryApi.getForProduct(pid.value).catch(() => [])
    ])
    const items = Array.isArray(storeResult) ? storeResult : storeResult?.items || []
    stores.value = items.map((s) => ({ id: s.id, name: s.name }))
    invLevels.value = Array.isArray(levels) ? levels : []
  } catch (err) {
    stores.value = []
    invLevels.value = []
  }
  if (!invStoreId.value && stores.value.length) invStoreId.value = stores.value[0].id
  rebuildInvModel()
}

// Pictures now hold both images and video embeds (unified Media-backed gallery). Split by media type.
const imagePictures = computed(() => pictures.value.filter((p) => p.mediaType === 'Image'))
const videoImages = computed(() => pictures.value.filter((p) => p.mediaType === 'Video'))
const mediaCount = computed(() => pictures.value.length + attachments.value.length)
const orgCount = computed(() => categoryIds.value.length + tagNames.value.length)

// ---- Auto-save status ---------------------------------------------------------
const saving = ref(0)
const saveError = ref(false)
const savedOnce = ref(false)
const coreBlocked = ref(false)
let hydrating = false
let lastCore = ''
// Track the last slug/SKU we auto-generated so we only keep tracking the name while the field is
// still that value (or empty) — a merchant-customized slug/SKU is left untouched.
let lastAutoSlug = ''
let lastAutoSku = ''

const saveStatus = computed(() => {
  if (isCreate.value) return null
  if (saving.value > 0) return { label: 'Saving…', icon: 'o_sync', chip: 'blue-1', text: 'primary', spin: true }
  if (coreBlocked.value) return { label: 'Fix errors to save', icon: 'o_error_outline', chip: 'red-1', text: 'negative' }
  if (saveError.value) return { label: 'Couldn’t save — retry', icon: 'o_cloud_off', chip: 'red-1', text: 'negative' }
  if (savedOnce.value) return { label: 'All changes saved', icon: 'o_cloud_done', chip: 'green-1', text: 'positive' }
  return { label: 'Auto-save on', icon: 'o_cloud_queue', chip: 'grey-3', text: 'grey-8' }
})

async function runSave (fn) {
  saving.value++
  saveError.value = false
  try {
    await fn()
    savedOnce.value = true
  } catch (err) {
    saveError.value = true
    notify.error(getApiErrorMessage(err))
  } finally {
    saving.value--
  }
}

// ---- Slug / SKU derivation ----------------------------------------------------
// "Men's Red Polo Shirt" -> slug "mens-red-polo-shirt" (shared slugify), SKU "MENS-RED-POLO-SHIRT".
function skuify (value) {
  return slugify(value).toUpperCase()
}

// ---- Payload + hydration ------------------------------------------------------
function toNumberOrNull (value) {
  if (value === '' || value === null || value === undefined) return null
  const n = Number(value)
  return Number.isFinite(n) ? n : null
}

// <input type=datetime-local> holds local "YYYY-MM-DDTHH:mm"; the API stores UTC.
function localToUtcIso (local) {
  if (!local) return null
  const d = new Date(local)
  return Number.isNaN(d.getTime()) ? null : d.toISOString()
}
function utcIsoToLocal (iso) {
  if (!iso) return ''
  // EF returns datetime2 without a zone designator — it is UTC.
  const d = new Date(/[zZ]|[+-]\d\d:\d\d$/.test(iso) ? iso : `${iso}Z`)
  if (Number.isNaN(d.getTime())) return ''
  const pad = (n) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`
}

function buildPayload () {
  const payload = {
    name: form.name,
    productType: form.productType,
    taxCategoryId: form.taxCategoryId,
    slug: form.slug || null,
    shortDescription: form.shortDescription || null,
    fullDescription: form.fullDescription || null,
    sku: form.sku || null,
    price: toNumberOrNull(form.price),
    stockQuantity: toNumberOrNull(form.stockQuantity) || 0,
    allowBackorder: form.allowBackorder,
    estimatedRestockDate: form.allowBackorder ? (form.estimatedRestockDate || null) : null,
    manufacturerId: form.manufacturerId || null,
    isPublished: form.isPublished,
    displayOrder: toNumberOrNull(form.displayOrder) || 0,
    isFeatured: form.isFeatured,
    featuredDisplayOrder: form.isFeatured ? (toNumberOrNull(form.featuredDisplayOrder) || 0) : 0,
    isInquiryOnly: form.isInquiryOnly,
    inquiryButtonLabel: form.isInquiryOnly ? (form.inquiryButtonLabel || null) : null,
    gtin: form.gtin || null,
    taxCode: form.taxCode || null,
    isPurchasable: form.isPurchasable,
    isFflRequired: form.isFflRequired,
    cost: toNumberOrNull(form.cost),
    msrp: toNumberOrNull(form.msrp),
    salePrice: toNumberOrNull(form.salePrice),
    saleStartDateUtc: hasSalePrice.value ? localToUtcIso(form.saleStartDateUtc) : null,
    saleEndDateUtc: hasSalePrice.value ? localToUtcIso(form.saleEndDateUtc) : null,
    minOrderQuantity: toNumberOrNull(form.minOrderQuantity),
    maxOrderQuantity: toNumberOrNull(form.maxOrderQuantity),
    stockStatusOverride: form.stockStatusOverride || null,
    backorderMessage: form.allowBackorder ? (form.backorderMessage || null) : null,
    weight: toNumberOrNull(form.weight),
    length: toNumberOrNull(form.length),
    width: toNumberOrNull(form.width),
    height: toNumberOrNull(form.height),
    packagePieces: toNumberOrNull(form.packagePieces) || 1,
    packageType: form.packageType || null,
    freightClass: form.freightClass || null,
    isHazardous: form.isHazardous
  }
  if (form.productType === 'Downloadable') {
    payload.downloadExpiryDays = toNumberOrNull(form.downloadExpiryDays)
    payload.downloadLimit = toNumberOrNull(form.downloadLimit)
  }
  if (form.productType === 'GiftCard') {
    payload.giftCardType = form.giftCardType
    payload.giftCardAmount = toNumberOrNull(form.giftCardAmount)
  }
  return payload
}

function hydrate (p) {
  hydrating = true
  // Reset the auto-trackers: a saved product's slug/SKU is treated as customized, so renaming an
  // existing product never rewrites its (URL-stable) slug — only an empty field auto-fills.
  lastAutoSlug = ''
  lastAutoSku = ''
  for (const k of CORE_KEYS) form[k] = p[k] ?? EMPTY[k]
  // Date-only for the <input type=date> (the API returns a full ISO timestamp).
  if (form.estimatedRestockDate) form.estimatedRestockDate = String(form.estimatedRestockDate).slice(0, 10)
  form.saleStartDateUtc = utcIsoToLocal(p.saleStartDateUtc)
  form.saleEndDateUtc = utcIsoToLocal(p.saleEndDateUtc)
  storeIds.value = [...(p.storeIds || [])]
  for (const k of Object.keys(customValues)) delete customValues[k]
  for (const cv of p.customFieldValues || []) customValues[cv.productCustomFieldId] = cv.value || ''
  customFieldsTouched.value = false
  categoryIds.value = [...(p.categoryIds || [])]
  tagNames.value = (p.tags || []).map((t) => t.name)
  tiers.value = (p.tierPrices || []).filter((t) => !t.productVariantId).map((t) => ({ minQuantity: t.minQuantity, price: t.price }))
  attributeIds.value = [...(p.attributeIds || [])]
  variants.value = (p.variants || []).map((v) => ({
    id: v.id, sku: v.sku, price: v.price, stockQuantity: v.stockQuantity,
    gtin: v.gtin, cost: v.cost, msrp: v.msrp, weight: v.weight,
    allowBackorder: v.allowBackorder, isEnabled: v.isEnabled, displayOrder: v.displayOrder,
    attributeValueIds: v.attributeValueIds || [], _saving: false, _saved: false
  }))
  hydrateParity(p)
  lastCore = JSON.stringify(buildPayload())
  nextTick(() => { hydrating = false })
}

// ---- Core auto-save -----------------------------------------------------------
const saveCore = debounce(async () => {
  if (isCreate.value) return
  const ok = await v$.value.$validate()
  if (!ok) { coreBlocked.value = true; return }
  coreBlocked.value = false
  const payload = buildPayload()
  const snapshot = JSON.stringify(payload)
  if (snapshot === lastCore) return
  await runSave(async () => {
    await productApi.update(pid.value, payload)
    lastCore = snapshot
  })
}, 800)

watch(form, () => { if (!hydrating && !isCreate.value) saveCore() }, { deep: true })

// Auto-generate the slug + SKU from the name while each field is still empty or unchanged from the
// value we last derived. Once the merchant types their own slug/SKU it stops tracking; clearing the
// field (then editing the name) resumes it. In edit mode this also triggers the core auto-save.
watch(() => form.name, (name) => {
  // Loading a saved product sets form.name too — don't derive then: auto-save is paused while
  // hydrating, so a derived slug/SKU would show in the form but never be saved.
  if (hydrating) return
  if (!form.slug || form.slug === lastAutoSlug) {
    lastAutoSlug = slugify(name)
    form.slug = lastAutoSlug
  }
  if (!form.sku || form.sku === lastAutoSku) {
    lastAutoSku = skuify(name)
    form.sku = lastAutoSku
  }
})

// If the type stops being variant-capable while that tab is open, fall back to General.
watch(isVariantType, (v) => { if (!v && tab.value === 'variants') tab.value = 'general' })

// ---- Sub-resource auto-save ---------------------------------------------------
const queueStores = debounce(() => {
  if (hydrating) return
  runSave(async () => { product.value = await productApi.setStores(pid.value, storeIds.value || []) })
}, 700)

// Custom fields save as one set; hold the save while a required field is blank (the server rejects it).
const queueCustomFields = debounce(() => {
  if (hydrating) return
  customFieldsTouched.value = true
  if (customFields.value.some((f) => f.isRequired && !(customValues[f.id] || '').trim())) return
  const values = customFields.value.map((f) => ({ productCustomFieldId: f.id, value: customValues[f.id] || null }))
  runSave(async () => { product.value = await productApi.setCustomFieldValues(pid.value, values) })
}, 800)

const queueCategories = debounce(() => {
  if (hydrating) return
  runSave(async () => { product.value = await productApi.setCategories(pid.value, categoryIds.value) })
}, 700)


// ---- PHP-parity sub-resources ---------------------------------------------------
function hydrateParity (p) {
  specOptionIds.value = [...(p.specificationOptionIds || [])]
  relatedIds.value = (p.relationships || [])
    .filter((r) => r.relationshipType === 'Related')
    .sort((a, b) => a.displayOrder - b.displayOrder)
    .map((r) => r.relatedProductId)
  groupedMemberIds.value = [...(p.groupedMemberIds || [])]
  attachments.value = (p.attachments || []).map(toAttachmentRow)
}

function resetParity () {
  specOptionIds.value = []
  relatedIds.value = []
  groupedMemberIds.value = []
  attachments.value = []
}

// Specification attributes: one pick per attribute, derived from the assigned option ids.
const specPicks = computed(() => {
  const picks = {}
  for (const a of specAttributes.value) {
    const hit = (a.options || []).find((o) => specOptionIds.value.includes(o.id))
    if (hit) picks[a.id] = hit.id
  }
  return picks
})

function onSpecPick (attributeId, optionId) {
  const attr = specAttributes.value.find((a) => a.id === attributeId)
  const ownIds = new Set((attr?.options || []).map((o) => o.id))
  const next = specOptionIds.value.filter((id) => !ownIds.has(id))
  if (optionId) next.push(optionId)
  specOptionIds.value = next
  queueSpecs()
}

const queueSpecs = debounce(() => {
  if (hydrating) return
  runSave(async () => { product.value = await productApi.setSpecificationValues(pid.value, specOptionIds.value) })
}, 500)

async function loadSpecAttributes () {
  try {
    const result = await specificationAttributeApi.list({ page: 1, pageSize: 200 })
    const items = Array.isArray(result) ? result : result?.items || []
    specAttributes.value = items.filter((a) => (a.options || []).length)
  } catch (err) { specAttributes.value = [] }
}

const queueRelated = debounce(() => {
  if (hydrating) return
  runSave(async () => { product.value = await productApi.setRelationships(pid.value, 'Related', relatedIds.value) })
}, 600)

const queueGroupedMembers = debounce(() => {
  if (hydrating) return
  runSave(async () => { product.value = await productApi.setGroupedMembers(pid.value, groupedMemberIds.value) })
}, 600)

// Attachments: Media-backed public files, saved as one ordered set.
function toAttachmentRow (a) {
  return {
    mediaId: a.mediaId,
    // The DTO falls back to the file name when no title was stored — show that as "no title".
    title: a.title && a.title !== a.fileName ? a.title : '',
    fileName: a.fileName,
    url: a.url,
    mimeType: a.mimeType,
    fileSizeBytes: a.fileSizeBytes
  }
}

const queueAttachments = debounce(() => saveAttachments(), 700)

function saveAttachments () {
  if (hydrating) return Promise.resolve()
  const payload = attachments.value.map((a) => ({ mediaId: a.mediaId, title: (a.title || '').trim() || null }))
  return runSave(async () => {
    const rows = await productApi.setAttachments(pid.value, payload)
    // Keep any title still being typed; refresh file facts from the server.
    const typed = new Map(attachments.value.map((a) => [a.mediaId, a.title]))
    attachments.value = (rows || []).map((r) => {
      const row = toAttachmentRow(r)
      return { ...row, title: typed.has(r.mediaId) ? typed.get(r.mediaId) : row.title }
    })
  })
}

async function onAttachmentFiles (selection) {
  const files = Array.isArray(selection) ? selection : (selection ? [selection] : [])
  if (!files.length) return
  uploadingAttachments.value = true
  try {
    for (const file of files) {
      const draft = await mediaApi.prepare(file)
      const committed = await mediaApi.commit({
        tempId: draft.tempId,
        seoFileName: draft.suggestedSeoFileName,
        altText: null,
        title: file.name,
        caption: null,
        description: null
      })
      if (!attachments.value.some((a) => a.mediaId === committed.mediaId)) {
        attachments.value.push({ mediaId: committed.mediaId, title: '', fileName: file.name, url: committed.publicUrl, mimeType: file.type, fileSizeBytes: file.size })
      }
    }
    await saveAttachments()
    notify.success(files.length > 1 ? `${files.length} attachments added` : 'Attachment added')
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    uploadingAttachments.value = false
    attachmentFiles.value = null
  }
}

function moveAttachment (i, delta) {
  const list = [...attachments.value]
  const j = i + delta
  if (j < 0 || j >= list.length) return
  const tmp = list[i]
  list[i] = list[j]
  list[j] = tmp
  attachments.value = list
  saveAttachments()
}

async function removeAttachment (i) {
  const att = attachments.value[i]
  const label = att.title || att.fileName
  if (!(await deleteConfirmation(`the attachment "${label}"`, { title: 'Remove', okLabel: 'Remove', message: 'Remove this attachment from the product? The file stays in the media library.' }))) return
  attachments.value = attachments.value.filter((_, k) => k !== i)
  saveAttachments()
}

function attachmentIcon (att) {
  const t = (att.mimeType || '').toLowerCase()
  if (t.includes('pdf')) return 'o_picture_as_pdf'
  if (t.startsWith('image/')) return 'o_image'
  if (t.includes('sheet') || t.includes('excel') || t.includes('csv')) return 'o_table_chart'
  if (t.includes('word') || t.includes('document') || t.startsWith('text/')) return 'o_description'
  return 'o_insert_drive_file'
}

function formatBytes (n) {
  if (!n) return ''
  if (n < 1024) return `${n} B`
  if (n < 1024 * 1024) return `${(n / 1024).toFixed(0)} KB`
  return `${(n / 1024 / 1024).toFixed(1)} MB`
}

// If the type stops being Grouped while that tab is open, fall back to General.
watch(isGroupedType, (v) => { if (!v && tab.value === 'subproducts') tab.value = 'general' })

const queueTags = debounce(() => {
  if (hydrating) return
  runSave(async () => { product.value = await productApi.setTags(pid.value, tagNames.value) })
}, 700)

const queueTiers = debounce(() => {
  if (hydrating) return
  const payload = tiers.value
    .filter((t) => t.minQuantity != null && t.minQuantity !== '' && t.price != null && t.price !== '')
    .map((t) => ({ minQuantity: Number(t.minQuantity) || 0, price: Number(t.price) || 0 }))
  runSave(async () => { product.value = await productApi.setTierPrices(pid.value, payload) })
}, 800)

const queueAttributes = debounce(() => {
  if (hydrating) return
  runSave(async () => { product.value = await productApi.setAttributes(pid.value, attributeIds.value) })
}, 700)

// `attributeIds` holds every attribute assigned to the product, but they serve two unrelated jobs and
// are picked in two places: value-bearing attributes generate variants (Variants tab), custom-input
// ones are fields the shopper fills in on the product page (Organization tab, any product type).
// Both views write back into the one list so a save from either keeps the other's assignments.
function isCustomInputAttr (id) { return attributeTypes.value[id] === 'CustomInput' }

const variantAttributeOptions = computed(() => attributeOptions.value.filter((o) => !isCustomInputAttr(o.value)))
const customInputAttributeOptions = computed(() => attributeOptions.value.filter((o) => isCustomInputAttr(o.value)))

const variantAttributeIds = computed({
  get: () => attributeIds.value.filter((id) => !isCustomInputAttr(id)),
  set: (ids) => {
    attributeIds.value = [...ids, ...attributeIds.value.filter(isCustomInputAttr)]
    queueAttributes()
  }
})

const customInputAttributeIds = computed({
  get: () => attributeIds.value.filter(isCustomInputAttr),
  set: (ids) => {
    attributeIds.value = [...attributeIds.value.filter((id) => !isCustomInputAttr(id)), ...ids]
    queueAttributes()
  }
})

function addTier () { tiers.value.push({ minQuantity: 1, price: 0 }) }
function removeTier (i) { tiers.value.splice(i, 1); queueTiers() }

// ---- Variants (per-row debounced save) ----------------------------------------
const variantSavers = {}
function queueVariant (variant) {
  if (!variantSavers[variant.id]) {
    variantSavers[variant.id] = debounce(async () => {
      variant._saving = true
      variant._saved = false
      try {
        await productApi.updateVariant(variant.id, {
          sku: variant.sku || null,
          price: variant.price === '' || variant.price === null ? null : Number(variant.price),
          gtin: variant.gtin || null,
          cost: toNumberOrNull(variant.cost),
          msrp: toNumberOrNull(variant.msrp),
          weight: toNumberOrNull(variant.weight),
          stockQuantity: Number(variant.stockQuantity) || 0,
          allowBackorder: variant.allowBackorder,
          isEnabled: variant.isEnabled,
          displayOrder: Number(variant.displayOrder) || 0
        })
        savedOnce.value = true
        variant._saved = true
        setTimeout(() => { variant._saved = false }, 2000)
      } catch (err) {
        saveError.value = true
        notify.error(getApiErrorMessage(err))
      } finally {
        variant._saving = false
      }
    }, 700)
  }
  variantSavers[variant.id]()
}

// ---- Media (images via the centralized library) -------------------------------
async function loadPictures () {
  try {
    pictures.value = await productApi.listPictures(pid.value)
  } catch (err) { pictures.value = [] }
}

function openPicture (pic) {
  selectedMediaId.value = pic.mediaId || null
  lightboxUrl.value = pic.url
  lightboxOpen.value = true
}
async function onPictureSaved () {
  // Refresh so the new alt text / SEO-renamed URL is reflected in the grid.
  try { pictures.value = await productApi.listPictures(pid.value) } catch (e) { /* keep current */ }
}

// Each image runs the two-step media flow (prepare → commit) then is assigned as a ProductPicture.
// Alt text defaults to the product name; the SEO file name comes from the prepare step's suggestion.
async function onImageFiles (selection) {
  const files = Array.isArray(selection) ? selection : (selection ? [selection] : [])
  if (!files.length) return
  uploadingImages.value = true
  try {
    let added = 0
    for (const file of files) {
      const draft = await mediaApi.prepare(file)
      const committed = await mediaApi.commit({
        tempId: draft.tempId,
        seoFileName: draft.suggestedSeoFileName,
        altText: form.name || null,
        title: null,
        caption: null,
        description: null
      })
      await productApi.assignPicture(pid.value, { mediaId: committed.mediaId })
      added++
    }
    notify.success(added > 1 ? `${added} images added` : 'Image added')
    savedOnce.value = true
    await loadPictures()
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    uploadingImages.value = false
    imageFiles.value = null
  }
}

async function removePicture (pic) {
  if (!(await deleteConfirmation('this image', { title: 'Remove', okLabel: 'Remove', message: 'Are you sure you want to remove this image?' }))) return
  try {
    await productApi.removePicture(pic.id)
    pictures.value = pictures.value.filter((p) => p.id !== pic.id)
    notify.success('Image removed')
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  }
}

async function addVideo () {
  const url = newVideoUrl.value.trim()
  if (!url) { notify.warning('Enter a video URL'); return }
  addingVideo.value = true
  try {
    await productApi.addVideo(pid.value, { url, altText: null })
    notify.success('Video added')
    newVideoUrl.value = ''
    await loadPictures()
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    addingVideo.value = false
  }
}

async function removeImage (img) {
  if (!(await deleteConfirmation('this media entry'))) return
  try {
    await productApi.removePicture(img.id)
    notify.success('Media deleted')
    await loadPictures()
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  }
}

// ---- Variant operations -------------------------------------------------------
function variantLabel (variant) {
  const parts = (variant.attributeValueIds || []).map((id) => attributeValueMap.value[id] || id)
  return parts.length ? parts.join(', ') : '(no attributes)'
}

// Variants datatable: client-side quick search + column sorting. Field accessors expose the computed
// combination label and the per-store stock so both are searchable and sortable.
const variantSearch = ref('')
const variantPagination = ref({ sortBy: 'combination', descending: false, page: 1, rowsPerPage: 10 })
const variantColumns = [
  { name: 'combination', label: 'Combination', align: 'left', field: (r) => variantLabel(r), sortable: true },
  { name: 'sku', label: 'SKU', align: 'left', field: (r) => r.sku || '', sortable: true },
  { name: 'price', label: 'Price', align: 'right', field: (r) => (r.price == null ? -1 : Number(r.price)), sortable: true },
  { name: 'gtin', label: 'GTIN', align: 'left', field: (r) => r.gtin || '', sortable: true },
  { name: 'cost', label: 'Cost', align: 'right', field: (r) => (r.cost == null ? -1 : Number(r.cost)), sortable: true },
  { name: 'msrp', label: 'MSRP', align: 'right', field: (r) => (r.msrp == null ? -1 : Number(r.msrp)), sortable: true },
  { name: 'weight', label: 'Weight (lb)', align: 'right', field: (r) => (r.weight == null ? -1 : Number(r.weight)), sortable: true },
  { name: 'stock', label: 'Stock @ store', align: 'right', field: (r) => (invStoreId.value ? Number(invEntry(r.id, invStoreId.value).qty) || 0 : 0), sortable: true },
  { name: 'threshold', label: 'Low-stock alert', align: 'right', field: (r) => (invStoreId.value ? Number(invEntry(r.id, invStoreId.value).threshold) || 0 : 0), sortable: true },
  { name: 'reserved', label: 'Reserved', align: 'right', field: (r) => (invStoreId.value ? Number(invEntry(r.id, invStoreId.value).reserved) || 0 : 0), sortable: true },
  { name: 'alerted', label: 'Low-stock', align: 'center', field: (r) => (invStoreId.value && invEntry(r.id, invStoreId.value).alerted ? 1 : 0), sortable: true },
  { name: 'isEnabled', label: 'Enabled', align: 'center', field: (r) => (r.isEnabled ? 1 : 0), sortable: true },
  { name: 'act', label: '', align: 'center', field: 'id', sortable: false }
]
// Totals across all variants at the selected store (staged values, update live).
const variantStockTotal = computed(() =>
  invStoreId.value
    ? variants.value.reduce((sum, v) => sum + (Number(invEntry(v.id, invStoreId.value).qty) || 0), 0)
    : 0
)
const variantReservedTotal = computed(() =>
  invStoreId.value
    ? variants.value.reduce((sum, v) => sum + (Number(invEntry(v.id, invStoreId.value).reserved) || 0), 0)
    : 0
)

async function generate () {
  generating.value = true
  try {
    product.value = await productApi.generateVariants(pid.value)
    hydrate(product.value)
    await loadInventoryData()
    notify.success('Variants generated')
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    generating.value = false
  }
}

async function removeVariant (variant) {
  // Summarise the inventory that will be deleted along with the variant so the user knows the impact.
  const levels = invLevels.value.filter((l) => (l.productVariantId || null) === variant.id)
  const storeCount = levels.length
  const totalStock = levels.reduce((sum, l) => sum + (Number(l.stockQuantity) || 0), 0)
  const reserved = levels.reduce((sum, l) => sum + (Number(l.reservedQuantity) || 0), 0)

  let message = `Delete the variant “${variantLabel(variant)}”?`
  if (storeCount > 0) {
    message += ` Its per-store inventory will also be permanently deleted — ${totalStock} unit(s) across ${storeCount} store(s)`
    message += reserved > 0 ? `, including ${reserved} reserved unit(s).` : '.'
  } else {
    message += ' It has no inventory recorded.'
  }
  message += ' This can’t be undone.'

  if (!(await deleteConfirmation('this variant', { title: 'Delete variant', okLabel: 'Delete', message }))) return
  try {
    await productApi.deleteVariant(variant.id)
    notify.success('Variant and its inventory deleted')
    await load()
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  }
}

// ---- Create -------------------------------------------------------------------
const creating = ref(false)
async function createProduct () {
  const ok = await v$.value.$validate()
  if (!ok) { notify.warning('Fill in the required fields'); return }
  creating.value = true
  try {
    const created = await productApi.create(buildPayload())
    notify.success('Product created')
    router.replace({ name: 'catalog-product-detail', params: { id: created.id } })
  } catch (err) {
    notify.error(getApiErrorMessage(err))
  } finally {
    creating.value = false
  }
}

// ---- Loading ------------------------------------------------------------------
function flattenTree (nodes, depth = 0, acc = []) {
  for (const node of nodes || []) {
    acc.push({ label: `${'— '.repeat(depth)}${node.name}`, value: node.id })
    if (node.children && node.children.length) flattenTree(node.children, depth + 1, acc)
  }
  return acc
}

async function loadCategories () {
  try {
    const tree = await categoryApi.tree()
    categoryOptions.value = flattenTree(Array.isArray(tree) ? tree : [])
  } catch (err) { categoryOptions.value = [] }
}

async function loadAttributes () {
  try {
    const result = await productAttributeApi.list({ page: 1, pageSize: 200 })
    const items = Array.isArray(result) ? result : result?.items || []
    attributeOptions.value = items.map((a) => ({ label: a.name, value: a.id }))
    const map = {}
    const types = {}
    for (const attr of items) {
      types[attr.id] = attr.displayType
      for (const val of attr.values || []) map[val.id] = `${attr.name}: ${val.value}`
    }
    attributeValueMap.value = map
    attributeTypes.value = types
  } catch (err) { attributeOptions.value = []; attributeTypes.value = {} }
}

async function loadManufacturers () {
  try {
    const result = await manufacturerApi.list({ page: 1, pageSize: 200 })
    const items = Array.isArray(result) ? result : result?.items || []
    manufacturerOptions.value = items.map((m) => ({ label: m.name, value: m.id }))
  } catch (err) { manufacturerOptions.value = [] }
}

async function loadCustomFields () {
  try {
    const items = await productCustomFieldApi.list({ isActive: true })
    customFields.value = Array.isArray(items) ? items : []
  } catch (err) { customFields.value = [] }
}

async function loadTaxCategories () {
  try {
    const result = await taxCategoryApi.list({ page: 1, pageSize: 200 })
    const items = Array.isArray(result) ? result : result?.items || []
    taxCategoryOptions.value = items.map((t) => ({ label: t.name, value: t.id }))
    // For a brand-new product, preselect the first tax category so the required field is satisfied.
    if (isCreate.value && !form.taxCategoryId && taxCategoryOptions.value.length) {
      hydrating = true
      form.taxCategoryId = taxCategoryOptions.value[0].value
      nextTick(() => { hydrating = false })
    }
  } catch (err) { taxCategoryOptions.value = [] }
}

async function load () {
  loading.value = true
  try {
    product.value = await productApi.get(pid.value)
    hydrate(product.value)
    loadPictures()
    loadInventoryData()
  } catch (err) {
    product.value = null
    notify.error(getApiErrorMessage(err))
  } finally {
    loading.value = false
  }
}

async function init () {
  // Reset transient state on (re)entry.
  saveError.value = false
  savedOnce.value = false
  coreBlocked.value = false
  reviewCount.value = 0
  questionCount.value = 0
  tab.value = 'general'
  if (isCreate.value) {
    hydrating = true
    Object.assign(form, EMPTY)
    categoryIds.value = []; tagNames.value = []; tiers.value = []; attributeIds.value = []; variants.value = []; pictures.value = []
    storeIds.value = []
    resetParity()
    for (const k of Object.keys(customValues)) delete customValues[k]
    product.value = null
    loading.value = false
    v$.value.$reset()
    await loadTaxCategories()
    nextTick(() => { hydrating = false })
    return
  }
  await load()
}

onMounted(() => {
  init()
  loadCategories()
  loadAttributes()
  loadManufacturers()
  loadCustomFields()
  loadSpecAttributes()
  if (!isCreate.value) loadTaxCategories()
})

// After create we router.replace to the detail route on the same component instance — reload on change.
watch(() => route.fullPath, () => { init(); if (!isCreate.value) loadTaxCategories() })
</script>

<style scoped lang="scss">
.app-product-tabs {
  :deep(.q-tab) { min-height: 44px; }
}

.variant-total-row td {
  background: rgba(0, 0, 0, 0.03);
  border-top: 1px solid rgba(0, 0, 0, 0.12);
}

// Media-backed picture thumbnails.
.product-pic {
  position: relative;
  width: 92px;
  &__img {
    width: 92px;
    height: 92px;
    object-fit: cover;
    border: 1px solid rgba(0, 0, 0, 0.12);
    border-radius: 6px;
    cursor: pointer;
    background: #fafafa;
  }
  &__remove {
    position: absolute;
    top: -8px;
    right: -8px;
  }
}
</style>
