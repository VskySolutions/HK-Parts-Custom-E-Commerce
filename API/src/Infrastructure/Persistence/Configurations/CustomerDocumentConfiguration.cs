using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>
/// Customer documents (Customer → Documents tab). Customer is a NoAction FK (customers are soft-deleted, never
/// removed). No global soft-delete filter on purpose: the admin list shows deleted rows too.
/// </summary>
public class CustomerDocumentConfiguration : IEntityTypeConfiguration<CustomerDocument>
{
    public void Configure(EntityTypeBuilder<CustomerDocument> b)
    {
        b.ToTable("CustomerDocuments");
        b.HasKey(x => x.Id);
        b.Property(x => x.DocumentType).HasConversion<int>();
        b.Property(x => x.OriginalFileName).HasMaxLength(255).IsRequired();
        b.Property(x => x.StorageKey).HasMaxLength(500).IsRequired();
        b.Property(x => x.MimeType).HasMaxLength(100).IsRequired();
        b.Property(x => x.Note).HasMaxLength(2000);

        b.HasOne(x => x.Customer)
            .WithMany(c => c.Documents)
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.NoAction);

        // List = WHERE CustomerId ORDER BY CreatedOnUtc DESC; badge = COUNT WHERE CustomerId AND Deleted = 0.
        b.HasIndex(x => new { x.CustomerId, x.Deleted, x.CreatedOnUtc });
    }
}
