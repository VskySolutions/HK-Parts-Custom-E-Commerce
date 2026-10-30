namespace HKPartsEcommerce.Application.Features.Reports;

/// <summary>Per-store performance report for a period (REQ-STR-005).</summary>
public class StorePerformanceReportDto
{
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }
    public List<StorePerformanceRowDto> Stores { get; set; } = new();
}

/// <summary>One store's KPIs over the reporting window.</summary>
public class StorePerformanceRowDto
{
    public Guid StoreId { get; set; }
    public string StoreName { get; set; } = string.Empty;

    /// <summary>Orders assigned to the store that were placed within the window.</summary>
    public int OrdersReceived { get; set; }

    /// <summary>Orders that reached Delivered within the window's placed set.</summary>
    public int OrdersFulfilled { get; set; }

    /// <summary>Gross revenue of non-cancelled orders (grand total).</summary>
    public decimal Revenue { get; set; }

    /// <summary>Average Placed→Delivered time in hours over delivered orders; null when none delivered.</summary>
    public double? AverageFulfilmentHours { get; set; }
}
