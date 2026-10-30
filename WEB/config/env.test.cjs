/*
 * Test environment configuration (QENV=test). Used by the Vitest harness and
 * by `quasar build` smoke builds.
 */
module.exports = {
  API_BASE_URL: process.env.API_BASE_URL || 'https://ecommerceapi.vskyapplications.com',
  BUILD_PUBLIC_PATH: '/',
  PUBLISH_FOLDER: 'test',
  IGNORE_PUBLIC_FOLDER: true
}
