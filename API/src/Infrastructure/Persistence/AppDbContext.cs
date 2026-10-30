using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Common;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Infrastructure.Persistence;

/// <summary>
/// The single EF Core persistence context for the deployment. Applies audit metadata and converts
/// hard deletes of soft-deletable entities into soft deletes on save.
/// </summary>
public class AppDbContext : DbContext, IApplicationDbContext
{
    private readonly ICurrentUserService? _currentUser;
    private readonly IDateTimeProvider? _clock;
    private readonly ICredentialProtector? _protector;

    public AppDbContext(
        DbContextOptions<AppDbContext> options,
        ICurrentUserService? currentUser = null,
        IDateTimeProvider? clock = null,
        ICredentialProtector? protector = null)
        : base(options)
    {
        _currentUser = currentUser;
        _clock = clock;
        _protector = protector;
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<SmtpAccount> SmtpAccounts => Set<SmtpAccount>();
    public DbSet<TenantBranding> TenantBrandings => Set<TenantBranding>();
    public DbSet<SupportedCurrency> SupportedCurrencies => Set<SupportedCurrency>();
    public DbSet<Store> Stores => Set<Store>();
    public DbSet<DeliveryZone> DeliveryZones => Set<DeliveryZone>();
    public DbSet<AdminAlert> AdminAlerts => Set<AdminAlert>();
    public DbSet<PlatformSetting> PlatformSettings => Set<PlatformSetting>();
    public DbSet<SettingsChangeHistory> SettingsChangeHistory => Set<SettingsChangeHistory>();
    public DbSet<EmailTemplate> EmailTemplates => Set<EmailTemplate>();
    public DbSet<EmailQueue> EmailQueue => Set<EmailQueue>();
    public DbSet<MarketingSuppression> MarketingSuppressions => Set<MarketingSuppression>();
    public DbSet<ApplicationLog> ApplicationLogs => Set<ApplicationLog>();
    public DbSet<AuditTrail> AuditTrails => Set<AuditTrail>();
    public DbSet<BackgroundTaskLog> BackgroundTaskLogs => Set<BackgroundTaskLog>();

    // Central media library (WO-122)
    public DbSet<Media> Media => Set<Media>();

    // Per-integration credential tables (Credentials_*)
    public DbSet<StripeCredential> StripeCredentials => Set<StripeCredential>();
    public DbSet<PayPalCredential> PayPalCredentials => Set<PayPalCredential>();
    public DbSet<RazorpayCredential> RazorpayCredentials => Set<RazorpayCredential>();
    public DbSet<SquareCredential> SquareCredentials => Set<SquareCredential>();
    public DbSet<AuthorizeNetCredential> AuthorizeNetCredentials => Set<AuthorizeNetCredential>();
    public DbSet<TaxJarCredential> TaxJarCredentials => Set<TaxJarCredential>();
    public DbSet<StripeTaxCredential> StripeTaxCredentials => Set<StripeTaxCredential>();
    public DbSet<FedExCredential> FedExCredentials => Set<FedExCredential>();
    public DbSet<DhlExpressCredential> DhlExpressCredentials => Set<DhlExpressCredential>();
    public DbSet<UspsCredential> UspsCredentials => Set<UspsCredential>();
    public DbSet<UpsCredential> UpsCredentials => Set<UpsCredential>();
    public DbSet<TwilioCredential> TwilioCredentials => Set<TwilioCredential>();
    public DbSet<AzureBlobCredential> AzureBlobCredentials => Set<AzureBlobCredential>();

    // Catalog (WO-10, WO-11)
    public DbSet<TaxCategory> TaxCategories => Set<TaxCategory>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Manufacturer> Manufacturers => Set<Manufacturer>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<ProductAttribute> ProductAttributes => Set<ProductAttribute>();
    public DbSet<ProductAttributeValue> ProductAttributeValues => Set<ProductAttributeValue>();
    public DbSet<SpecificationAttribute> SpecificationAttributes => Set<SpecificationAttribute>();
    public DbSet<SpecificationAttributeOption> SpecificationAttributeOptions => Set<SpecificationAttributeOption>();
    public DbSet<ProductPicture> ProductPictures => Set<ProductPicture>();
    public DbSet<CategoryPicture> CategoryPictures => Set<CategoryPicture>();
    public DbSet<TierPrice> TierPrices => Set<TierPrice>();
    public DbSet<ProductRelationship> ProductRelationships => Set<ProductRelationship>();
    public DbSet<ProductTag> ProductTags => Set<ProductTag>();
    public DbSet<ProductTagMapping> ProductTagMappings => Set<ProductTagMapping>();
    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();
    public DbSet<ProductAttributeMapping> ProductAttributeMappings => Set<ProductAttributeMapping>();
    public DbSet<ProductVariantAttributeValue> ProductVariantAttributeValues => Set<ProductVariantAttributeValue>();
    public DbSet<ProductSpecificationValue> ProductSpecificationValues => Set<ProductSpecificationValue>();
    public DbSet<GroupedProductMember> GroupedProductMembers => Set<GroupedProductMember>();
    public DbSet<ProductDownload> ProductDownloads => Set<ProductDownload>();
    public DbSet<ProductStoreMapping> ProductStoreMappings => Set<ProductStoreMapping>();
    public DbSet<CategoryFilterAttribute> CategoryFilterAttributes => Set<CategoryFilterAttribute>();
    public DbSet<ProductCustomField> ProductCustomFields => Set<ProductCustomField>();
    public DbSet<ProductCustomFieldValue> ProductCustomFieldValues => Set<ProductCustomFieldValue>();

    // Inventory (WO-12)
    public DbSet<InventoryLevel> InventoryLevels => Set<InventoryLevel>();

    // Customer accounts (WO-20, WO-21)
    public DbSet<Address> Addresses => Set<Address>();
    public DbSet<CustomerAddress> CustomerAddresses => Set<CustomerAddress>();
    public DbSet<UserToken> UserTokens => Set<UserToken>();

    // Customer groups + group pricing (WO-22). The group is assigned via Customer.CustomerGroupId.
    public DbSet<CustomerGroup> CustomerGroups => Set<CustomerGroup>();
    public DbSet<CustomerGroupPrice> CustomerGroupPrices => Set<CustomerGroupPrice>();

    // Customer tax exemption requests (WO-126)
    public DbSet<TaxExemptionRequest> TaxExemptionRequests => Set<TaxExemptionRequest>();
    public DbSet<TaxExemptionRequestDocument> TaxExemptionRequestDocuments => Set<TaxExemptionRequestDocument>();

    // Orders & store fulfilment (WO-51, WO-52)
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderLineItem> OrderLineItems => Set<OrderLineItem>();
    public DbSet<StoreManagerAssignment> StoreManagerAssignments => Set<StoreManagerAssignment>();
    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<ShipmentLineItem> ShipmentLineItems => Set<ShipmentLineItem>();
    public DbSet<ShipmentTracking> ShipmentTrackingEvents => Set<ShipmentTracking>();
    public DbSet<Rma> Rmas => Set<Rma>();
    public DbSet<StoreCreditTransaction> StoreCreditTransactions => Set<StoreCreditTransaction>();
    public DbSet<RmaLineItem> RmaLineItems => Set<RmaLineItem>();

    // Commerce — pricing, cart, shipping, payments, tax (Phase 3)
    public DbSet<Discount> Discounts => Set<Discount>();
    public DbSet<CouponCode> CouponCodes => Set<CouponCode>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Wishlist> Wishlists => Set<Wishlist>();
    public DbSet<WishlistItem> WishlistItems => Set<WishlistItem>();
    public DbSet<ShippingMethod> ShippingMethods => Set<ShippingMethod>();
    public DbSet<ShippingZone> ShippingZones => Set<ShippingZone>();
    public DbSet<ShippingMethodZoneRate> ShippingMethodZoneRates => Set<ShippingMethodZoneRate>();
    public DbSet<ShippingProviderConfiguration> ShippingProviderConfigurations => Set<ShippingProviderConfiguration>();
    public DbSet<ShippingCarrierSetting> ShippingCarrierSettings => Set<ShippingCarrierSetting>();
    public DbSet<PaymentRecord> PaymentRecords => Set<PaymentRecord>();
    public DbSet<TaxProviderConfiguration> TaxProviderConfigurations => Set<TaxProviderConfiguration>();
    public DbSet<StateNexusAccumulator> StateNexusAccumulators => Set<StateNexusAccumulator>();
    public DbSet<OrderStatusHistory> OrderStatusHistory => Set<OrderStatusHistory>();

    // Security config (WO-106)
    public DbSet<RecaptchaConfig> RecaptchaConfigs => Set<RecaptchaConfig>();

    // Webhooks (WO-5)
    public DbSet<WebhookSubscription> WebhookSubscriptions => Set<WebhookSubscription>();
    public DbSet<WebhookSubscriptionEvent> WebhookSubscriptionEvents => Set<WebhookSubscriptionEvent>();
    public DbSet<WebhookDelivery> WebhookDeliveries => Set<WebhookDelivery>();

    // Localization (WO-18)
    public DbSet<Language> Languages => Set<Language>();
    public DbSet<ContentTranslation> ContentTranslations => Set<ContentTranslation>();

    // Phase 5 — catalog engagement (WO-14 reviews, WO-58 Q&A)
    public DbSet<ProductReview> ProductReviews => Set<ProductReview>();
    public DbSet<ProductQuestion> ProductQuestions => Set<ProductQuestion>();

    // Phase 5 — CMS / Content & Marketing (WO-54/55/56/96/97/99/105)
    public DbSet<CMSPageGroup> CMSPageGroups => Set<CMSPageGroup>();
    public DbSet<CMSPage> CMSPages => Set<CMSPage>();
    public DbSet<CMSBlogPost> CMSBlogPosts => Set<CMSBlogPost>();
    public DbSet<CMSBanner> CMSBanners => Set<CMSBanner>();
    public DbSet<CMSProductCollection> CMSProductCollections => Set<CMSProductCollection>();
    public DbSet<CMSProductCollectionItem> CMSProductCollectionItems => Set<CMSProductCollectionItem>();
    public DbSet<CMSHomePageSection> CMSHomePageSections => Set<CMSHomePageSection>();
    public DbSet<CMSCategoryPageConfig> CMSCategoryPageConfigs => Set<CMSCategoryPageConfig>();
    public DbSet<CMSCategoryPinnedProduct> CMSCategoryPinnedProducts => Set<CMSCategoryPinnedProduct>();
    public DbSet<CMSSearchPageContent> CMSSearchPageContents => Set<CMSSearchPageContent>();
    public DbSet<CMSNewsletterSubscription> CMSNewsletterSubscriptions => Set<CMSNewsletterSubscription>();

    // Phase 5 — loyalty points (WO-27)
    public DbSet<CustomerPointsBalance> CustomerPointsBalances => Set<CustomerPointsBalance>();
    public DbSet<PointsTransaction> PointsTransactions => Set<PointsTransaction>();

    // Phase 5 — subscriptions (WO-49)
    public DbSet<Subscription> Subscriptions => Set<Subscription>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // Encrypt-at-rest for [Encrypted] string columns (per-integration credential secrets). The
        // converter is applied only when a protector is available — i.e. at runtime, not at design time.
        // It is a string→string conversion, so the column DDL (nvarchar) is identical either way and
        // generated migrations / model-in-sync checks are unaffected. Captured into a local so the cached
        // model closes over the singleton protector rather than a specific (disposable) context instance.
        var protector = _protector;
        if (protector is not null)
        {
            var encryptedConverter = new ValueConverter<string, string>(
                v => protector.Protect(v),
                v => protector.Unprotect(v));

            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
                foreach (var property in entityType.GetProperties())
                    if (property.ClrType == typeof(string) &&
                        property.PropertyInfo?.IsDefined(typeof(EncryptedAttribute), inherit: false) == true)
                        property.SetValueConverter(encryptedConverter);
        }

        // Every Guid surrogate key is assigned client-side in BaseEntity's initializer, yet EF's
        // convention marks single Guid keys as store-generated (ValueGeneratedOnAdd). That mismatch
        // makes EF mis-classify a NEW child added to a *tracked* parent's collection as Modified
        // (its key is already populated) instead of Added — emitting an UPDATE of a non-existent row
        // that throws DbUpdateConcurrencyException. Declaring these keys client-generated fixes every
        // "load aggregate, mutate children, save" handler (e.g. Set{Categories,Tags,Attributes,
        // TierPrices}, ReplaceProductImages, GenerateVariants). It is a metadata-only change: a Guid
        // PK carries no database default, so the column DDL is identical.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var key = entityType.FindPrimaryKey();
            if (key is { Properties.Count: 1 } && key.Properties[0].ClrType == typeof(Guid))
                key.Properties[0].ValueGenerated = ValueGenerated.Never;
        }

        base.OnModelCreating(modelBuilder);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditInformation();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        ApplyAuditInformation();
        return base.SaveChanges();
    }

    private void ApplyAuditInformation()
    {
        var now = _clock?.UtcNow ?? DateTime.UtcNow;
        var userId = _currentUser?.UserId;

        foreach (var entry in ChangeTracker.Entries<IAuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedOnUtc = now;
                    entry.Entity.CreatedById = userId;
                    entry.Entity.UpdatedOnUtc = now;
                    entry.Entity.UpdatedById = userId;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedOnUtc = now;
                    entry.Entity.UpdatedById = userId;
                    break;
            }
        }

        foreach (var entry in ChangeTracker.Entries<ISoftDeletable>())
        {
            if (entry.State == EntityState.Deleted)
            {
                entry.State = EntityState.Modified;
                entry.Entity.Deleted = true;
                entry.Entity.DeletedOnUtc = now;
            }
        }
    }
}
