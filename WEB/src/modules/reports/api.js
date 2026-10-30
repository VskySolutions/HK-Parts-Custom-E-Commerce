/* Reports admin module API layer (WO-119): store-level performance. */
import { api, unwrap, qsSerializer } from 'services/api'

export const reportApi = {
  storePerformance (params = {}) {
    return api.get('/api/admin/reports/store-performance', { params, paramsSerializer: qsSerializer }).then(unwrap)
  }
}

export default { reportApi }
