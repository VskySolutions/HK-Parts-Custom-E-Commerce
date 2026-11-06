namespace HKPartsEcommerce.Domain.Enums;

/// <summary>Which eBay environment a store's account talks to.</summary>
public enum EbayEnvironment
{
    Sandbox = 0,
    Production = 1
}

/// <summary>Lifecycle of an <see cref="Entities.EbayListing"/>.</summary>
public enum EbayListingStatus
{
    Draft = 0,
    Active = 1,
    Ended = 2,
    Error = 3
}
