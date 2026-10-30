using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Common.Interfaces;

/// <summary>
/// Abstraction over the persistence context exposed to Application-layer handlers, keeping them
/// free of a direct Infrastructure/EF Core provider dependency.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<Role> Roles { get; }
    DbSet<UserRole> UserRoles { get; }
    DbSet<ApiKey> ApiKeys { get; }
    DbSet<Customer> Customers { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<SmtpAccount> SmtpAccounts { get; }
    DbSet<TenantBranding> TenantBrandings { get; }
    DbSet<SupportedCurrency> SupportedCurrencies { get; }
    DbSet<Store> Stores { get; }
    DbSet<DeliveryZone> DeliveryZones { get; }
    DbSet<AdminAlert> AdminAlerts { get; }
    DbSet<PlatformSetting> PlatformSettings { get; }
    DbSet<SettingsChangeHistory> SettingsChangeHistory { get; }
    DbSet<EmailTemplate> EmailTemplates { get; }
    DbSet<EmailQueue> EmailQueue { get; }
    DbSet<MarketingSuppression> MarketingSuppressions { get; }
    DbSet<ApplicationLog> ApplicationLogs { get; }
    DbSet<AuditTrail> AuditTrails { get; }
    DbSet<BackgroundTaskLog> BackgroundTaskLogs { get; }

    // Central media library (WO-122)
    DbSet<Media> Media { get; }

    // Per-integration credential tables (Credentials_*)
    DbSet<StripeCredential> StripeCredentials { get; }
    DbSet<PayPalCredential> PayPalCredentials { get; }
    DbSet<RazorpayCredential> RazorpayCredentials { get; }
    DbSet<SquareCredential> SquareCredentials { get; }
    DbSet<AuthorizeNetCredential> AuthorizeNetCredentials { get; }
    DbSet<TaxJarCredential> TaxJarCredentials { get; }
    DbSet<StripeTaxCredential> StripeTaxCredentials { get; }
    DbSet<FedExCredential> FedExCredentials { get; }
    DbSet<DhlExpressCredential> DhlExpressCredentials { get; }
    DbSet<UspsCredential> UspsCredentials { get; }
    DbSet<UpsCredential> UpsCredentials { get; }
    DbSet<TwilioCredential> TwilioCredentials { get; }
    DbSet<AzureBlobCredential> AzureBlobCredentials { get; }

    // Catalog (WO-10, WO-11)
    DbSet<TaxCategory> TaxCategories { get; }
    DbSet<Category> Categories { get; }
    DbSet<Manufacturer> Manufacturers { get; }
    DbSet<Product> Products { get; }
    DbSet<ProductVariant> ProductVariants { get; }
    DbSet<ProductAttribute> ProductAttributes { get; }
    DbSet<ProductAttributeValue> ProductAttributeValues { get; }
    DbSet<SpecificationAttribute> SpecificationAttributes { get; }
    DbSet<SpecificationAttributeOption> SpecificationAttributeOptions { get; }
    DbSet<ProductPicture> ProductPictures { get; }
    DbSet<CategoryPicture> CategoryPictures { get; }
    DbSet<TierPrice> TierPrices { get; }
    DbSet<ProductRelationship> ProductRelationships { get; }
    DbSet<ProductTag> ProductTags { get; }
    DbSet<ProductTagMapping> ProductTagMappings { get; }
    DbSet<ProductCategory> ProductCategories { get; }
    DbSet<ProductAttributeMapping> ProductAttributeMappings { get; }
    DbSet<ProductVariantAttributeValue> ProductVariantAttributeValues { get; }
    DbSet<ProductSpecificationValue> ProductSpecificationValues { get; }
    DbSet<GroupedProductMember> GroupedProductMembers { get; }
    DbSet<ProductDownload> ProductDownloads { get; }

    // Inventory (WO-12)
    DbSet<InventoryLevel> InventoryLevels { get; }

    // Customer accounts (WO-20, WO-21)
    DbSet<Address> Addresses { get; }
    DbSet<CustomerAddress> CustomerAddresses { get; }
    DbSet<UserToken> UserTokens { get; }

    // Customer groups + group pricing (WO-22). The group is assigned via Customer.CustomerGroupId.
    DbSet<CustomerGroup> CustomerGroups { get; }
    DbSet<CustomerGroupPrice> CustomerGroupPrices { get; }

    // Customer tax exemption requests (WO-126)
    DbSet<TaxExemptionRequest> TaxExemptionRequests { get; }
    DbSet<TaxExemptionRequestDocument> TaxExemptionRequestDocuments { get; }

    // Orders & store fulfilment (WO-51, WO-52)
    DbSet<Order> Orders { get; }
    DbSet<OrderLineItem> OrderLineItems { get; }
    DbSet<StoreManagerAssignment> StoreManagerAssignments { get; }
    DbSet<Shipment> Shipments { get; }
    DbSet<ShipmentLineItem> ShipmentLineItems { get; }
    DbSet<ShipmentTracking> ShipmentTrackingEvents { get; }
    DbSet<Rma> Rmas { get; }
    DbSet<RmaLineItem> RmaLineItems { get; }
    DbSet<StoreCreditTransaction> StoreCreditTransactions { get; }

    // Commerce — pricing, cart, shipping, payments, tax (Phase 3)
    DbSet<Discount> Discounts { get; }
    DbSet<CouponCode> CouponCodes { get; }
    DbSet<Cart> Carts { get; }
    DbSet<CartItem> CartItems { get; }
    DbSet<Wishlist> Wishlists { get; }
    DbSet<WishlistItem> WishlistItems { get; }
    DbSet<ShippingMethod> ShippingMethods { get; }
    DbSet<ShippingZone> ShippingZones { get; }
    DbSet<ShippingMethodZoneRate> ShippingMethodZoneRates { get; }
    DbSet<ShippingProviderConfiguration> ShippingProviderConfigurations { get; }
    DbSet<ShippingCarrierSetting> ShippingCarrierSettings { get; }
    DbSet<PaymentRecord> PaymentRecords { get; }
    DbSet<TaxProviderConfiguration> TaxProviderConfigurations { get; }
    DbSet<StateNexusAccumulator> StateNexusAccumulators { get; }
    DbSet<OrderStatusHistory> OrderStatusHistory { get; }

    // Security config (WO-106)
    DbSet<RecaptchaConfig> RecaptchaConfigs { get; }

    // Webhooks (WO-5)
    DbSet<WebhookSubscription> WebhookSubscriptions { get; }
    DbSet<WebhookSubscriptionEvent> WebhookSubscriptionEvents { get; }
    DbSet<WebhookDelivery> WebhookDeliveries { get; }

    // Localization (WO-18)
    DbSet<Language> Languages { get; }
    DbSet<ContentTranslation> ContentTranslations { get; }

    // Phase 5 — catalog engagement (WO-14 reviews, WO-58 Q&A)
    DbSet<ProductReview> ProductReviews { get; }
    DbSet<ProductQuestion> ProductQuestions { get; }

    // Phase 5 — CMS / Content & Marketing (WO-54/55/56/96/97/99/105)
    DbSet<CMSPageGroup> CMSPageGroups { get; }
    DbSet<CMSPage> CMSPages { get; }
    DbSet<CMSBlogPost> CMSBlogPosts { get; }
    DbSet<CMSBanner> CMSBanners { get; }
    DbSet<CMSProductCollection> CMSProductCollections { get; }
    DbSet<CMSProductCollectionItem> CMSProductCollectionItems { get; }
    DbSet<CMSHomePageSection> CMSHomePageSections { get; }
    DbSet<CMSCategoryPageConfig> CMSCategoryPageConfigs { get; }
    DbSet<CMSCategoryPinnedProduct> CMSCategoryPinnedProducts { get; }
    DbSet<CMSSearchPageContent> CMSSearchPageContents { get; }
    DbSet<CMSNewsletterSubscription> CMSNewsletterSubscriptions { get; }

    // Phase 5 — loyalty points (WO-27)
    DbSet<CustomerPointsBalance> CustomerPointsBalances { get; }
    DbSet<PointsTransaction> PointsTransactions { get; }

    // Phase 5 — subscriptions (WO-49)
    DbSet<Subscription> Subscriptions { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Load a tracked entity of the given CLR type by its primary key (or null if not found). Used by
    /// cross-cutting features (e.g. the record-audit footer) that operate polymorphically over entity
    /// types. Satisfied by <see cref="DbContext.FindAsync(Type, object?[], CancellationToken)"/>.
    /// </summary>
    ValueTask<object?> FindAsync(Type entityType, object?[]? keyValues, CancellationToken cancellationToken);
}
