namespace HKPartsEcommerce.Application.Common.Models;

/// <summary>UPS service, packaging and pickup codes (docs/ups-shipping-requirements.md §3.2, §4).</summary>
public static class UpsCodes
{
    public static readonly IReadOnlyDictionary<string, string> Services = new Dictionary<string, string>
    {
        ["03"] = "Ground",
        ["12"] = "3 Day Select",
        ["02"] = "2nd Day Air",
        ["59"] = "2nd Day Air AM",
        ["13"] = "Next Day Air Saver",
        ["01"] = "Next Day Air",
        ["14"] = "Next Day Air Early AM",
        ["65"] = "UPS Saver",
        ["11"] = "UPS Standard",
        ["07"] = "Worldwide Express",
        ["08"] = "Worldwide Expedited",
        ["54"] = "Worldwide Express Plus",
        ["96"] = "Worldwide Express Freight",
    };

    /// <summary>§3.2 proposed defaults: domestic on, international off.</summary>
    public static readonly IReadOnlyList<string> DefaultServices = new[] { "03", "12", "02", "13", "01" };

    /// <summary>UP-22: the Next Day services an FFL cart may use.</summary>
    public static readonly IReadOnlyList<string> DefaultFflServices = new[] { "01", "13", "14" };

    public static readonly IReadOnlyDictionary<string, string> PackageTypes = new Dictionary<string, string>
    {
        ["02"] = "Customer supplied package",
        ["01"] = "UPS Letter",
        ["03"] = "Tube",
        ["04"] = "PAK",
        ["21"] = "UPS Express Box",
        ["24"] = "UPS 25KG Box",
        ["25"] = "UPS 10KG Box",
    };

    public static readonly IReadOnlyDictionary<string, string> PickupTypes = new Dictionary<string, string>
    {
        ["01"] = "Daily Pickup",
        ["03"] = "Customer Counter",
        ["06"] = "One Time Pickup",
        ["07"] = "On Call Air",
        ["19"] = "Letter Center",
        ["20"] = "Air Service Center",
    };

    public const string DefaultApiVersion = "v2409";
    public const string DefaultPackageType = "02";
    public const string DefaultPickupType = "01";

    public static string ServiceName(string code)
        => Services.TryGetValue(code, out var name) ? name : $"Service {code}";

    /// <summary>Splits a stored comma-separated list into trimmed, de-duplicated values.</summary>
    public static List<string> SplitList(string? csv) => UspsMailClasses.SplitList(csv);

    public static string? JoinList(IEnumerable<string>? values) => UspsMailClasses.JoinList(values);
}
