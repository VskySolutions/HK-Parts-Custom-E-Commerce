/*
 * Development environment configuration (QENV=dev, the default).
 *
 * Consumed by quasar.config.js at build time:
 *   - API_BASE_URL        -> exposed to the client as process.env.API_BASE_URL
 *   - BUILD_PUBLIC_PATH   -> quasar build.publicPath
 *   - PUBLISH_FOLDER      -> distDir "../publish/spa/<PUBLISH_FOLDER>"
 *   - IGNORE_PUBLIC_FOLDER-> when true the publicPath is forced back to "/"
 */
module.exports = {
  // Plain HTTP in dev — the SPA (http://localhost:9000) calls Kestrel's http endpoint, so there is no
  // mixed-content block and no certificate to trust. Overridable via the API_BASE_URL env var.
  // NOTE: the Authorize.Net Accept.js and Square Web Payments SDKs require an https:// page and will not
  // run under this setup; point both back at https to exercise those card flows.
  API_BASE_URL: process.env.API_BASE_URL || 'http://localhost:5144',
  BUILD_PUBLIC_PATH: '/',
  PUBLISH_FOLDER: 'dev',
  IGNORE_PUBLIC_FOLDER: true
}
