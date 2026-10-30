using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Infrastructure.Persistence.Configurations;

public class LanguageConfiguration : IEntityTypeConfiguration<Language>
{
    public void Configure(EntityTypeBuilder<Language> b)
    {
        b.ToTable("Languages");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(16).IsRequired();
        b.Property(x => x.Name).HasMaxLength(128).IsRequired();
        b.Property(x => x.NativeName).HasMaxLength(128);
        b.HasIndex(x => x.Code).IsUnique();
    }
}

public class ContentTranslationConfiguration : IEntityTypeConfiguration<ContentTranslation>
{
    public void Configure(EntityTypeBuilder<ContentTranslation> b)
    {
        b.ToTable("ContentTranslations");
        b.HasKey(x => x.Id);
        b.Property(x => x.EntityType).HasMaxLength(64).IsRequired();
        b.Property(x => x.FieldName).HasMaxLength(64).IsRequired();
        b.Property(x => x.LanguageCode).HasMaxLength(16).IsRequired();
        b.Property(x => x.Value).IsRequired();

        b.HasIndex(x => new { x.EntityType, x.EntityId, x.FieldName, x.LanguageCode }).IsUnique();
        b.HasIndex(x => new { x.EntityType, x.EntityId });
    }
}
