using System.Globalization;
using System.Text;
using HKPartsEcommerce.Application.Common.Utilities;

namespace HKPartsEcommerce.Application.Features.Reports;

/// <summary>
/// Builds the CSV payloads for the operational reports (AC-ADM-002.4). Column/row shaping lives here in the
/// Application layer; RFC-4180 quoting is delegated to the shared <see cref="Csv"/> writer
/// (<c>Application/Common/Utilities/Csv.cs</c>) rather than re-implemented. Each method returns UTF-8 bytes
/// ready for a controller <c>File(bytes, "text/csv", name)</c> result.
/// </summary>
public static class ReportCsv
{
    public static byte[] BestSellers(IEnumerable<BestSellerRowDto> rows) =>
        Bytes(Csv.Write(
            new[] { "Product", "Units Sold", "Revenue" },
            rows.Select(r => new string?[]
            {
                r.ProductName,
                r.UnitsSold.ToString(CultureInfo.InvariantCulture),
                r.Revenue.ToString(CultureInfo.InvariantCulture),
            })));

    public static byte[] LowStock(IEnumerable<LowStockRowDto> rows) =>
        Bytes(Csv.Write(
            new[] { "Product", "Variant SKU", "Store", "Stock Quantity", "Low Stock Threshold" },
            rows.Select(r => new string?[]
            {
                r.ProductName,
                r.VariantSku,
                r.StoreName,
                r.StockQuantity.ToString(CultureInfo.InvariantCulture),
                r.LowStockThreshold.ToString(CultureInfo.InvariantCulture),
            })));

    public static byte[] Customers(CustomersReportDto report) =>
        Bytes(Csv.Write(
            new[] { "Customer", "Email", "Order Count", "Total Spent" },
            report.TopCustomers.Select(r => new string?[]
            {
                r.CustomerName,
                r.Email,
                r.OrderCount.ToString(CultureInfo.InvariantCulture),
                r.TotalSpent.ToString(CultureInfo.InvariantCulture),
            })));

    private static byte[] Bytes(string csv) => Encoding.UTF8.GetBytes(csv);
}
