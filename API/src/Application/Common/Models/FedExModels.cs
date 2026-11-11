namespace HKPartsEcommerce.Application.Common.Models;

/// <summary>FedEx REST service types, rate modes and pickup types (docs/fedex-shipping-requirements.md §3.2, §4).</summary>
/// <remarks>§3.2: re-map every code against the REST API's service type list before go-live.</remarks>
public static class FedExCodes
{
    public static readonly IReadOnlyDictionary<string, string> Services = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["FEDEX_GROUND"] = "Ground",
        ["GROUND_HOME_DELIVERY"] = "Ground Home Delivery",
        ["FEDEX_EXPRESS_SAVER"] = "Express Saver",
        ["FEDEX_2_DAY"] = "2 Day",
        ["FEDEX_2_DAY_AM"] = "2 Day AM",
        ["STANDARD_OVERNIGHT"] = "Standard Overnight",
        ["PRIORITY_OVERNIGHT"] = "Priority Overnight",
        ["FIRST_OVERNIGHT"] = "First Overnight",
        ["SMART_POST"] = "Ground Economy",
        ["INTERNATIONAL_ECONOMY"] = "International Economy",
        ["INTERNATIONAL_PRIORITY"] = "International Priority",
        ["FEDEX_INTERNATIONAL_PRIORITY"] = "International Priority",
        ["FEDEX_INTERNATIONAL_PRIORITY_EXPRESS"] = "International Priority Express",
        ["INTERNATIONAL_FIRST"] = "International First",
        ["FEDEX_INTERNATIONAL_CONNECT_PLUS"] = "International Connect Plus",
        ["EUROPE_FIRST_INTERNATIONAL_PRIORITY"] = "Europe First International Priority",
        ["FEDEX_1_DAY_FREIGHT"] = "1 Day Freight",
        ["FEDEX_2_DAY_FREIGHT"] = "2 Day Freight",
        ["FEDEX_3_DAY_FREIGHT"] = "3 Day Freight",
        ["FIRST_OVERNIGHT_FREIGHT"] = "First Overnight Freight",
        ["INTERNATIONAL_ECONOMY_FREIGHT"] = "International Economy Freight",
        ["INTERNATIONAL_PRIORITY_FREIGHT"] = "International Priority Freight",
        ["SAME_DAY"] = "Same Day",
        ["SAME_DAY_CITY"] = "Same Day City",
    };

    /// <summary>FX-19 / §3.2 defaults: parcel services only — freight, same-day, Europe and international off.</summary>
    public static readonly IReadOnlyList<string> DefaultServices = new[]
    {
        "FEDEX_GROUND", "GROUND_HOME_DELIVERY", "FEDEX_EXPRESS_SAVER", "FEDEX_2_DAY",
        "STANDARD_OVERNIGHT", "PRIORITY_OVERNIGHT",
    };

    public static readonly IReadOnlyList<string> RateModes = new[] { "ACCOUNT", "LIST" };

    public static readonly IReadOnlyList<string> PickupTypes = new[] { "USE_SCHEDULED_PICKUP", "DROPOFF_AT_FEDEX_LOCATION" };

    public const string DefaultRateMode = "ACCOUNT";
    public const string DefaultPickupType = "USE_SCHEDULED_PICKUP";

    public static string ServiceName(string code)
        => Services.TryGetValue(code, out var name) ? name : code.Replace('_', ' ');
}
