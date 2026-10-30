using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Features.Products;
using HKPartsEcommerce.Application.Tests.Common;
using HKPartsEcommerce.Domain.Enums;
using Xunit;

namespace HKPartsEcommerce.Application.Tests.Products;

/// <summary>
/// Coverage for the remaining "replace child collection on a tracked product" handlers —
/// relationships, grouped members, specification values and downloads. These share the exact
/// pattern that the Guid-key value-generation fix repaired, so each proves an insert path.
/// </summary>
public class ProductSubResourceTests : CatalogTestBase
{
    // ---- Relationships ----------------------------------------------------------------------------

    [Fact]
    public async Task SetRelationships_assigns_related_products_of_a_type()
    {
        var taxId = SeedTaxCategory();
        var productId = SeedProduct(taxCategoryId: taxId);
        var relatedA = SeedProduct(p => p.Name = "A", taxCategoryId: taxId);
        var relatedB = SeedProduct(p => p.Name = "B", taxCategoryId: taxId);

        var dto = await new SetProductRelationshipsCommandHandler(NewContext()).Handle(
            new SetProductRelationshipsCommand(productId, ProductRelationshipType.CrossSell, new() { relatedA, relatedB }),
            CancellationToken.None);

        Assert.Equal(2, dto.Relationships.Count);
        Assert.All(dto.Relationships, r => Assert.Equal(ProductRelationshipType.CrossSell, r.RelationshipType));
    }

    [Fact]
    public async Task SetRelationships_leaves_other_relationship_types_untouched()
    {
        var taxId = SeedTaxCategory();
        var productId = SeedProduct(taxCategoryId: taxId);
        var related = SeedProduct(p => p.Name = "R", taxCategoryId: taxId);
        var crossSell = SeedProduct(p => p.Name = "X", taxCategoryId: taxId);

        await new SetProductRelationshipsCommandHandler(NewContext()).Handle(
            new SetProductRelationshipsCommand(productId, ProductRelationshipType.Related, new() { related }), CancellationToken.None);

        var dto = await new SetProductRelationshipsCommandHandler(NewContext()).Handle(
            new SetProductRelationshipsCommand(productId, ProductRelationshipType.CrossSell, new() { crossSell }), CancellationToken.None);

        // The Related row must survive a CrossSell replace.
        Assert.Contains(dto.Relationships, r => r.RelationshipType == ProductRelationshipType.Related && r.RelatedProductId == related);
        Assert.Contains(dto.Relationships, r => r.RelationshipType == ProductRelationshipType.CrossSell && r.RelatedProductId == crossSell);
    }

    [Fact]
    public async Task SetRelationships_rejects_self_reference()
    {
        var productId = SeedProduct();

        await Assert.ThrowsAsync<ConflictException>(() => new SetProductRelationshipsCommandHandler(NewContext()).Handle(
            new SetProductRelationshipsCommand(productId, ProductRelationshipType.Related, new() { productId }), CancellationToken.None));
    }

    [Fact]
    public async Task SetRelationships_throws_NotFound_for_unknown_related_product()
    {
        var productId = SeedProduct();

        await Assert.ThrowsAsync<NotFoundException>(() => new SetProductRelationshipsCommandHandler(NewContext()).Handle(
            new SetProductRelationshipsCommand(productId, ProductRelationshipType.Related, new() { Guid.NewGuid() }), CancellationToken.None));
    }

    // ---- Grouped members --------------------------------------------------------------------------

    [Fact]
    public async Task SetGroupedMembers_assigns_and_replaces_members()
    {
        var taxId = SeedTaxCategory();
        var groupId = SeedProduct(p => p.ProductType = ProductType.Grouped, taxId);
        var memberA = SeedProduct(p => p.Name = "A", taxCategoryId: taxId);
        var memberB = SeedProduct(p => p.Name = "B", taxCategoryId: taxId);

        await new SetGroupedMembersCommandHandler(NewContext()).Handle(
            new SetGroupedMembersCommand(groupId, new() { memberA, memberB }), CancellationToken.None);

        var dto = await new SetGroupedMembersCommandHandler(NewContext()).Handle(
            new SetGroupedMembersCommand(groupId, new() { memberB }), CancellationToken.None);

        Assert.Equal(new[] { memberB }, dto.GroupedMemberIds.ToArray());
    }

    [Fact]
    public async Task SetGroupedMembers_rejects_self_membership()
    {
        var groupId = SeedProduct(p => p.ProductType = ProductType.Grouped);

        await Assert.ThrowsAsync<ConflictException>(() => new SetGroupedMembersCommandHandler(NewContext()).Handle(
            new SetGroupedMembersCommand(groupId, new() { groupId }), CancellationToken.None));
    }

    [Fact]
    public async Task SetGroupedMembers_throws_NotFound_for_unknown_member()
    {
        var groupId = SeedProduct(p => p.ProductType = ProductType.Grouped);

        await Assert.ThrowsAsync<NotFoundException>(() => new SetGroupedMembersCommandHandler(NewContext()).Handle(
            new SetGroupedMembersCommand(groupId, new() { Guid.NewGuid() }), CancellationToken.None));
    }

    // ---- Specification values ---------------------------------------------------------------------

    [Fact]
    public async Task SetSpecificationValues_assigns_options()
    {
        var productId = SeedProduct();
        var optionId = SeedSpecificationOption();

        var dto = await new SetProductSpecificationValuesCommandHandler(NewContext()).Handle(
            new SetProductSpecificationValuesCommand(productId, new() { optionId }), CancellationToken.None);

        Assert.Equal(new[] { optionId }, dto.SpecificationOptionIds.ToArray());
    }

    [Fact]
    public async Task SetSpecificationValues_throws_NotFound_for_unknown_option()
    {
        var productId = SeedProduct();

        await Assert.ThrowsAsync<NotFoundException>(() => new SetProductSpecificationValuesCommandHandler(NewContext()).Handle(
            new SetProductSpecificationValuesCommand(productId, new() { Guid.NewGuid() }), CancellationToken.None));
    }

    [Fact]
    public async Task SetSpecificationValues_replaces_previous_options()
    {
        var productId = SeedProduct();
        var first = SeedSpecificationOption("Spec1", "V1");
        var second = SeedSpecificationOption("Spec2", "V2");
        await new SetProductSpecificationValuesCommandHandler(NewContext()).Handle(
            new SetProductSpecificationValuesCommand(productId, new() { first }), CancellationToken.None);

        var dto = await new SetProductSpecificationValuesCommandHandler(NewContext()).Handle(
            new SetProductSpecificationValuesCommand(productId, new() { second }), CancellationToken.None);

        Assert.Equal(new[] { second }, dto.SpecificationOptionIds.ToArray());
    }

    // ---- Downloads --------------------------------------------------------------------------------

    [Fact]
    public async Task SetDownloads_sets_and_replaces_downloads()
    {
        var productId = SeedProduct(p => p.ProductType = ProductType.Downloadable);
        await new SetProductDownloadsCommandHandler(NewContext()).Handle(
            new SetProductDownloadsCommand(productId, new() { new ProductDownloadInput("Manual", "https://cdn/m.pdf") }),
            CancellationToken.None);

        var dto = await new SetProductDownloadsCommandHandler(NewContext()).Handle(
            new SetProductDownloadsCommand(productId, new() { new ProductDownloadInput("Guide", "https://cdn/g.pdf", 1) }),
            CancellationToken.None);

        Assert.Single(dto.Downloads);
        Assert.Equal("Guide", dto.Downloads[0].Name);
    }

    [Fact]
    public async Task SetDownloads_empty_list_clears_downloads()
    {
        var productId = SeedProduct(p => p.ProductType = ProductType.Downloadable);
        await new SetProductDownloadsCommandHandler(NewContext()).Handle(
            new SetProductDownloadsCommand(productId, new() { new ProductDownloadInput("Manual", "https://cdn/m.pdf") }),
            CancellationToken.None);

        var dto = await new SetProductDownloadsCommandHandler(NewContext()).Handle(
            new SetProductDownloadsCommand(productId, new()), CancellationToken.None);

        Assert.Empty(dto.Downloads);
    }

    [Fact]
    public async Task SetDownloads_throws_NotFound_when_product_missing()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => new SetProductDownloadsCommandHandler(NewContext()).Handle(
            new SetProductDownloadsCommand(Guid.NewGuid(), new()), CancellationToken.None));
    }

    [Fact]
    public void SetDownloads_validator_rejects_empty_name()
    {
        var result = new SetProductDownloadsCommandValidator().Validate(
            new SetProductDownloadsCommand(Guid.NewGuid(), new() { new ProductDownloadInput("", "https://cdn/x.pdf") }));
        Assert.False(result.IsValid);
    }
}
