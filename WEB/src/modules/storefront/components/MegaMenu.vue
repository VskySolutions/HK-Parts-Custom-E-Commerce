<template>
  <nav class="sf-megamenu">
    <div class="sf-container row items-center no-wrap">
      <!-- Mobile: hamburger opens the category drawer -->
      <q-btn
        flat
        dense
        round
        icon="o_menu"
        class="lt-md"
        aria-label="Browse categories"
        @click="drawer = true"
      />
      <span class="lt-md text-weight-medium q-ml-sm">Shop by category</span>

      <!-- ===== Desktop ===== -->
      <div class="gt-sm row items-center no-wrap full-width">
        <!-- "All categories" pillar: a two-pane browser over the whole tree -->
        <button
          type="button"
          class="sf-mm-pillar"
          :class="{ 'sf-mm-pillar--open': pillarOpen }"
          aria-haspopup="true"
          :aria-expanded="pillarOpen"
        >
          <q-icon name="o_widgets" size="18px" />
          <span>All Categories</span>
          <q-icon
            name="o_expand_more"
            size="18px"
            class="sf-mm-caret"
            :class="{ 'sf-mm-caret--open': pillarOpen }"
          />

          <q-menu
            anchor="bottom left"
            self="top left"
            :offset="[0, 1]"
            class="sf-mm-panel sf-mm-panel--pillar"
            transition-show="jump-down"
            transition-hide="jump-up"
            @show="onPillarShow"
            @hide="pillarOpen = false"
          >
            <div v-if="categories.length" class="sf-mm-browser">
              <!-- left: every top-level category -->
              <div class="sf-mm-browser__aside">
                <button
                  v-for="cat in categories"
                  :key="cat.id"
                  type="button"
                  class="sf-mm-browser__row"
                  :class="{ 'sf-mm-browser__row--active': previewId === cat.id }"
                  @mouseenter="previewId = cat.id"
                  @focus="previewId = cat.id"
                  @click="hasChildren(cat) ? (previewId = cat.id) : goTo(cat)"
                >
                  <span class="sf-mm-browser__name">{{ cat.name }}</span>
                  <span class="sf-mm-browser__count">{{ totalCount(cat) }}</span>
                  <q-icon v-if="hasChildren(cat)" name="o_chevron_right" size="17px" />
                </button>
              </div>

              <!-- right: the previewed category -->
              <div class="sf-mm-browser__pane">
                <template v-if="preview">
                  <div class="sf-mm-panel__head">
                    <div>
                      <div class="sf-mm-panel__eyebrow">Browsing</div>
                      <div class="sf-mm-panel__title">{{ preview.name }}</div>
                    </div>
                    <router-link class="sf-mm-panel__all" :to="categoryTo(preview)" v-close-popup>
                      View all <q-icon name="o_arrow_forward" size="15px" />
                    </router-link>
                  </div>

                  <div v-if="hasChildren(preview)" class="sf-mm-cols">
                    <div v-for="col in columnsFor(preview)" :key="col.key" class="sf-mm-col">
                      <div v-for="g in col.items" :key="g.id" class="sf-mm-group">
                        <router-link class="sf-mm-group__title" :to="categoryTo(g)" v-close-popup>
                          {{ g.name }}
                        </router-link>
                        <router-link
                          v-for="s in visibleChildren(g)"
                          :key="s.id"
                          class="sf-mm-link"
                          :to="categoryTo(s)"
                          v-close-popup
                        >
                          <span class="sf-mm-link__label">{{ s.name }}</span>
                          <span class="sf-mm-link__count">{{ s.productCount }}</span>
                        </router-link>
                        <router-link
                          v-if="moreCount(g)"
                          class="sf-mm-more"
                          :to="categoryTo(g)"
                          v-close-popup
                        >
                          +{{ moreCount(g) }} more
                        </router-link>
                      </div>
                    </div>
                  </div>

                  <div v-else class="sf-mm-empty">
                    <q-icon name="o_inventory_2" size="30px" />
                    <span>{{ totalCount(preview) }} products in {{ preview.name }}</span>
                    <router-link class="sf-btn sf-btn--primary q-mt-md" :to="categoryTo(preview)" v-close-popup>
                      Shop {{ preview.name }}
                    </router-link>
                  </div>
                </template>
              </div>
            </div>
          </q-menu>
        </button>

        <!-- Top-level links -->
        <div class="row items-center no-wrap sf-megamenu__bar">
          <router-link
            class="sf-megamenu__link"
            :class="{ 'sf-megamenu__link--active': isHome }"
            :to="{ name: 'shop-home' }"
          >
            Home
          </router-link>

          <div v-for="cat in topLevel" :key="cat.id" class="sf-megamenu__item">
            <!-- Has children: the top-level entry is a toggle, not a link -->
            <button
              v-if="hasChildren(cat)"
              type="button"
              class="sf-megamenu__link sf-megamenu__toggle"
              :class="{ 'sf-megamenu__link--active': activeId === cat.id || openId === cat.id }"
              :aria-expanded="openId === cat.id"
              aria-haspopup="true"
            >
              {{ cat.name }}
              <q-icon
                name="o_expand_more"
                size="17px"
                class="q-ml-xs sf-mm-caret"
                :class="{ 'sf-mm-caret--open': openId === cat.id }"
              />

              <q-menu
                anchor="bottom left"
                self="top left"
                :offset="[0, 1]"
                class="sf-mm-panel"
                transition-show="jump-down"
                transition-hide="jump-up"
                @show="openId = cat.id"
                @hide="openId === cat.id && (openId = null)"
              >
                <div class="sf-mm-panel__body" :style="panelStyle(cat)">
                  <div class="sf-mm-cols">
                    <div v-for="col in columnsFor(cat)" :key="col.key" class="sf-mm-col">
                      <div v-for="g in col.items" :key="g.id" class="sf-mm-group">
                        <router-link class="sf-mm-group__title" :to="categoryTo(g)" v-close-popup>
                          {{ g.name }}
                        </router-link>
                        <router-link
                          v-for="s in visibleChildren(g)"
                          :key="s.id"
                          class="sf-mm-link"
                          :to="categoryTo(s)"
                          v-close-popup
                        >
                          <span class="sf-mm-link__label">{{ s.name }}</span>
                          <span class="sf-mm-link__count">{{ s.productCount }}</span>
                        </router-link>
                        <router-link
                          v-if="moreCount(g)"
                          class="sf-mm-more"
                          :to="categoryTo(g)"
                          v-close-popup
                        >
                          +{{ moreCount(g) }} more
                        </router-link>
                      </div>
                    </div>
                  </div>

                  <!-- Right rail -->
                  <aside class="sf-mm-rail">
                    <q-icon name="o_local_mall" class="sf-mm-rail__glyph" />
                    <div class="sf-mm-rail__eyebrow">Explore</div>
                    <div class="sf-mm-rail__title">{{ cat.name }}</div>
                    <div class="sf-mm-rail__meta">
                      {{ totalCount(cat) }} products &middot; {{ cat.children.length }} sub-categories
                    </div>
                    <router-link class="sf-mm-rail__cta" :to="categoryTo(cat)" v-close-popup>
                      Shop all <q-icon name="o_arrow_forward" size="15px" />
                    </router-link>
                  </aside>
                </div>
              </q-menu>
            </button>

            <!-- Leaf category: navigate straight away -->
            <router-link
              v-else
              class="sf-megamenu__link"
              :class="{ 'sf-megamenu__link--active': activeId === cat.id }"
              :to="categoryTo(cat)"
            >
              {{ cat.name }}
            </router-link>
          </div>
        </div>

        <!-- CMS quick-access pages (admin-flagged "show in top bar"), inline after the category links -->
        <router-link
          v-for="pg in inlinePages"
          :key="'qp-' + pg.slug"
          class="sf-megamenu__link"
          :class="{ 'sf-megamenu__link--active': isActivePage(pg) }"
          :to="pageTo(pg)"
        >
          {{ pg.title }}
        </router-link>

        <!-- Overflow when more pages are flagged than fit inline -->
        <div v-if="overflowPages.length" class="sf-megamenu__item">
          <button type="button" class="sf-megamenu__link sf-megamenu__toggle" :aria-expanded="moreOpen">
            More
            <q-icon
              name="o_expand_more"
              size="17px"
              class="q-ml-xs sf-mm-caret"
              :class="{ 'sf-mm-caret--open': moreOpen }"
            />
            <q-menu
              anchor="bottom left"
              self="top left"
              :offset="[0, 1]"
              transition-show="jump-down"
              transition-hide="jump-up"
              @show="moreOpen = true"
              @hide="moreOpen = false"
            >
              <q-list style="min-width: 200px">
                <q-item v-for="pg in overflowPages" :key="'qpo-' + pg.slug" clickable v-close-popup :to="pageTo(pg)">
                  <q-item-section>{{ pg.title }}</q-item-section>
                </q-item>
              </q-list>
            </q-menu>
          </button>
        </div>

        <!-- Right: support shortcut (the top bar collapses on scroll, this stays) -->
        <a
          v-if="branding.supportPhone"
          :href="supportTel"
          class="sf-megamenu__support gt-md"
        >
          <q-icon name="o_headset_mic" size="19px" />
          <span class="sf-megamenu__support-text">
            <small>Need help?</small>
            <strong>{{ branding.supportPhone }}</strong>
          </span>
        </a>
      </div>
    </div>

    <!-- ===== Mobile drawer: accordion category tree ===== -->
    <q-drawer v-model="drawer" side="left" overlay bordered :width="300" class="sf-mobile-nav">
      <div class="sf-mobile-nav__head row items-center justify-between q-pa-md">
        <div class="row items-center q-gutter-sm no-wrap">
          <q-icon name="o_widgets" size="20px" />
          <span class="text-weight-bold">All Categories</span>
        </div>
        <q-btn flat dense round icon="o_close" @click="drawer = false" />
      </div>
      <q-scroll-area style="height: calc(100% - 64px)">
        <q-list class="sf-mobile-nav__list">
          <q-item clickable :to="{ name: 'shop-home' }" @click="drawer = false">
            <q-item-section avatar><q-icon name="o_home" /></q-item-section>
            <q-item-section>Home</q-item-section>
          </q-item>
          <!-- CMS quick-access pages flagged "show in top bar" -->
          <template v-if="quickPages.length">
            <q-separator />
            <q-item-label header class="text-weight-bold text-grey-7">Quick Links</q-item-label>
            <q-item v-for="pg in quickPages" :key="'qp-' + pg.slug" clickable :to="pageTo(pg)" @click="drawer = false">
              <q-item-section avatar><q-icon name="o_description" /></q-item-section>
              <q-item-section>{{ pg.title }}</q-item-section>
            </q-item>
          </template>
          <q-separator />
          <template v-for="cat in categories" :key="cat.id">
            <q-expansion-item
              v-if="hasChildren(cat)"
              :label="cat.name"
              dense
              expand-separator
              header-class="sf-mobile-nav__parent"
            >
              <q-item clickable :to="categoryTo(cat)" class="q-pl-lg" @click="drawer = false">
                <q-item-section class="sf-mobile-nav__all">All {{ cat.name }}</q-item-section>
              </q-item>
              <q-item
                v-for="child in cat.children"
                :key="child.id"
                clickable
                :to="categoryTo(child)"
                class="q-pl-lg"
                @click="drawer = false"
              >
                <q-item-section>{{ child.name }}</q-item-section>
                <q-item-section side>
                  <span class="sf-mobile-nav__count">{{ child.productCount }}</span>
                </q-item-section>
              </q-item>
            </q-expansion-item>
            <q-item v-else clickable :to="categoryTo(cat)" @click="drawer = false">
              <q-item-section>{{ cat.name }}</q-item-section>
              <q-item-section side>
                <span class="sf-mobile-nav__count">{{ cat.productCount }}</span>
              </q-item-section>
            </q-item>
          </template>
        </q-list>
      </q-scroll-area>
    </q-drawer>
  </nav>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useCategories } from 'modules/storefront/composables/useCategories'
import { useStorefront } from 'modules/storefront/composables/useStorefront'

const route = useRoute()
const router = useRouter()
const { categories, loadCategories } = useCategories()
const { branding } = useStorefront()

// CMS pages the admin flagged "show in top bar" ([{ title, slug }]), loaded by storefront_layout.
const props = defineProps({
  quickPages: { type: Array, default: () => [] }
})

const drawer = ref(false)
// Id of the top-level category whose bar dropdown is open.
const openId = ref(null)
// "All categories" pillar state + the row currently previewed in its right pane.
const pillarOpen = ref(false)
const previewId = ref(null)
// Overflow menu for quick-access pages beyond what fits inline in the bar.
const moreOpen = ref(false)

const MAX_SUBLINKS = 5
const MAX_COLS = 4
// Quick pages shown inline before collapsing the rest under "More" (the bar clips overflow).
const MAX_INLINE_PAGES = 4

// Show a reasonable number of top-level links in the bar; the pillar/drawer hold the full tree.
const topLevel = computed(() => categories.value.slice(0, 7))
const isHome = computed(() => route.name === 'shop-home')
const activeId = computed(() => (route.name === 'shop-category' ? route.params.idOrSlug : null))
const preview = computed(() => categories.value.find(c => c.id === previewId.value) || null)
const supportTel = computed(() => 'tel:' + (branding.value.supportPhone || ''))

// Quick-access CMS pages: the first few render inline, the rest collapse under "More".
const inlinePages = computed(() => (props.quickPages || []).slice(0, MAX_INLINE_PAGES))
const overflowPages = computed(() => (props.quickPages || []).slice(MAX_INLINE_PAGES))
const activePageSlug = computed(() => (route.name === 'shop-cms-page' ? route.params.slug : null))

function pageTo (pg) {
  return { name: 'shop-cms-page', params: { slug: pg.slug } }
}

function isActivePage (pg) {
  return !!pg && pg.slug === activePageSlug.value
}

function hasChildren (cat) {
  return !!(cat && cat.children && cat.children.length)
}

function categoryTo (cat) {
  return { name: 'shop-category', params: { idOrSlug: cat.slug || cat.id } }
}

function goTo (cat) {
  router.push(categoryTo(cat))
}

function visibleChildren (cat) {
  return (cat.children || []).slice(0, MAX_SUBLINKS)
}

function moreCount (cat) {
  return Math.max(0, (cat.children || []).length - MAX_SUBLINKS)
}

// Product total for a branch: own count plus every descendant's.
function totalCount (cat) {
  if (!cat) return 0
  return (cat.productCount || 0) + (cat.children || []).reduce((sum, c) => sum + totalCount(c), 0)
}

// Balance the child groups across up to 4 columns, weighted by how tall each group renders.
function columnsFor (cat) {
  const groups = cat.children || []
  const weight = g => 1 + Math.min((g.children || []).length, MAX_SUBLINKS)
  const total = groups.reduce((s, g) => s + weight(g), 0)
  const cols = Math.min(MAX_COLS, Math.max(1, Math.ceil(groups.length / 2)))
  const target = total / cols
  const out = []
  let bucket = []
  let used = 0
  for (const g of groups) {
    bucket.push(g)
    used += weight(g)
    if (used >= target && out.length < cols - 1) {
      out.push({ key: out.length, items: bucket })
      bucket = []
      used = 0
    }
  }
  if (bucket.length) out.push({ key: out.length, items: bucket })
  return out
}

// Keep the panel proportional to how many columns it actually renders.
function panelStyle (cat) {
  const cols = columnsFor(cat).length
  return { '--sf-mm-cols': cols, minWidth: (cols * 210 + 250) + 'px' }
}

function onPillarShow () {
  pillarOpen.value = true
  if (!previewId.value || !categories.value.some(c => c.id === previewId.value)) {
    previewId.value = categories.value.length ? categories.value[0].id : null
  }
}

onMounted(loadCategories)
</script>
