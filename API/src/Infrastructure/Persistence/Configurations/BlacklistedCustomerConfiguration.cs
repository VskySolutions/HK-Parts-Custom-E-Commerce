using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>
/// Append-only blacklist history. Customer is a NoAction FK (customers are soft-deleted, never removed).
/// A customer may have many rows — one per block/unblock event — so there is no unique/filtered index.
/// </summary>
public class BlacklistedCustomerConfiguration : IEntityTypeConfiguration<BlacklistedCustomer>
{
    public void Configure(EntityTypeBuilder<BlacklistedCustomer> b)
    {
        b.ToTable("BlacklistedCustomers");
        b.HasKey(x => x.Id);
        b.Property(x => x.Action).HasConversion<int>();
        // Required for Blocked events (enforced by BlacklistCustomerCommandValidator); null on unblocks.
        b.Property(x => x.Reason).HasMaxLength(BlacklistedCustomer.ReasonMaxLength);
        b.Property(x => x.InternalNotes).HasMaxLength(BlacklistedCustomer.InternalNotesMaxLength);
        // History rows are never updated, so the shared audit base's update columns are not persisted.
        b.Ignore(x => x.UpdatedById);
        b.Ignore(x => x.UpdatedOnUtc);

        b.HasOne(x => x.Customer)
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.NoAction);

        // Lookup history for one customer, newest first.
        b.HasIndex(x => x.CustomerId, "IX_BlacklistedCustomers_CustomerId");
        b.HasIndex(x => new { x.CustomerId, x.CreatedOnUtc }, "IX_BlacklistedCustomers_CustomerId_CreatedOnUtc");
    }
}
