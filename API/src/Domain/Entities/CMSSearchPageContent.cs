using HKPartsEcommerce.Domain.Common;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>Singleton configuration for the storefront search results page: heading, input placeholder,
/// results-count label, no-results message, and an optional no-results promotional banner or collection
/// (WO-105). CMS-owned: table name is <c>CMSSearchPageContent</c>.</summary>
public class CMSSearchPageContent : AuditableEntity
{
    public string? Heading { get; set; }
    public string? PlaceholderText { get; set; }
    public string? ResultsCountLabel { get; set; }
    public string? NoResultsMessage { get; set; }

    public Guid? NoResultsBannerId { get; set; }
    public CMSBanner? NoResultsBanner { get; set; }

    public Guid? NoResultsCollectionId { get; set; }
    public CMSProductCollection? NoResultsCollection { get; set; }
}
