using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>
/// Shared configuration for every per-integration credential table (<c>Credentials_*</c>): Guid key, the
/// common Active/IsProduction/Name columns, and the soft-delete query filter. Secret columns are marked
/// with <c>[Encrypted]</c> and encrypted at rest by a value converter applied globally in
/// <c>AppDbContext.OnModelCreating</c>, so they are left unbounded (nvarchar(max)) here to fit ciphertext.
/// </summary>
public abstract class IntegrationCredentialConfig<T> : IEntityTypeConfiguration<T>
    where T : IntegrationCredentialBase
{
    protected abstract string TableName { get; }

    public void Configure(EntityTypeBuilder<T> b)
    {
        b.ToTable(TableName);
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).IsRequired().HasMaxLength(200);
        b.Property(x => x.TransactionFeePercent).HasPrecision(5, 2);
        b.HasQueryFilter(x => !x.Deleted);
        ConfigureFields(b);
    }

    /// <summary>Bounds the integration-specific non-secret columns. Encrypted columns are left unbounded.</summary>
    protected virtual void ConfigureFields(EntityTypeBuilder<T> b) { }
}

// ---- Payment gateways ------------------------------------------------------

public sealed class StripeCredentialConfig : IntegrationCredentialConfig<StripeCredential>
{
    protected override string TableName => "Credentials_Stripe";
    protected override void ConfigureFields(EntityTypeBuilder<StripeCredential> b)
    {
        b.Property(x => x.PublishableKey).HasMaxLength(300);
        b.Property(x => x.ReturnUrl).HasMaxLength(500);
    }
}

public sealed class PayPalCredentialConfig : IntegrationCredentialConfig<PayPalCredential>
{
    protected override string TableName => "Credentials_PayPal";
    protected override void ConfigureFields(EntityTypeBuilder<PayPalCredential> b)
    {
        b.Property(x => x.ClientId).HasMaxLength(300);
        b.Property(x => x.ReturnUrl).HasMaxLength(500);
    }
}

public sealed class RazorpayCredentialConfig : IntegrationCredentialConfig<RazorpayCredential>
{
    protected override string TableName => "Credentials_Razorpay";
    protected override void ConfigureFields(EntityTypeBuilder<RazorpayCredential> b)
        => b.Property(x => x.KeyId).HasMaxLength(200);
}

public sealed class SquareCredentialConfig : IntegrationCredentialConfig<SquareCredential>
{
    protected override string TableName => "Credentials_Square";
    protected override void ConfigureFields(EntityTypeBuilder<SquareCredential> b)
    {
        b.Property(x => x.ApplicationId).HasMaxLength(200);
        b.Property(x => x.LocationId).HasMaxLength(100);
    }
}

public sealed class AuthorizeNetCredentialConfig : IntegrationCredentialConfig<AuthorizeNetCredential>
{
    protected override string TableName => "Credentials_AuthorizeNet";
    protected override void ConfigureFields(EntityTypeBuilder<AuthorizeNetCredential> b)
    {
        b.Property(x => x.ApplicationLoginId).HasMaxLength(200);
        b.Property(x => x.PublicClientKey).HasMaxLength(200);
    }
}

// ---- Tax providers ---------------------------------------------------------

public sealed class TaxJarCredentialConfig : IntegrationCredentialConfig<TaxJarCredential>
{
    protected override string TableName => "Credentials_TaxJar";
    protected override void ConfigureFields(EntityTypeBuilder<TaxJarCredential> b)
        => b.Property(x => x.BaseUrl).HasMaxLength(500);
}

public sealed class StripeTaxCredentialConfig : IntegrationCredentialConfig<StripeTaxCredential>
{
    protected override string TableName => "Credentials_StripeTax";
    protected override void ConfigureFields(EntityTypeBuilder<StripeTaxCredential> b)
        => b.Property(x => x.PublishableKey).HasMaxLength(300);
}

// ---- Shipping carriers -----------------------------------------------------

public sealed class FedExCredentialConfig : IntegrationCredentialConfig<FedExCredential>
{
    protected override string TableName => "Credentials_FedEx";
    protected override void ConfigureFields(EntityTypeBuilder<FedExCredential> b)
    {
        b.Property(x => x.ApiKey).HasMaxLength(300);
        b.Property(x => x.AccountNumber).HasMaxLength(50);
    }
}

public sealed class DhlExpressCredentialConfig : IntegrationCredentialConfig<DhlExpressCredential>
{
    protected override string TableName => "Credentials_DHLExpress";
    protected override void ConfigureFields(EntityTypeBuilder<DhlExpressCredential> b)
    {
        b.Property(x => x.ApiKey).HasMaxLength(300);
        b.Property(x => x.AccountNumber).HasMaxLength(50);
    }
}

public sealed class UspsCredentialConfig : IntegrationCredentialConfig<UspsCredential>
{
    protected override string TableName => "Credentials_USPS";
    protected override void ConfigureFields(EntityTypeBuilder<UspsCredential> b)
        => b.Property(x => x.ConsumerKey).HasMaxLength(300);
}

public sealed class UpsCredentialConfig : IntegrationCredentialConfig<UpsCredential>
{
    protected override string TableName => "Credentials_UPS";
    protected override void ConfigureFields(EntityTypeBuilder<UpsCredential> b)
    {
        b.Property(x => x.MerchantId).HasMaxLength(100);
        b.Property(x => x.ClientId).HasMaxLength(300);
    }
}

// ---- Communication ---------------------------------------------------------

public sealed class TwilioCredentialConfig : IntegrationCredentialConfig<TwilioCredential>
{
    protected override string TableName => "Credentials_Twilio";
    protected override void ConfigureFields(EntityTypeBuilder<TwilioCredential> b)
    {
        b.Property(x => x.AccountSid).HasMaxLength(200);
        b.Property(x => x.WhatsAppNumber).HasMaxLength(40);
    }
}

// ---- Storage ---------------------------------------------------------------

public sealed class AzureBlobCredentialConfig : IntegrationCredentialConfig<AzureBlobCredential>
{
    protected override string TableName => "Credentials_AzureBlob";
}
