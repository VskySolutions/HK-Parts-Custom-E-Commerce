using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Infrastructure.Persistence.Configurations;

// Content & Marketing (Phase 5). Every table is CMS-prefixed per the Content and Marketing blueprint
// key contract. Media / Category / Product / Collection references are NoAction lookups; only an owned
// child (collection items, pinned products) cascades from its single parent.

public class CMSPageGroupConfiguration : IEntityTypeConfiguration<CMSPageGroup>
{
    public void Configure(EntityTypeBuilder<CMSPageGroup> b)
    {
        b.ToTable("CMSPageGroups");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Slug).HasMaxLength(220);
        b.HasIndex(x => x.Slug).IsUnique().HasFilter("[Slug] IS NOT NULL AND [Deleted] = 0");
        b.HasQueryFilter(x => !x.Deleted);
    }
}

public class CMSPageConfiguration : IEntityTypeConfiguration<CMSPage>
{
    public void Configure(EntityTypeBuilder<CMSPage> b)
    {
        b.ToTable("CMSPages");
        b.HasKey(x => x.Id);
        b.Property(x => x.Title).HasMaxLength(300).IsRequired();
        b.Property(x => x.Slug).HasMaxLength(300).IsRequired();
        b.Property(x => x.MetaTitle).HasMaxLength(300);
        b.Property(x => x.MetaDescription).HasMaxLength(500);
        b.Property(x => x.MetaKeywords).HasMaxLength(500);
        b.Property(x => x.CanonicalUrl).HasMaxLength(500);
        b.Property(x => x.Status).HasConversion<int>();
        b.Property(x => x.ShowInFooter).HasDefaultValue(true);
        b.HasIndex(x => new { x.Status, x.DisplayOrder });

        b.HasOne(x => x.PageGroup)
            .WithMany(g => g.Pages)
            .HasForeignKey(x => x.PageGroupId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasIndex(x => x.Slug).IsUnique().HasFilter("[Deleted] = 0");
        b.HasIndex(x => new { x.PageGroupId, x.DisplayOrder });
        b.HasQueryFilter(x => !x.Deleted);
    }
}

public class CMSBlogPostConfiguration : IEntityTypeConfiguration<CMSBlogPost>
{
    public void Configure(EntityTypeBuilder<CMSBlogPost> b)
    {
        b.ToTable("CMSBlogPosts");
        b.HasKey(x => x.Id);
        b.Property(x => x.Title).HasMaxLength(300).IsRequired();
        b.Property(x => x.Slug).HasMaxLength(300).IsRequired();
        b.Property(x => x.Summary).HasMaxLength(1000);
        b.Property(x => x.Author).HasMaxLength(200);
        b.Property(x => x.Tags).HasMaxLength(1000);
        b.Property(x => x.MetaTitle).HasMaxLength(300);
        b.Property(x => x.MetaDescription).HasMaxLength(500);
        b.Property(x => x.Status).HasConversion<int>();

        b.HasOne(x => x.FeaturedImageMedia)
            .WithMany()
            .HasForeignKey(x => x.FeaturedImageMediaId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasIndex(x => x.Slug).IsUnique().HasFilter("[Deleted] = 0");
        b.HasIndex(x => new { x.Status, x.PublishedOnUtc });
        b.HasQueryFilter(x => !x.Deleted);
    }
}

public class CMSBannerConfiguration : IEntityTypeConfiguration<CMSBanner>
{
    public void Configure(EntityTypeBuilder<CMSBanner> b)
    {
        b.ToTable("CMSBanners");
        b.HasKey(x => x.Id);
        b.Property(x => x.Title).HasMaxLength(300).IsRequired();
        b.Property(x => x.Subtitle).HasMaxLength(500);
        b.Property(x => x.LinkUrl).HasMaxLength(1000);
        b.Property(x => x.CtaLabel).HasMaxLength(120);
        b.Property(x => x.DisplayLocation).HasMaxLength(100).IsRequired();

        b.HasOne(x => x.ImageMedia)
            .WithMany()
            .HasForeignKey(x => x.ImageMediaId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasIndex(x => new { x.DisplayLocation, x.DisplayOrder });
        b.HasQueryFilter(x => !x.Deleted);
    }
}

public class CMSProductCollectionConfiguration : IEntityTypeConfiguration<CMSProductCollection>
{
    public void Configure(EntityTypeBuilder<CMSProductCollection> b)
    {
        b.ToTable("CMSProductCollections");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasMaxLength(1000);
        b.Property(x => x.Slug).HasMaxLength(220);
        b.HasIndex(x => x.Slug).IsUnique().HasFilter("[Slug] IS NOT NULL AND [Deleted] = 0");
        b.HasQueryFilter(x => !x.Deleted);
    }
}

public class CMSProductCollectionItemConfiguration : IEntityTypeConfiguration<CMSProductCollectionItem>
{
    public void Configure(EntityTypeBuilder<CMSProductCollectionItem> b)
    {
        b.ToTable("CMSProductCollectionItems");
        b.HasKey(x => x.Id);

        b.HasOne(x => x.Collection)
            .WithMany(c => c.Items)
            .HasForeignKey(x => x.CollectionId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasIndex(x => new { x.CollectionId, x.DisplayOrder });
        b.HasIndex(x => new { x.CollectionId, x.ProductId }).IsUnique();
    }
}

public class CMSHomePageSectionConfiguration : IEntityTypeConfiguration<CMSHomePageSection>
{
    public void Configure(EntityTypeBuilder<CMSHomePageSection> b)
    {
        b.ToTable("CMSHomePageSections");
        b.HasKey(x => x.Id);
        b.Property(x => x.SectionType).HasConversion<int>();
        b.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
        b.HasIndex(x => x.DisplayOrder);
        b.HasQueryFilter(x => !x.Deleted);
    }
}

public class CMSCategoryPageConfigConfiguration : IEntityTypeConfiguration<CMSCategoryPageConfig>
{
    public void Configure(EntityTypeBuilder<CMSCategoryPageConfig> b)
    {
        b.ToTable("CMSCategoryPageConfigs");
        b.HasKey(x => x.Id);

        b.HasOne(x => x.Category)
            .WithMany()
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasOne(x => x.BannerMedia)
            .WithMany()
            .HasForeignKey(x => x.BannerMediaId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasOne(x => x.YmalCollection)
            .WithMany()
            .HasForeignKey(x => x.YmalCollectionId)
            .OnDelete(DeleteBehavior.NoAction);

        // One config per category.
        b.HasIndex(x => x.CategoryId).IsUnique();
    }
}

public class CMSCategoryPinnedProductConfiguration : IEntityTypeConfiguration<CMSCategoryPinnedProduct>
{
    public void Configure(EntityTypeBuilder<CMSCategoryPinnedProduct> b)
    {
        b.ToTable("CMSCategoryPinnedProducts");
        b.HasKey(x => x.Id);

        b.HasOne(x => x.CategoryPageConfig)
            .WithMany(c => c.PinnedProducts)
            .HasForeignKey(x => x.CategoryPageConfigId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasIndex(x => new { x.CategoryPageConfigId, x.DisplayOrder });
        b.HasIndex(x => new { x.CategoryPageConfigId, x.ProductId }).IsUnique();
    }
}

public class CMSSearchPageContentConfiguration : IEntityTypeConfiguration<CMSSearchPageContent>
{
    public void Configure(EntityTypeBuilder<CMSSearchPageContent> b)
    {
        b.ToTable("CMSSearchPageContent");
        b.HasKey(x => x.Id);
        b.Property(x => x.Heading).HasMaxLength(300);
        b.Property(x => x.PlaceholderText).HasMaxLength(300);
        b.Property(x => x.ResultsCountLabel).HasMaxLength(300);

        b.HasOne(x => x.NoResultsBanner)
            .WithMany()
            .HasForeignKey(x => x.NoResultsBannerId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasOne(x => x.NoResultsCollection)
            .WithMany()
            .HasForeignKey(x => x.NoResultsCollectionId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}

public class CMSNewsletterSubscriptionConfiguration : IEntityTypeConfiguration<CMSNewsletterSubscription>
{
    public void Configure(EntityTypeBuilder<CMSNewsletterSubscription> b)
    {
        b.ToTable("CMSNewsletterSubscriptions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Email).HasMaxLength(256).IsRequired();
        b.Property(x => x.Status).HasConversion<int>();
        b.Property(x => x.Source).HasMaxLength(100);
        b.HasIndex(x => x.Email).IsUnique();
    }
}
