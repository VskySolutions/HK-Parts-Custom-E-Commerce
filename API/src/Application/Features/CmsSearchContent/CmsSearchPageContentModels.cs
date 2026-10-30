using HKPartsEcommerce.Application.Features.CmsBanners;
using HKPartsEcommerce.Application.Features.StorefrontCatalog;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.CmsSearchContent;

/// <summary>
/// In-code fallback strings for the storefront search-results page (WO-105). These make the page render
/// meaningfully even when no <see cref="CMSSearchPageContent"/> row has been configured, so no database
/// seeder is required — the storefront read composes stored values over these defaults field-by-field.
/// </summary>
public static class SearchContentDefaults
{
    public const string Heading = "Search results";
    public const string PlaceholderText = "Search products…";
    public const string ResultsCountLabel = "{count} results";
    public const string NoResultsMessage = "<p>No products matched your search. Try different keywords.</p>";
}

/// <summary>
/// Admin editor view of the singleton search-page configuration (WO-105). Text fields carry the raw stored
/// values (null = inheriting the in-code default); <see cref="Defaults"/> supplies the starting content when
/// no row exists yet. The two optional references point at the no-results promotional banner/collection.
/// </summary>
public class CmsSearchPageContentDto
{
    /// <summary>The stored singleton's id; null when nothing has been configured yet.</summary>
    public Guid? Id { get; set; }

    public string? Heading { get; set; }
    public string? PlaceholderText { get; set; }
    public string? ResultsCountLabel { get; set; }
    public string? NoResultsMessage { get; set; }

    public Guid? NoResultsBannerId { get; set; }
    public Guid? NoResultsCollectionId { get; set; }

    /// <summary>Projects the stored singleton row as-is (nulls preserved so the editor can tell which fields
    /// are inheriting the default).</summary>
    public static CmsSearchPageContentDto From(CMSSearchPageContent e) => new()
    {
        Id = e.Id,
        Heading = e.Heading,
        PlaceholderText = e.PlaceholderText,
        ResultsCountLabel = e.ResultsCountLabel,
        NoResultsMessage = e.NoResultsMessage,
        NoResultsBannerId = e.NoResultsBannerId,
        NoResultsCollectionId = e.NoResultsCollectionId,
    };

    /// <summary>The starting editor state when no configuration row exists: the in-code defaults, no references.</summary>
    public static CmsSearchPageContentDto Defaults() => new()
    {
        Id = null,
        Heading = SearchContentDefaults.Heading,
        PlaceholderText = SearchContentDefaults.PlaceholderText,
        ResultsCountLabel = SearchContentDefaults.ResultsCountLabel,
        NoResultsMessage = SearchContentDefaults.NoResultsMessage,
    };
}

/// <summary>
/// Public storefront projection of the search-results page content (WO-105): the effective text (stored value
/// over the in-code default, resolved field-by-field) plus the resolved no-results promotional banner and the
/// no-results collection's products, when configured. Always populated, so the page is never blank.
/// </summary>
public class StorefrontSearchContentDto
{
    public string Heading { get; set; } = SearchContentDefaults.Heading;
    public string PlaceholderText { get; set; } = SearchContentDefaults.PlaceholderText;
    public string ResultsCountLabel { get; set; } = SearchContentDefaults.ResultsCountLabel;
    public string NoResultsMessage { get; set; } = SearchContentDefaults.NoResultsMessage;

    /// <summary>Promotional banner to show when a search yields no results; null when none is configured (or the
    /// configured banner is disabled or outside its active date window, which is never surfaced to buyers).</summary>
    public CmsBannerPublicDto? NoResultsBanner { get; set; }

    /// <summary>Products from the configured no-results collection (published only, in curated order, with any
    /// Customer Group pricing applied); empty when no collection is configured or it is disabled/empty.</summary>
    public IReadOnlyList<StorefrontProductSummaryDto> NoResultsProducts { get; set; } = Array.Empty<StorefrontProductSummaryDto>();
}
