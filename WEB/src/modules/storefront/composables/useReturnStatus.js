/* Customer-facing labels and colours for return (RMA) statuses. */
const LABELS = {
  Requested: 'Under review',
  Approved: 'Approved — please ship',
  InTransit: 'On its way to us',
  Received: 'Received',
  Inspected: 'Inspected',
  Refunded: 'Refunded',
  Exchanged: 'Replacement sent',
  Closed: 'Closed',
  Rejected: 'Not approved',
  Cancelled: 'Cancelled',
  Expired: 'Expired'
}

export function returnStatusLabel (status) {
  return LABELS[status] || status || '—'
}

export function returnStatusColor (status) {
  switch (status) {
    case 'Requested': return 'orange'
    case 'Approved': case 'InTransit': return 'blue'
    case 'Received': case 'Inspected': return 'purple'
    case 'Refunded': case 'Exchanged': case 'Closed': return 'positive'
    case 'Rejected': case 'Cancelled': case 'Expired': return 'negative'
    default: return 'grey'
  }
}

export default { returnStatusLabel, returnStatusColor }
