<template>
    <q-page class="sf-container contact-us-page">
        <q-form v-if="!isSubmitted" @submit.prevent="onSubmit">
            <div class="row bg-ed q-pa-lg">
                <div class="col-md-6 col-sm-12 col-xs-12" :class="$q.screen.width > 1023 ? 'q-pr-md' : ''">
                    <AppTextField v-model="form.firstName" label="First Name" required :rules="[(v) => !!v || 'First Name is Required']"  />
                </div>
                <div class="col-md-6 col-sm-12 col-xs-12 q-lr-md">
                    <AppTextField v-model="form.lastName" label="Last Name" required :rules="[(v) => !!v || 'Last Name is Required']" />
                </div>
                <div class="col-md-6 col-sm-12 col-xs-12" :class="$q.screen.width > 1023 ? 'q-pr-md' : ''">
                    <AppTextField v-model="form.email" type="email" label="Email" autocomplete="email" required :rules="[(v) => !!v || 'Email is Required']" />
                </div>
                <div class="col-md-6 col-sm-12 col-xs-12 q-lr-md">
                    <AppPhoneInput label="Phone Number" v-model="form.phoneNumber" @update:model-value="phoneNumber = $event"/>
                </div>
                <div class="col-md-12 col-sm-12 col-xs-12">
                    <AppTextField v-model="form.comments" label="Comments" type="textarea" autogrow />
                </div>
                <div class="col-12 text-right">
                    <q-btn label="Submit" color="primary" type="submit" />
                </div>
            </div>
        </q-form>
        <div v-else class="contact-success text-center q-pa-xl bg-ed">
          <q-icon name="o_check_circle" color="positive" size="64px" />

          <div class="text-h5 text-weight-bold q-mt-md">
            Thank You for Contacting Us!
          </div>

          <p class="text-body1 text-grey-7 q-mt-sm">
            Your enquiry has been submitted successfully.
            Our team will review your message and get back to you soon.
          </p>
      </div>
    </q-page>
</template>
<script setup>
import { ref } from 'vue'
import { useQuasar } from 'quasar'
import AppTextField from 'components/common/AppTextField.vue'
import AppPhoneInput from 'components/common/AppPhoneInput.vue'
import { contactUsApi, getApiErrorMessage } from 'services/api'

const $q = useQuasar();
const loading = ref(false);
const isSubmitted = ref(false);
const form = ref({
  firstName: '',
  lastName: '',
  email: '',
  phoneNumber: '',
  comments: ''
});

async function onSubmit () {
  loading.value = true
  try {
    const payload = {
      firstName: form.value.firstName.trim(),
      lastName: form.value.lastName.trim(),
      email: form.value.email.trim(),
      phoneNumber: form.value.phoneNumber?.trim() || null,
      comments: form.value.comments?.trim() || null
    }
    await contactUsApi.create(payload)
    isSubmitted.value = true
    $q.notify({ type: 'positive', message: 'Your enquiry has been submitted successfully.' })
    form.value = {
      firstName: '',
      lastName: '',
      email: '',
      phoneNumber: '',
      comments: ''
    }
  } catch (e) {
    $q.notify({
      type: 'negative',
      message: getApiErrorMessage(e)
    })
    console.log("Validation Error Message:", getApiErrorMessage(e))
  } finally {
    loading.value = false
  }
}
</script>
