using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.Products;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>Manage catalog products: core CRUD, variants, assignments, media and downloads (WO-10).</summary>
[Route("api/admin/products")]
[RequireModule(Modules.Catalog)]
public class AdminProductsController : ApiControllerBase
{
    // ----- Core CRUD -----------------------------------------------------------------------------

    /// <summary>List products (paged), filtered by name, type, published state, category or manufacturer.</summary>
    [HttpGet]
    public async Task<ActionResult<PaginatedList<ProductListItemDto>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] ProductType? type = null,
        [FromQuery] bool? isPublished = null,
        [FromQuery] Guid? categoryId = null,
        [FromQuery] Guid? manufacturerId = null,
        [FromQuery] bool? isFeatured = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool sortDescending = false)
        => Ok(await Mediator.Send(new ListProductsQuery(page, pageSize, search, type, isPublished, categoryId, manufacturerId, isFeatured, sortBy, sortDescending)));

    /// <summary>Get a single product with its full child graph.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductDto>> Get(Guid id)
        => Ok(await Mediator.Send(new GetProductQuery(id)));

    /// <summary>Create a new product.</summary>
    [HttpPost]
    public async Task<ActionResult<ProductDto>> Create([FromBody] CreateProductCommand command)
        => Ok(await Mediator.Send(command));

    /// <summary>Update a product's scalar configuration (route id wins over any id in the body).</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ProductDto>> Update(Guid id, [FromBody] UpdateProductCommand command)
        => Ok(await Mediator.Send(command with { Id = id }));

    /// <summary>Delete (soft) a product.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await Mediator.Send(new DeleteProductCommand(id));
        return NoContent();
    }

    // ----- Variants ------------------------------------------------------------------------------

    /// <summary>Generate the missing variants for the product's assigned attribute-value combinations.</summary>
    [HttpPost("{id:guid}/variants/generate")]
    public async Task<ActionResult<ProductDto>> GenerateVariants(Guid id)
        => Ok(await Mediator.Send(new GenerateVariantsCommand(id)));

    /// <summary>Update a single variant's purchasable configuration.</summary>
    [HttpPut("variants/{variantId:guid}")]
    public async Task<ActionResult<ProductVariantDto>> UpdateVariant(Guid variantId, [FromBody] UpdateVariantCommand command)
        => Ok(await Mediator.Send(command with { VariantId = variantId }));

    /// <summary>Delete (soft) a variant.</summary>
    [HttpDelete("variants/{variantId:guid}")]
    public async Task<IActionResult> DeleteVariant(Guid variantId)
    {
        await Mediator.Send(new DeleteVariantCommand(variantId));
        return NoContent();
    }

    // ----- Assignments (replace-semantics) -------------------------------------------------------

    /// <summary>Replace the stores the product is listed in (empty = every store).</summary>
    [HttpPut("{id:guid}/stores")]
    public async Task<ActionResult<ProductDto>> SetStores(Guid id, [FromBody] SetProductStoresCommand command)
        => Ok(await Mediator.Send(command with { ProductId = id }));

    /// <summary>Replace the product's custom-field values.</summary>
    [HttpPut("{id:guid}/custom-fields")]
    public async Task<ActionResult<ProductDto>> SetCustomFieldValues(Guid id, [FromBody] SetProductCustomFieldValuesCommand command)
        => Ok(await Mediator.Send(command with { ProductId = id }));

    /// <summary>Replace the product's category assignments.</summary>
    [HttpPut("{id:guid}/categories")]
    public async Task<ActionResult<ProductDto>> SetCategories(Guid id, [FromBody] SetProductCategoriesCommand command)
        => Ok(await Mediator.Send(command with { ProductId = id }));

    /// <summary>Replace the product's assigned attributes (used for variant generation).</summary>
    [HttpPut("{id:guid}/attributes")]
    public async Task<ActionResult<ProductDto>> SetAttributes(Guid id, [FromBody] SetProductAttributesCommand command)
        => Ok(await Mediator.Send(command with { ProductId = id }));

    /// <summary>Replace the product's specification-attribute option values.</summary>
    [HttpPut("{id:guid}/specification-values")]
    public async Task<ActionResult<ProductDto>> SetSpecificationValues(Guid id, [FromBody] SetProductSpecificationValuesCommand command)
        => Ok(await Mediator.Send(command with { ProductId = id }));

    /// <summary>Replace the product's tags (creating any that do not yet exist).</summary>
    [HttpPut("{id:guid}/tags")]
    public async Task<ActionResult<ProductDto>> SetTags(Guid id, [FromBody] SetProductTagsCommand command)
        => Ok(await Mediator.Send(command with { ProductId = id }));

    /// <summary>Replace the product's relationships of the given type.</summary>
    [HttpPut("{id:guid}/relationships")]
    public async Task<ActionResult<ProductDto>> SetRelationships(Guid id, [FromBody] SetProductRelationshipsCommand command)
        => Ok(await Mediator.Send(command with { ProductId = id }));

    /// <summary>Replace the product's (or a variant's) tier prices.</summary>
    [HttpPut("{id:guid}/tier-prices")]
    public async Task<ActionResult<ProductDto>> SetTierPrices(Guid id, [FromBody] SetTierPricesCommand command)
        => Ok(await Mediator.Send(command with { ProductId = id }));

    /// <summary>Replace the grouped product's member products.</summary>
    [HttpPut("{id:guid}/grouped-members")]
    public async Task<ActionResult<ProductDto>> SetGroupedMembers(Guid id, [FromBody] SetGroupedMembersCommand command)
        => Ok(await Mediator.Send(command with { ProductId = id }));

    // ----- Media + downloads ---------------------------------------------------------------------

    /// <summary>Add a video embed (URL) to the product as a Media-backed picture (images use the pictures endpoints).</summary>
    [HttpPost("{id:guid}/videos")]
    public async Task<ActionResult<ProductPictureDto>> AddVideo(Guid id, [FromBody] AddProductVideoCommand command)
        => Ok(await Mediator.Send(command with { ProductId = id }));

    // ----- Pictures (Media-library backed, WO-123) -----------------------------------------------

    /// <summary>List the product's Media-backed pictures (ordered) with resolved public URLs.</summary>
    [HttpGet("{id:guid}/pictures")]
    public async Task<ActionResult<List<ProductPictureDto>>> ListPictures(Guid id)
        => Ok(await Mediator.Send(new ListProductPicturesQuery(id)));

    /// <summary>Assign a committed Media asset to the product as a picture.</summary>
    [HttpPost("{id:guid}/pictures")]
    public async Task<ActionResult<ProductPictureDto>> AssignPicture(Guid id, [FromBody] AssignProductPictureCommand command)
        => Ok(await Mediator.Send(command with { ProductId = id }));

    /// <summary>Remove a product picture (the underlying Media asset is left intact).</summary>
    [HttpDelete("pictures/{pictureId:guid}")]
    public async Task<IActionResult> RemovePicture(Guid pictureId)
    {
        await Mediator.Send(new RemoveProductPictureCommand(pictureId));
        return NoContent();
    }

    /// <summary>Replace the downloadable files/URLs attached to the product.</summary>
    [HttpPut("{id:guid}/downloads")]
    public async Task<ActionResult<ProductDto>> SetDownloads(Guid id, [FromBody] SetProductDownloadsCommand command)
        => Ok(await Mediator.Send(command with { ProductId = id }));
}
