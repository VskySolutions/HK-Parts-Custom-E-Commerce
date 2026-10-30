using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Infrastructure.Persistence.Configurations;

public class TaxCategoryConfiguration : IEntityTypeConfiguration<TaxCategory>
{
    public void Configure(EntityTypeBuilder<TaxCategory> b)
    {
        b.ToTable("TaxCategories");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(120).IsRequired();
        b.Property(x => x.Description).HasMaxLength(500);
        b.Property(x => x.DefaultRatePercent).HasPrecision(9, 4);
        b.HasQueryFilter(x => !x.Deleted);
    }
}

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> b)
    {
        b.ToTable("Categories");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasMaxLength(2000);
        b.Property(x => x.Slug).HasMaxLength(220);
        b.Property(x => x.MetaTitle).HasMaxLength(300);
        b.Property(x => x.MetaDescription).HasMaxLength(500);
        b.Property(x => x.MetaKeywords).HasMaxLength(500);
        b.Property(x => x.CanonicalUrl).HasMaxLength(500);

        b.HasOne(x => x.Parent)
            .WithMany(x => x.Children)
            .HasForeignKey(x => x.ParentId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasIndex(x => x.ParentId);
        b.HasIndex(x => x.Slug).IsUnique().HasFilter("[Slug] IS NOT NULL AND [Deleted] = 0");
        b.HasIndex(x => x.IsFeatured);
        b.HasQueryFilter(x => !x.Deleted);
    }
}

public class ManufacturerConfiguration : IEntityTypeConfiguration<Manufacturer>
{
    public void Configure(EntityTypeBuilder<Manufacturer> b)
    {
        b.ToTable("Manufacturers");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasMaxLength(2000);
        b.Property(x => x.LogoUrl).HasMaxLength(500);
        b.Property(x => x.Slug).HasMaxLength(220);
        b.Property(x => x.MetaTitle).HasMaxLength(300);
        b.Property(x => x.MetaDescription).HasMaxLength(500);
        b.Property(x => x.MetaKeywords).HasMaxLength(500);

        b.HasOne(x => x.LogoMedia)
            .WithMany()
            .HasForeignKey(x => x.LogoMediaId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasIndex(x => x.Slug).IsUnique().HasFilter("[Slug] IS NOT NULL AND [Deleted] = 0");
        b.HasQueryFilter(x => !x.Deleted);
    }
}

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> b)
    {
        b.ToTable("Products");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(400).IsRequired();
        b.Property(x => x.Slug).HasMaxLength(400);
        b.Property(x => x.ShortDescription).HasMaxLength(1000);
        b.Property(x => x.Sku).HasMaxLength(100);
        b.Property(x => x.InquiryButtonLabel).HasMaxLength(80);
        b.Property(x => x.SubscriptionIntervals).HasMaxLength(200);
        b.Property(x => x.MetaTitle).HasMaxLength(300);
        b.Property(x => x.MetaDescription).HasMaxLength(500);
        b.Property(x => x.MetaKeywords).HasMaxLength(500);
        b.Property(x => x.Price).HasPrecision(18, 2);
        b.Property(x => x.GiftCardAmount).HasPrecision(18, 2);
        b.Property(x => x.ProductType).HasConversion<int>();
        b.Property(x => x.GiftCardType).HasConversion<int?>();

        b.HasOne(x => x.TaxCategory)
            .WithMany(t => t.Products)
            .HasForeignKey(x => x.TaxCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.Manufacturer)
            .WithMany(m => m.Products)
            .HasForeignKey(x => x.ManufacturerId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.Slug).IsUnique().HasFilter("[Slug] IS NOT NULL AND [Deleted] = 0");
        b.HasIndex(x => x.ProductType);
        b.HasIndex(x => x.IsPublished);
        b.HasIndex(x => new { x.IsFeatured, x.FeaturedDisplayOrder });
        b.HasIndex(x => x.ManufacturerId);
        b.HasQueryFilter(x => !x.Deleted);
    }
}

public class ProductVariantConfiguration : IEntityTypeConfiguration<ProductVariant>
{
    public void Configure(EntityTypeBuilder<ProductVariant> b)
    {
        b.ToTable("ProductVariants");
        b.HasKey(x => x.Id);
        b.Property(x => x.Sku).HasMaxLength(100);
        b.Property(x => x.Price).HasPrecision(18, 2);

        b.HasOne(x => x.Product)
            .WithMany(p => p.Variants)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.ProductId);
        b.HasIndex(x => x.Sku);
        b.HasQueryFilter(x => !x.Deleted);
    }
}

public class ProductAttributeConfiguration : IEntityTypeConfiguration<ProductAttribute>
{
    public void Configure(EntityTypeBuilder<ProductAttribute> b)
    {
        b.ToTable("ProductAttributes");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasMaxLength(1000);
        b.Property(x => x.DisplayType).HasConversion<int>();
        b.Property(x => x.InputType).HasConversion<int>();
        b.HasQueryFilter(x => !x.Deleted);
    }
}

public class ProductAttributeValueConfiguration : IEntityTypeConfiguration<ProductAttributeValue>
{
    public void Configure(EntityTypeBuilder<ProductAttributeValue> b)
    {
        b.ToTable("ProductAttributeValues");
        b.HasKey(x => x.Id);
        b.Property(x => x.Value).HasMaxLength(400).IsRequired();
        b.Property(x => x.ColorHex).HasMaxLength(9);

        b.HasOne(x => x.ProductAttribute)
            .WithMany(a => a.Values)
            .HasForeignKey(x => x.ProductAttributeId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.ProductAttributeId);
    }
}

public class SpecificationAttributeConfiguration : IEntityTypeConfiguration<SpecificationAttribute>
{
    public void Configure(EntityTypeBuilder<SpecificationAttribute> b)
    {
        b.ToTable("SpecificationAttributes");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.HasQueryFilter(x => !x.Deleted);
    }
}

public class SpecificationAttributeOptionConfiguration : IEntityTypeConfiguration<SpecificationAttributeOption>
{
    public void Configure(EntityTypeBuilder<SpecificationAttributeOption> b)
    {
        b.ToTable("SpecificationAttributeOptions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Value).HasMaxLength(400).IsRequired();

        b.HasOne(x => x.SpecificationAttribute)
            .WithMany(a => a.Options)
            .HasForeignKey(x => x.SpecificationAttributeId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.SpecificationAttributeId);
    }
}

/// <summary>Media-library-backed product pictures (WO-123): the unified image + video mapping. Product
/// cascades; the optional variant and the Media asset are NoAction lookups.</summary>
public class ProductPictureConfiguration : IEntityTypeConfiguration<ProductPicture>
{
    public void Configure(EntityTypeBuilder<ProductPicture> b)
    {
        b.ToTable("ProductPictures");
        b.HasKey(x => x.Id);

        b.HasOne(x => x.ProductVariant)
            .WithMany()
            .HasForeignKey(x => x.ProductVariantId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasOne(x => x.Product)
            .WithMany(p => p.Pictures)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Media)
            .WithMany()
            .HasForeignKey(x => x.MediaId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasIndex(x => new { x.ProductId, x.DisplayOrder });
    }
}

/// <summary>Media-library-backed category pictures: category cascades; Media is a NoAction lookup.</summary>
public class CategoryPictureConfiguration : IEntityTypeConfiguration<CategoryPicture>
{
    public void Configure(EntityTypeBuilder<CategoryPicture> b)
    {
        b.ToTable("CategoryPictures");
        b.HasKey(x => x.Id);

        b.HasOne(x => x.Category)
            .WithMany()
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Media)
            .WithMany()
            .HasForeignKey(x => x.MediaId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasIndex(x => new { x.CategoryId, x.DisplayOrder });
    }
}

public class TierPriceConfiguration : IEntityTypeConfiguration<TierPrice>
{
    public void Configure(EntityTypeBuilder<TierPrice> b)
    {
        b.ToTable("TierPrices");
        b.HasKey(x => x.Id);
        b.Property(x => x.Price).HasPrecision(18, 2);

        b.HasOne(x => x.Product)
            .WithMany(p => p.TierPrices)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.ProductVariant)
            .WithMany(v => v.TierPrices)
            .HasForeignKey(x => x.ProductVariantId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasIndex(x => x.ProductId);
        b.HasIndex(x => x.ProductVariantId);
    }
}

public class ProductRelationshipConfiguration : IEntityTypeConfiguration<ProductRelationship>
{
    public void Configure(EntityTypeBuilder<ProductRelationship> b)
    {
        b.ToTable("ProductRelationships");
        b.HasKey(x => x.Id);
        b.Property(x => x.RelationshipType).HasConversion<int>();

        b.HasOne(x => x.Product)
            .WithMany(p => p.Relationships)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.RelatedProduct)
            .WithMany()
            .HasForeignKey(x => x.RelatedProductId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasIndex(x => new { x.ProductId, x.RelationshipType });
    }
}

public class ProductTagConfiguration : IEntityTypeConfiguration<ProductTag>
{
    public void Configure(EntityTypeBuilder<ProductTag> b)
    {
        b.ToTable("ProductTags");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Slug).HasMaxLength(220);
        b.HasIndex(x => x.Slug).IsUnique().HasFilter("[Slug] IS NOT NULL AND [Deleted] = 0");
        b.HasQueryFilter(x => !x.Deleted);
    }
}

public class ProductTagMappingConfiguration : IEntityTypeConfiguration<ProductTagMapping>
{
    public void Configure(EntityTypeBuilder<ProductTagMapping> b)
    {
        b.ToTable("ProductTagMappings");
        b.HasKey(x => x.Id);

        b.HasOne(x => x.Product)
            .WithMany(p => p.Tags)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.ProductTag)
            .WithMany(t => t.ProductTags)
            .HasForeignKey(x => x.ProductTagId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasIndex(x => new { x.ProductId, x.ProductTagId }).IsUnique();
    }
}

public class ProductCategoryConfiguration : IEntityTypeConfiguration<ProductCategory>
{
    public void Configure(EntityTypeBuilder<ProductCategory> b)
    {
        b.ToTable("ProductCategories");
        b.HasKey(x => x.Id);

        b.HasOne(x => x.Product)
            .WithMany(p => p.ProductCategories)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Category)
            .WithMany(c => c.ProductCategories)
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasIndex(x => new { x.ProductId, x.CategoryId }).IsUnique();
        b.HasIndex(x => x.CategoryId);
    }
}

public class ProductAttributeMappingConfiguration : IEntityTypeConfiguration<ProductAttributeMapping>
{
    public void Configure(EntityTypeBuilder<ProductAttributeMapping> b)
    {
        b.ToTable("ProductAttributeMappings");
        b.HasKey(x => x.Id);

        b.HasOne(x => x.Product)
            .WithMany(p => p.AttributeMappings)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.ProductAttribute)
            .WithMany(a => a.ProductMappings)
            .HasForeignKey(x => x.ProductAttributeId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasIndex(x => new { x.ProductId, x.ProductAttributeId }).IsUnique();
    }
}

public class ProductVariantAttributeValueConfiguration : IEntityTypeConfiguration<ProductVariantAttributeValue>
{
    public void Configure(EntityTypeBuilder<ProductVariantAttributeValue> b)
    {
        b.ToTable("ProductVariantAttributeValues");
        b.HasKey(x => x.Id);

        b.HasOne(x => x.ProductVariant)
            .WithMany(v => v.AttributeValues)
            .HasForeignKey(x => x.ProductVariantId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.ProductAttributeValue)
            .WithMany()
            .HasForeignKey(x => x.ProductAttributeValueId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasIndex(x => new { x.ProductVariantId, x.ProductAttributeValueId }).IsUnique();
    }
}

public class ProductSpecificationValueConfiguration : IEntityTypeConfiguration<ProductSpecificationValue>
{
    public void Configure(EntityTypeBuilder<ProductSpecificationValue> b)
    {
        b.ToTable("ProductSpecificationValues");
        b.HasKey(x => x.Id);

        b.HasOne(x => x.Product)
            .WithMany(p => p.SpecificationValues)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.SpecificationAttributeOption)
            .WithMany(o => o.ProductValues)
            .HasForeignKey(x => x.SpecificationAttributeOptionId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasIndex(x => new { x.ProductId, x.SpecificationAttributeOptionId }).IsUnique();
        b.HasIndex(x => x.SpecificationAttributeOptionId);
    }
}

public class GroupedProductMemberConfiguration : IEntityTypeConfiguration<GroupedProductMember>
{
    public void Configure(EntityTypeBuilder<GroupedProductMember> b)
    {
        b.ToTable("GroupedProductMembers");
        b.HasKey(x => x.Id);

        b.HasOne(x => x.GroupedProduct)
            .WithMany(p => p.GroupedMembers)
            .HasForeignKey(x => x.GroupedProductId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.MemberProduct)
            .WithMany()
            .HasForeignKey(x => x.MemberProductId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasIndex(x => new { x.GroupedProductId, x.MemberProductId }).IsUnique();
    }
}

public class ProductDownloadConfiguration : IEntityTypeConfiguration<ProductDownload>
{
    public void Configure(EntityTypeBuilder<ProductDownload> b)
    {
        b.ToTable("ProductDownloads");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(300).IsRequired();
        b.Property(x => x.Url).HasMaxLength(1000);
        b.Property(x => x.FileKey).HasMaxLength(500);

        b.HasOne(x => x.Product)
            .WithMany(p => p.Downloads)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.ProductId);
    }
}
