/*
 * "Min qty allowed" / "Max qty allowed" for cart steppers. The API applies both to a product's TOTAL
 * quantity across every cart line of that product (variants, differently-engraved lines), so the
 * steppers check that same total. A missing or non-positive limit means "no limit".
 */

function productTotal (items, productId) {
  return (items || [])
    .filter((i) => i.productId === productId)
    .reduce((sum, i) => sum + (Number(i.quantity) || 0), 0)
}

function minOf (item) {
  const min = Number(item?.minOrderQuantity)
  return min > 1 ? min : null
}

function maxOf (item) {
  const max = Number(item?.maxOrderQuantity)
  return max > 0 ? max : null
}

// True when changing this line to `nextQty` keeps the product's total within its limits. Lowering a total
// that is already above the max, or raising one that is below the min, is always allowed — it moves the
// cart towards a valid state.
export function withinQtyLimits (items, item, nextQty) {
  const total = productTotal(items, item.productId) - (Number(item.quantity) || 0) + nextQty
  const min = minOf(item)
  const max = maxOf(item)
  const goingUp = nextQty > item.quantity
  if (goingUp && max != null && total > max) return false
  if (!goingUp && min != null && total < min) return false
  return true
}

// Short caption under the stepper once the product's total sits at (or beyond) a limit.
export function qtyLimitNote (items, item) {
  const total = productTotal(items, item.productId)
  const min = minOf(item)
  const max = maxOf(item)
  if (max != null && total > max) return `Maximum ${max} per order — please reduce`
  if (max != null && total === max) return `Maximum ${max} per order`
  if (min != null && total < min) return `Minimum ${min} per order — please increase`
  if (min != null && total === min) return `Minimum ${min} per order`
  return ''
}
