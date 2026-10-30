<template>
  <q-layout view="hHh lpR fFf" class="storefront-root">
    <q-header>
      <q-scroll-observer @scroll="onScroll" />

      <!-- Only the top info bar collapses on scroll-down; the main header + mega-menu stay sticky. -->
      <div class="sf-chrome" :class="{ 'sf-chrome--collapsed': chromeHidden }">
      <!-- ===== Top info bar ===== -->
      <div class="sf-topbar">
        <div class="sf-container row items-center justify-between q-py-xs">
          <div class="row items-center q-gutter-md">
            <a v-if="branding.supportPhone" :href="`tel:${branding.supportPhone}`" class="sf-topbar__item gt-xs">
              <q-icon name="o_call" size="16px" /> {{ branding.supportPhone }}
            </a>
            <a v-if="branding.supportEmail" :href="`mailto:${branding.supportEmail}`" class="sf-topbar__item gt-sm">
              <q-icon name="o_mail" size="16px" /> {{ branding.supportEmail }}
            </a>
            <span v-if="!branding.supportPhone && !branding.supportEmail" class="sf-topbar__item">
              Welcome to {{ branding.brandName }}
            </span>
          </div>

          <div class="row items-center q-gutter-sm no-wrap">
            <!-- <CurrencySelector /> -->
            <LanguageSelector class="gt-xs" />
            <router-link :to="{ name: 'shop-compare' }" class="sf-topbar__item gt-xs">
              Compare<span v-if="compareIds.length">&nbsp;({{ compareIds.length }})</span>
            </router-link>

            <span v-if="!customerAuth.isAuthenticated" class="row items-center no-wrap">
              <router-link :to="{ name: 'shop-login' }" class="sf-topbar__item">Sign in</router-link>
              <span class="q-mx-xs">/</span>
              <router-link :to="{ name: 'shop-register' }" class="sf-topbar__item">Register</router-link>
            </span>
            <a v-else class="sf-topbar__item cursor-pointer">
              <q-icon name="o_account_circle" size="16px" /> {{ accountLabel }}
              <q-menu>
                <q-list style="min-width: 180px">
                  <template v-if="isAdmin">
                    <q-item clickable v-close-popup :to="{ name: 'dashboard' }">
                      <q-item-section avatar><q-icon name="o_admin_panel_settings" color="primary" /></q-item-section>
                      <q-item-section class="text-primary text-weight-medium">Admin Portal</q-item-section>
                    </q-item>
                    <q-separator />
                  </template>
                  <q-item clickable v-close-popup :to="{ name: 'shop-account-profile' }">
                    <q-item-section avatar><q-icon name="o_person" /></q-item-section>
                    <q-item-section>My profile</q-item-section>
                  </q-item>
                  <q-item clickable v-close-popup :to="{ name: 'shop-account-addresses' }">
                    <q-item-section avatar><q-icon name="o_location_on" /></q-item-section>
                    <q-item-section>Addresses</q-item-section>
                  </q-item>
                  <q-item clickable v-close-popup :to="{ name: 'shop-account-orders' }">
                    <q-item-section avatar><q-icon name="o_receipt_long" /></q-item-section>
                    <q-item-section>Orders</q-item-section>
                  </q-item>
                  <q-separator />
                  <q-item clickable v-close-popup @click="onLogout">
                    <q-item-section avatar><q-icon name="o_logout" /></q-item-section>
                    <q-item-section>Sign out</q-item-section>
                  </q-item>
                </q-list>
              </q-menu>
            </a>
          </div>
        </div>
      </div>
      </div>

      <!-- ===== Main header (stays sticky with the mega-menu) ===== -->
      <div class="sf-header">
        <div class="sf-container row items-center no-wrap q-py-md q-gutter-xs">
          <router-link :to="{ name: 'shop-home' }" class="sf-header__logo" aria-label="Home">
            <img v-if="branding.logoUrl" :src="$media(branding.logoUrl)" :alt="branding.brandName" class="sf-header__logo-img">
            <template v-else>
              <q-icon name="o_storefront" size="26px" />
              <span class="gt-xs">{{ branding.brandName }}</span>
            </template>
          </router-link>

          <!-- Search with category prefix + submit -->
          <div class="sf-search col">
            <div class="sf-search__cat gt-sm">
              {{ searchCategoryLabel }}
              <q-icon name="o_expand_more" size="16px" class="q-ml-xs" />
              <q-menu v-model="catMenu" fit>
                <q-list style="min-width: 180px">
                  <q-item clickable v-close-popup @click="searchCategory = null">
                    <q-item-section>All categories</q-item-section>
                  </q-item>
                  <q-item
                    v-for="cat in topCategories"
                    :key="cat.id"
                    clickable
                    v-close-popup
                    @click="searchCategory = cat"
                  >
                    <q-item-section>{{ cat.name }}</q-item-section>
                  </q-item>
                </q-list>
              </q-menu>
            </div>
            <q-input
              v-model="searchText"
              dense
              borderless
              class="sf-search__input q-px-sm"
              placeholder="Search products…"
              debounce="300"
              @update:model-value="onQueryChange"
              @keyup.enter="onEnter"
            >
              <q-menu
                v-model="suggestOpen"
                fit
                no-focus
                no-refocus
                no-parent-event
                anchor="bottom left"
                self="top left"
              >
                <q-list style="min-width: 240px">
                  <template v-if="suggestions.products.length">
                    <q-item-label header class="q-py-xs">Products</q-item-label>
                    <q-item
                      v-for="(name, i) in suggestions.products"
                      :key="'p-' + i"
                      v-close-popup
                      clickable
                      dense
                      @click="selectSuggestion(name)"
                    >
                      <q-item-section avatar><q-icon name="o_inventory_2" size="18px" color="grey-6" /></q-item-section>
                      <q-item-section>{{ name }}</q-item-section>
                    </q-item>
                  </template>
                  <template v-if="suggestions.categories.length">
                    <q-item-label header class="q-py-xs">Categories</q-item-label>
                    <q-item
                      v-for="(name, i) in suggestions.categories"
                      :key="'c-' + i"
                      v-close-popup
                      clickable
                      dense
                      @click="selectSuggestion(name)"
                    >
                      <q-item-section avatar><q-icon name="o_category" size="18px" color="grey-6" /></q-item-section>
                      <q-item-section>{{ name }}</q-item-section>
                    </q-item>
                  </template>
                </q-list>
              </q-menu>
            </q-input>
            <button class="sf-search__btn" aria-label="Search" @click="onEnter">
              <q-icon name="o_search" size="20px" />
            </button>
          </div>

          <!-- Icon group -->
          <div class="row items-center no-wrap q-gutter-sm">
            <!-- Wishlist hidden
            <q-btn
              flat
              round
              dense
              class="sf-header__icon"
              icon="o_favorite_border"
              :to="{ name: 'shop-wishlist' }"
              aria-label="Wishlist"
            >
              <q-tooltip>Wishlist</q-tooltip>
            </q-btn>
            -->
            <q-btn
              flat
              round
              dense
              class="sf-header__icon"
              icon="o_shopping_cart"
              aria-label="Cart"
              @click="cartDrawer = true"
            >
              <q-badge v-if="cartCount" color="red" floating>{{ cartCount }}</q-badge>
              <q-tooltip>Cart</q-tooltip>
            </q-btn>
          </div>
        </div>
      </div>

      <!-- ===== Mega-menu (stays sticky at top on scroll) ===== -->
      <MegaMenu :quick-pages="topBarPages" />
    </q-header>

    <CartDrawer v-model="cartDrawer" />

    <q-page-container>
      <router-view />
    </q-page-container>

    <!-- ===== Footer ===== -->
    <div class="sf-footer">
      <div class="sf-container">
        <div class="row q-col-gutter-xl q-pb-lg">
          <!-- Brand + social -->
          <div class="col-12 col-sm-6 col-md-3">
            <div class="row items-center q-gutter-sm q-mb-md">
              <img v-if="branding.logoUrl" :src="$media(branding.logoUrl)" :alt="branding.brandName" style="max-height: 34px">
              <template v-else>
                <q-icon name="o_storefront" size="24px" color="primary" />
                <span class="sf-heading text-h6 text-weight-bold">{{ branding.brandName }}</span>
              </template>
            </div>
            <p class="text-body2">{{ branding.tagline }}</p>
            <div class="row q-gutter-sm q-mt-md">
              <a
                v-for="s in branding.social"
                :key="s.platform"
                :href="s.url"
                target="_blank"
                rel="noopener"
                class="sf-footer__social"
                :aria-label="s.platform"
              >
                <q-icon :name="s.icon" size="16px" />
              </a>
            </div>
          </div>

          <!-- Information — CMS Page Groups (WO-54). Falls back to the static links
               below when the navigation API is empty or unreachable. -->
          <template v-if="cmsNavGroups.length">
            <div v-for="group in cmsNavGroups" :key="group.groupSlug || group.groupName" class="col-6 col-md-3">
              <div class="sf-footer__title">{{ group.groupName }}</div>
              <router-link
                v-for="pg in group.pages"
                :key="pg.slug"
                class="sf-footer__link"
                :to="{ name: 'shop-cms-page', params: { slug: pg.slug } }"
              >{{ pg.title }}</router-link>
            </div>
          </template>
          <div v-else class="col-6 col-md-3">
            <div class="sf-footer__title">Information</div>
            <a v-for="link in informationLinks" :key="link.label" class="sf-footer__link" :href="link.href">{{ link.label }}</a>
          </div>

          <!-- Quick links -->
          <div class="col-6 col-md-3">
            <div class="sf-footer__title">Quick Links</div>
            <router-link class="sf-footer__link" :to="{ name: 'shop-account-profile' }">My Account</router-link>
            <!-- <router-link class="sf-footer__link" :to="{ name: 'shop-wishlist' }">Wishlist</router-link> -->
            <router-link class="sf-footer__link" :to="{ name: 'shop-cart' }">Cart</router-link>
            <router-link class="sf-footer__link" :to="{ name: 'shop-account-orders' }">Track Order</router-link>
            <router-link class="sf-footer__link" :to="{ name: 'shop-compare' }">Compare</router-link>
          </div>

          <!-- Contact + newsletter -->
          <div class="col-12 col-md-3">
            <div class="sf-footer__title">Stay in touch</div>
            <div v-if="branding.supportPhone" class="q-mb-xs"><q-icon name="o_call" size="16px" class="q-mr-xs" />{{ branding.supportPhone }}</div>
            <div v-if="branding.supportEmail" class="q-mb-md"><q-icon name="o_mail" size="16px" class="q-mr-xs" />{{ branding.supportEmail }}</div>
            <NewsletterForm compact />
          </div>
        </div>

        <!-- Bottom bar -->
        <div class="sf-footer__bottom row items-center justify-between">
          <div>&copy; {{ year }} {{ branding.brandName }}. All rights reserved.</div>
          <div class="row items-center q-gutter-xs" style="display: none !important;">
            <span class="sf-footer__pay">VISA</span>
            <span class="sf-footer__pay">MC</span>
            <span class="sf-footer__pay">AMEX</span>
            <span class="sf-footer__pay">PayPal</span>
            <span class="sf-footer__pay">Stripe</span>
          </div>
        </div>
      </div>
    </div>
  </q-layout>
</template>

<script setup>
/*
 * Porto-inspired public storefront shell (WO-109). Top info bar (contact +
 * currency/language/account), sticky navy header (logo, search with category
 * prefix, wishlist + cart-drawer icons), a category mega-menu, and a 4-column
 * footer. Branding (logo/colours/phone/email/social/tagline) is loaded from the
 * public tenant-branding endpoint and applied onto the Porto `--sf-*` tokens.
 *
 * PRESERVES the WO-21 isolated customer session: the account control uses
 * useCustomerAuthStore (sign-in/register when anonymous; profile/addresses/
 * orders/sign-out when authenticated), never the admin auth store.
 */
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { storefrontApi } from 'modules/storefront/api'
import { cmsApi } from 'modules/storefront/cms-api'
import { useCompare } from 'modules/storefront/composables/useStorefrontStorage'
import { useAuthStore } from 'stores/auth'
import { useCustomerAuthStore } from 'stores/customerAuth'
import { useStorefront } from 'modules/storefront/composables/useStorefront'
import { accountApi } from 'modules/storefront/account-api'
import { setActiveTimeZone } from 'src/utils/datetime'
import { useCategories } from 'modules/storefront/composables/useCategories'
import { useCart } from 'modules/storefront/composables/useCart'
import LanguageSelector from 'modules/storefront/components/LanguageSelector.vue'
import CurrencySelector from 'modules/storefront/components/CurrencySelector.vue'
import MegaMenu from 'modules/storefront/components/MegaMenu.vue'
import CartDrawer from 'modules/storefront/components/CartDrawer.vue'
import NewsletterForm from 'modules/storefront/components/NewsletterForm.vue'

const router = useRouter()
const { compareIds } = useCompare()
const auth = useAuthStore()
const customerAuth = useCustomerAuthStore()
const { branding, loadBranding } = useStorefront()
const { categories, loadCategories } = useCategories()
const { itemCount, ensureLoaded } = useCart()

const year = new Date().getFullYear()
const accountLabel = computed(() => customerAuth.displayName || 'Account')
// WO-112 unified session: staff carry an admin role; customers carry only the Customer role.
// Show the Admin Portal shortcut only for signed-in staff.
const isAdmin = computed(() => customerAuth.isAuthenticated && auth.isStaff)
const cartCount = computed(() => itemCount.value)
const topCategories = computed(() => categories.value.slice(0, 12))

const cartDrawer = ref(false)
const catMenu = ref(false)

// Collapse the top info bar on scroll-down (reveal on scroll-up / near top); the main header
// and mega-menu remain sticky at the top.
const chromeHidden = ref(false)
function onScroll (info) {
  const top = info.position ? info.position.top : 0
  if (top < 90) chromeHidden.value = false
  else if (info.direction === 'down') chromeHidden.value = true
  else if (info.direction === 'up') chromeHidden.value = false
}

// Footer "Information" nav — CMS Page Groups (WO-54). Populated from the public
// navigation endpoint on mount; the static links below are the fallback shown
// when the endpoint returns nothing or is unreachable.
const cmsNavGroups = ref([])
async function loadFooterNav () {
  try {
    const groups = await cmsApi.navigation()
    cmsNavGroups.value = Array.isArray(groups)
      ? groups.filter((g) => g && Array.isArray(g.pages) && g.pages.length)
      : []
  } catch (e) {
    cmsNavGroups.value = [] // keep the static fallback below
  }
}
onMounted(() => {
  loadFooterNav()
  loadTopBarNav()
})

// Top-bar quick-access pages — CMS pages the admin flagged "show in top bar". Loaded alongside the
// footer nav and handed to MegaMenu, which renders them inline after the category links.
const topBarPages = ref([])
async function loadTopBarNav () {
  try {
    const pages = await cmsApi.headerNavigation()
    topBarPages.value = Array.isArray(pages) ? pages : []
  } catch (e) {
    topBarPages.value = []
  }
}

// Static fallback links, used until (or when) the CMS navigation endpoint has data.
const informationLinks = [
  { label: 'About Us', href: '#' },
  { label: 'Shipping & Returns', href: '#' },
  { label: 'Privacy Policy', href: '#' },
  { label: 'Terms & Conditions', href: '#' },
  { label: 'Contact Us', href: '#' }
]

// ---- Search ----
const searchText = ref('')
const searchCategory = ref(null)
const suggestions = ref({ products: [], categories: [] })
const suggestOpen = ref(false)
const searchCategoryLabel = computed(() => (searchCategory.value ? searchCategory.value.name : 'All categories'))

async function onQueryChange (val) {
  const q = (val || '').trim()
  if (q.length < 2) {
    suggestOpen.value = false
    suggestions.value = { products: [], categories: [] }
    return
  }
  try {
    const result = await storefrontApi.autocomplete(q)
    const products = Array.isArray(result?.products) ? result.products : []
    const categoriesList = Array.isArray(result?.categories) ? result.categories : []
    suggestions.value = { products, categories: categoriesList }
    suggestOpen.value = products.length > 0 || categoriesList.length > 0
  } catch (e) {
    suggestOpen.value = false
    suggestions.value = { products: [], categories: [] }
  }
}

function goSearch (q) {
  suggestOpen.value = false
  const term = (q || '').trim()
  if (!term) return
  const query = { q: term }
  if (searchCategory.value) query.categoryId = searchCategory.value.id
  router.push({ name: 'shop-search', query })
}

function onEnter (evt) {
  const value = evt && evt.target ? evt.target.value : searchText.value
  goSearch(value)
}

function selectSuggestion (name) {
  searchText.value = name
  goSearch(name)
}

function onLogout () {
  customerAuth.logout()
  router.push({ name: 'shop-home' })
}

onMounted(async () => {
  await loadBranding() // sets the tenant display timezone
  loadCategories()
  ensureLoaded()
  // A signed-in customer's own timezone preference overrides the tenant default for their views.
  if (customerAuth.isAuthenticated) {
    try {
      const profile = await accountApi.getProfile()
      if (profile && profile.preferredTimeZone) setActiveTimeZone(profile.preferredTimeZone)
    } catch (e) { /* non-fatal — keep the tenant default */ }
  }
})
</script>
