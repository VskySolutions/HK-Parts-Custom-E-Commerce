/*
 * Document title boot file (WO-94 Step 4).
 *
 * router.afterEach sets the browser tab title from the deepest matched
 * route's meta.title, using @vueuse/core's useTitle.
 */
import { useTitle } from '@vueuse/core'

const APP_NAME = 'HK Parts E-Commerce'

export default ({ router }) => {
  const pageTitle = useTitle(APP_NAME)

  router.afterEach((to) => {
    // meta is merged across matched records; prefer the deepest one that sets it.
    let title = to.meta && to.meta.title
    if (!title) {
      for (let i = to.matched.length - 1; i >= 0; i--) {
        if (to.matched[i].meta && to.matched[i].meta.title) {
          title = to.matched[i].meta.title
          break
        }
      }
    }
    pageTitle.value = title ? `${title} · ${APP_NAME}` : APP_NAME
  })
}
