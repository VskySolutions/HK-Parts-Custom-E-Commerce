/*
 * Product Q&A module API resource group (WO-58).
 *
 * Admin question-moderation queue under /api/admin/product-questions: a paged
 * list, posting an answer to a question, and moderation (Approve/Reject).
 */
import { api, unwrap, qsSerializer } from 'services/api'

export const questionApi = {
  // GET /api/admin/product-questions -> PaginatedList<AdminProductQuestionDto>.
  list (params = {}) {
    return api
      .get('/api/admin/product-questions', { params, paramsSerializer: qsSerializer })
      .then(unwrap)
  },
  // POST /api/admin/product-questions — author a pre-answered FAQ (published immediately as Approved).
  createFaq (payload) {
    return api.post('/api/admin/product-questions', payload).then(unwrap)
  },
  // POST /api/admin/product-questions/{id}/answer — publish an answer to the question.
  answer (id, answerText) {
    return api.post(`/api/admin/product-questions/${id}/answer`, { answerText }).then(unwrap)
  },
  // POST /api/admin/product-questions/{id}/moderate — status is 'Approved' | 'Rejected'.
  moderate (id, status) {
    return api.post(`/api/admin/product-questions/${id}/moderate`, { status }).then(unwrap)
  }
}

export default questionApi
