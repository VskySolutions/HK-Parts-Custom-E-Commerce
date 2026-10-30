using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Infrastructure.Persistence.Configurations;

public class EmailTemplateConfiguration : IEntityTypeConfiguration<EmailTemplate>
{
    public void Configure(EntityTypeBuilder<EmailTemplate> b)
    {
        b.ToTable("EmailTemplates");
        b.HasKey(x => x.Id);
        b.Property(x => x.TemplateKey).HasMaxLength(100).IsRequired();
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.SubjectLine).HasMaxLength(512).IsRequired();
        b.Property(x => x.Description).HasMaxLength(500);

        b.HasIndex(x => x.TemplateKey).IsUnique();
    }
}

public class EmailQueueConfiguration : IEntityTypeConfiguration<EmailQueue>
{
    public void Configure(EntityTypeBuilder<EmailQueue> b)
    {
        b.ToTable("EmailQueue");
        b.HasKey(x => x.Id);
        b.Property(x => x.TemplateKey).HasMaxLength(100).IsRequired();
        b.Property(x => x.RecipientEmail).HasMaxLength(255).IsRequired();
        b.Property(x => x.RecipientName).HasMaxLength(200);
        b.Property(x => x.RenderedSubject).HasMaxLength(1024).IsRequired();

        // Supports the dispatch worker's "pending + due" polling query.
        b.HasIndex(x => new { x.Status, x.NextAttemptOnUtc });
    }
}

public class MarketingSuppressionConfiguration : IEntityTypeConfiguration<MarketingSuppression>
{
    public void Configure(EntityTypeBuilder<MarketingSuppression> b)
    {
        b.ToTable("MarketingSuppressions");
        b.HasKey(x => x.Id);
        b.Property(x => x.RecipientEmail).HasMaxLength(255).IsRequired();
        b.Property(x => x.Source).HasMaxLength(100);

        b.HasIndex(x => x.RecipientEmail).IsUnique();
    }
}
