using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>
/// Product Q&amp;A (WO-58). The FK to Product is a NoAction lookup (questions are soft-deleted, never
/// cascaded when a product is removed); a <c>(ProductId, Status)</c> index serves both the storefront
/// "approved questions for this product" read and the admin moderation queue.
/// </summary>
public class ProductQuestionConfiguration : IEntityTypeConfiguration<ProductQuestion>
{
    public void Configure(EntityTypeBuilder<ProductQuestion> b)
    {
        b.ToTable("ProductQuestions");
        b.HasKey(x => x.Id);
        b.Property(x => x.AskerName).HasMaxLength(200).IsRequired();
        b.Property(x => x.AskerEmail).HasMaxLength(256);
        b.Property(x => x.QuestionText).HasMaxLength(2000).IsRequired();
        b.Property(x => x.AnswerText).HasMaxLength(4000);
        b.Property(x => x.Status).HasConversion<int>();

        b.HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasIndex(x => new { x.ProductId, x.Status });
        b.HasQueryFilter(x => !x.Deleted);
    }
}
