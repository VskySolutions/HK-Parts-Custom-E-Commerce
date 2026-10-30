/*
 * Central LocalStorage key registry + typed accessors (WO-94 Step 4/6).
 *
 * The axios request interceptor (boot/axios.js) and the auth store
 * (stores/auth.js) both read/write the session here, so the key names live in
 * exactly one place. Uses Quasar's LocalStorage plugin (JSON aware).
 */
import { LocalStorage } from 'quasar'

export const STORAGE_KEYS = Object.freeze({
  TOKEN: 'token',
  REFRESH_TOKEN: 'refreshToken',
  EXPIRES_AT: 'expiresAtUtc',
  USER: 'user',
  PERMISSIONS: 'permissions',
  MUST_CHANGE_PASSWORD: 'mustChangePassword',
  LEFT_DRAWER_OPEN: 'leftDrawerOpen',
  FORM_DRAWER_WIDTH: 'formDrawerWidth',
  MENU_COLLAPSED: 'menuCollapsed',

  // Storefront customer session — a SEPARATE namespace from the admin session
  // above, so a shopper's login never collides with an admin's (isolated auth).
  CUSTOMER_TOKEN: 'customer.token',
  CUSTOMER_REFRESH_TOKEN: 'customer.refreshToken',
  CUSTOMER_EXPIRES_AT: 'customer.expiresAtUtc',
  CUSTOMER_USER: 'customer.user'
})

export function getItem (key, fallback = null) {
  try {
    const val = LocalStorage.getItem(key)
    return val === null || val === undefined ? fallback : val
  } catch (e) {
    return fallback
  }
}

export function setItem (key, value) {
  try {
    LocalStorage.set(key, value)
  } catch (e) {
    /* storage disabled / quota — ignore */
  }
}

export function removeItem (key) {
  try {
    LocalStorage.remove(key)
  } catch (e) {
    /* ignore */
  }
}

/** The persisted user object ({ id, email, fullName, role, ...site }). */
export function getStoredUser () {
  return getItem(STORAGE_KEYS.USER, null)
}

export function getStoredToken () {
  return getItem(STORAGE_KEYS.TOKEN, null)
}

/** The persisted storefront customer session token (isolated from the admin token). */
export function getCustomerToken () {
  return getItem(STORAGE_KEYS.CUSTOMER_TOKEN, null)
}

/** The persisted storefront customer object ({ id, email, fullName, ... }). */
export function getStoredCustomer () {
  return getItem(STORAGE_KEYS.CUSTOMER_USER, null)
}
