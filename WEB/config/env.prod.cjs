/*
 * Production environment configuration (QENV=prod).
 *
 * API_BASE_URL is same-origin ('') by default so the SPA talks to whatever
 * host it is served from; override with the API_BASE_URL env var when the API
 * lives on a different origin.
 */
module.exports = {
  API_BASE_URL: process.env.API_BASE_URL || '',
  BUILD_PUBLIC_PATH: '/',
  PUBLISH_FOLDER: 'prod',
  IGNORE_PUBLIC_FOLDER: false
}
