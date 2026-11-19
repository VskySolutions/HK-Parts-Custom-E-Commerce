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

    /// <summary>
    /// The "Default package type" choices: the legacy PHP list (EcommUPS::getPackageTypes) mapped onto UPS Rating
    /// API PackagingType codes. The PHP codes themselves are not reused: several clash with the REST codes
    /// (PHP "01" = Bag, REST "01" = UPS Letter). Bag / Box / Carton have no UPS packaging code of their own, so they
    /// are stored as site codes and rated as a customer-supplied package (<see cref="RatingPackagingCode"/>).
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> PackageTypes = new Dictionary<string, string>
    {
        ["02"] = "Package (customer supplied)",
        ["30"] = "Pallet",
        ["25"] = "UPS 10KG Box",
        ["24"] = "UPS 25KG Box",
        ["2a"] = "UPS Express Box Small",
        ["2b"] = "UPS Express Box Medium",
        ["2c"] = "UPS Express Box Large",
        ["21"] = "UPS Express Box",
        ["01"] = "UPS Letter",
        ["04"] = "UPS PAK",
        ["03"] = "UPS Tube",
        [BagPackageType] = "Bag",
        [BoxPackageType] = "Box",
        [CartonPackageType] = "Carton/Piece",
    };

    public const string BagPackageType = "BAG";
    public const string BoxPackageType = "BOX";
    public const string CartonPackageType = "CTN";

    /// <summary>The PackagingType code sent to UPS for a stored package type: Bag / Box / Carton go as "02".</summary>
    public static string RatingPackagingCode(string packageType) =>
        packageType is BagPackageType or BoxPackageType or CartonPackageType ? DefaultPackageType : packageType;

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
