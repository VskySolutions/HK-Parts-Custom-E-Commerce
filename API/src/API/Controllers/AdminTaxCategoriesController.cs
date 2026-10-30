using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.TaxCategories;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>
/// Manage tax categories — the classification every product must reference (AC-CAT-001.6). Exposed so
/// the product form can offer a picker instead of requiring a hand-pasted UUID.
/// </summary>
[Route("api/admin/tax-categories")]
[RequireModule(Modules.Catalog)]
public class AdminTaxCategoriesController : ApiControllerBase
{
    /// <summary>List tax categories (paged), optionally filtered by name or restricted to active ones.</summary>
    [HttpGet]
    public async Task<ActionResult<PaginatedList<TaxCategoryDto>>> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 200,
        [FromQuery] string? search = null, [FromQuery] bool? activeOnly = null)
        => Ok(await Mediator.Send(new ListTaxCategoriesQuery(page, pageSize, search, activeOnly)));

    /// <summary>Create a new tax category.</summary>
    [HttpPost]
    public async Task<ActionResult<TaxCategoryDto>> Create([FromBody] CreateTaxCategoryCommand command)
        => Ok(await Mediator.Send(command));
}
