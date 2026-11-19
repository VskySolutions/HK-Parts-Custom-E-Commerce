using HKPartsEcommerce.Application.Common.Models;
using Xunit;

namespace HKPartsEcommerce.Application.Tests.Shipping;

/// <summary>UPS "Default package type": the legacy PHP list, rated with valid UPS PackagingType codes.</summary>
public class UpsPackageTypeTests
{
    [Theory]
    [InlineData("02", "02")]
    [InlineData("30", "30")]
    [InlineData("2a", "2a")]
    [InlineData("2b", "2b")]
    [InlineData("2c", "2c")]
    [InlineData("04", "04")]
    [InlineData("03", "03")]
    [InlineData("BAG", "02")]
    [InlineData("BOX", "02")]
    [InlineData("CTN", "02")]
    public void Every_choice_is_offered_and_rated_with_a_ups_code(string stored, string sent)
    {
        Assert.True(UpsCodes.PackageTypes.ContainsKey(stored));
        Assert.Equal(sent, UpsCodes.RatingPackagingCode(stored));
    }

    [Theory]
    [InlineData("Package (customer supplied)")]
    [InlineData("Pallet")]
    [InlineData("UPS Express Box Small")]
    [InlineData("UPS Express Box Medium")]
    [InlineData("UPS Express Box Large")]
    [InlineData("UPS PAK")]
    [InlineData("UPS Tube")]
    [InlineData("Bag")]
    [InlineData("Box")]
    [InlineData("Carton/Piece")]
    public void Legacy_labels_are_present(string label) => Assert.Contains(label, UpsCodes.PackageTypes.Values);
}
