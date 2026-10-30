using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Infrastructure.Persistence.Configurations;

public class TaxExemptionRequestConfiguration : IEntityTypeConfiguration<TaxExemptionRequest>
{
    public void Configure(EntityTypeBuilder<TaxExemptionRequest> b)
    {
        b.ToTable("TaxExemptionRequests");
        b.HasKey(x => x.Id);
        b.Property(x => x.CertificateNumber).HasMaxLength(200);
        b.Property(x => x.VatId).HasMaxLength(64);
        b.Property(x => x.AdminNote).HasMaxLength(1024);

        // The admin queue filters by status; the customer portal reads "my latest request".
        b.HasIndex(x => x.Status);
        b.HasIndex(x => new { x.CustomerId, x.SubmittedOnUtc });

        b.HasQueryFilter(x => !x.Deleted);
    }
}

public class TaxExemptionRequestDocumentConfiguration : IEntityTypeConfiguration<TaxExemptionRequestDocument>
{
    public void Configure(EntityTypeBuilder<TaxExemptionRequestDocument> b)
    {
        b.ToTable("TaxExemptionRequestDocuments");
        b.HasKey(x => x.Id);

        // Owned child: cascade from its single parent. MediaId stays FK-less (second cascade path).
        b.HasOne(x => x.TaxExemptionRequest)
            .WithMany(r => r.Documents)
            .HasForeignKey(x => x.TaxExemptionRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.TaxExemptionRequestId);
    }
}
