# HK-Parts-Custom-E-Commerce

## Barcode inventory scanner

Admin → Catalog → **Barcode Scanner** (`/inventory-scanner`): scan a product's barcode / QR code (or type its SKU)
on a phone or tablet, then update its stock and selling price. Every change is written to the
`InventoryAuditLogs` table with who, when, old → new values, and how the item was scanned.

- **The camera needs a secure context.** Browsers only allow `getUserMedia` on `https://` or
  `http://localhost`. Local dev runs on plain HTTP, so on a phone that opens the dev server at
  `http://<LAN-IP>:9000` the camera is blocked. The scan page then offers manual SKU entry and
  "scan from a photo" instead. To scan with a phone camera, serve the SPA over HTTPS (see the
  "No dev TLS" note in `CLAUDE.md`) or use a deployed HTTPS environment.
- **Handheld scanners** (USB / Bluetooth keyboard-wedge) work on the dashboard and scan pages without
  setup: scan anywhere on the page.
- **Access:** the *Inventory* module opens the scanner and saves stock. Changing a price also needs the
  *Catalog* module. Without it the price field is read-only and the API returns 403.
- **Duplicate SKUs / barcodes** make a scan show a "Several products match" picker. The pre-flight query
  in `API/db-scripts/2026-10-31_AddInventoryAuditScanDetails.sql` lists them.
- "Products updated today" uses the tenant's display timezone (Branding → Display time zone).
