/*
 * Roles module API resource group (WO-62 / REQ-ADM-004).
 *
 * Mirrors the widgetApi style from services/api.js: reuse the shared authenticated
 * axios instance + unwrap/qsSerializer helpers, expose list/get/create/update/
 * remove, plus modules() for the role editor's module selector. All endpoints
 * live under /api/admin/roles.
 */
import { api, unwrap, qsSerializer } from 'services/api'

export const roleApi = {
  // GET /api/admin/roles -> RoleDto[] (plain array; the endpoint is not paged).
  list (params = {}) {
    return api
      .get('/api/admin/roles', { params, paramsSerializer: qsSerializer })
      .then(unwrap)
  },
  get (id) {
    return api.get(`/api/admin/roles/${id}`).then(unwrap)
  },
  create (payload) {
    return api.post('/api/admin/roles', payload).then(unwrap)
  },
  update (id, payload) {
    return api.put(`/api/admin/roles/${id}`, payload).then(unwrap)
  },
  remove (id) {
    return api.delete(`/api/admin/roles/${id}`).then(unwrap)
  },
  // GET /api/admin/roles/modules -> ModuleInfo[] ({ key, displayName }) for the
  // role editor's module selector.
  modules () {
    return api.get('/api/admin/roles/modules').then(unwrap)
  }
}

export default roleApi
