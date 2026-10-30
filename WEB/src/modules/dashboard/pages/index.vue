<template>
  <q-page class="app-page">
    <AppListHeader
      title="Dashboard"
      :subtitle="`Welcome${auth.user?.fullName ? ', ' + auth.user.fullName : ''}.`"
    />

    <div class="row q-col-gutter-md">
      <div class="col-12 col-sm-6 col-md-3">
        <q-card flat bordered>
          <q-card-section>
            <div class="text-caption text-grey-7">Brand</div>
            <div class="text-h6 ellipsis">{{ tenant.brandName }}</div>
          </q-card-section>
        </q-card>
      </div>
      <div class="col-12 col-sm-6 col-md-3">
        <q-card flat bordered>
          <q-card-section>
            <div class="text-caption text-grey-7">Signed in as</div>
            <div class="text-h6 ellipsis">{{ auth.user?.email || '—' }}</div>
          </q-card-section>
        </q-card>
      </div>
      <div class="col-12 col-sm-6 col-md-3">
        <q-card flat bordered>
          <q-card-section>
            <div class="text-caption text-grey-7">Role</div>
            <div class="text-h6">{{ auth.role || '—' }}</div>
          </q-card-section>
        </q-card>
      </div>
      <div class="col-12 col-sm-6 col-md-3">
        <q-card flat bordered>
          <q-card-section>
            <div class="text-caption text-grey-7">Time zone</div>
            <div class="text-h6 ellipsis">{{ tenant.timeZone }}</div>
          </q-card-section>
        </q-card>
      </div>
    </div>

    <q-card flat bordered class="q-mt-md">
      <q-card-section class="row items-center">
        <div class="col">
          <div class="text-subtitle1">Widgets</div>
          <div class="text-grey-7">Explore the feature-module template.</div>
        </div>
        <q-btn unelevated color="primary" icon="o_widgets" label="Open widgets" no-caps to="/widgets" />
      </q-card-section>
    </q-card>
  </q-page>
</template>

<script setup>
import { useAuthStore } from 'stores/auth'
import { useTenantStore } from 'stores/tenant'

const auth = useAuthStore()
const tenant = useTenantStore()
</script>
