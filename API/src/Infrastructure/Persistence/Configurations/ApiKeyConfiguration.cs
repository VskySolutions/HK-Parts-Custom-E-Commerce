using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Infrastructure.Persistence.Configurations;

public class ApiKeyConfiguration : IEntityTypeConfiguration<ApiKey>
{
    public void Configure(EntityTypeBuilder<ApiKey> b)
    {
        b.ToTable("ApiKeys");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.KeyHash).HasMaxLength(128).IsRequired();
        b.Property(x => x.Prefix).HasMaxLength(32).IsRequired();

        // Keys are looked up by hash on every authenticated M2M request; unique among non-deleted rows.
        b.HasIndex(x => x.KeyHash).IsUnique().HasFilter("[Deleted] = 0");
        b.HasQueryFilter(x => !x.Deleted);
    }
}
