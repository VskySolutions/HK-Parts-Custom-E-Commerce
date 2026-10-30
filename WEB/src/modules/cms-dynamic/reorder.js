/*
 * Tiny array-move helper shared by every CMS ordering surface. The app has no drag-and-drop
 * library, so lists reorder with up/down arrow buttons: this moves the item at `index` one slot in
 * `direction` (-1 up, +1 down) and returns a NEW array (no mutation), or the original array
 * unchanged when the move would fall off either end.
 */
export function moveInArray (arr, index, direction) {
  const target = index + direction
  if (target < 0 || target >= arr.length) return arr
  const next = arr.slice()
  const [moved] = next.splice(index, 1)
  next.splice(target, 0, moved)
  return next
}

export default moveInArray
