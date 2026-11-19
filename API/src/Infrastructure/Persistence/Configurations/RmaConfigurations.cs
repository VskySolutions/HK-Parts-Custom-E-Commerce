using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Infrastructure.Persistence.Configurations;

public class RmaConfiguration : IEntityTypeConfiguration<Rma>
{
    public void Configure(EntityTypeBuilder<Rma> b)
    {
        b.ToTable("Rmas");
        b.HasKey(x => x.Id);
        b.Property(x => x.RmaNumber).HasMaxLength(64).IsRequired();
        b.Property(x => x.Reason).HasMaxLength(1024);
        b.Property(x => x.ResolutionNotes).HasMaxLength(1024);

        b.Property(x => x.Status).HasConversion<int>();
        b.Property(x => x.RequestedResolution).HasConversion<int>();
        b.Property(x => x.Resolution).HasConversion<int>();
        b.Property(x => x.ReturnMethod).HasConversion<int>();
        b.Property(x => x.Source).HasConversion<int>();
        b.Property(x => x.RefundMethod).HasConversion<int>();

        b.Property(x => x.FflDealerName).HasMaxLength(200);
        b.Property(x => x.FflNumber).HasMaxLength(50);
        b.Property(x => x.EligibilityOverrideReason).HasMaxLength(500);

        b.Property(x => x.LabelCarrier).HasMaxLength(50);
        b.Property(x => x.LabelService).HasMaxLength(100);
        b.Property(x => x.TrackingNumber).HasMaxLength(100);
        b.Property(x => x.LabelUrl).HasMaxLength(1000);
        b.Property(x => x.LabelCost).HasPrecision(18, 2);
        b.Property(x => x.ReturnInstructions).HasMaxLength(2000);

        b.Property(x => x.SubtotalRefund).HasPrecision(18, 2);
        b.Property(x => x.TaxRefund).HasPrecision(18, 2);
        b.Property(x => x.ShippingRefund).HasPrecision(18, 2);
        b.Property(x => x.RestockFee).HasPrecision(18, 2);
        b.Property(x => x.LabelDeduction).HasPrecision(18, 2);
        b.Property(x => x.Adjustment).HasPrecision(18, 2);
        b.Property(x => x.AdjustmentReason).HasMaxLength(500);
        b.Property(x => x.TotalRefund).HasPrecision(18, 2);
        b.Property(x => x.RefundReference).HasMaxLength(500);
        b.Property(x => x.RefundedAmount).HasPrecision(18, 2);
        b.Property(x => x.StoreCreditIssued).HasPrecision(18, 2);
        b.Property(x => x.StoreCreditCode).HasMaxLength(100);
        b.Property(x => x.LastRefundError).HasMaxLength(1000);

        // Two staff refunding / receiving the same RMA at once: the second save fails (RT-67).
        b.Property(x => x.RowVersion).IsRowVersion();

        // Order is a lookup here; avoid a second cascade path (Order already cascades its own children).
        b.HasOne(x => x.Order)
            .WithMany()
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasIndex(x => x.RmaNumber).IsUnique();
        b.HasIndex(x => x.OrderId);
        b.HasIndex(x => x.CustomerId);
        b.HasIndex(x => x.Status);
        b.HasIndex(x => x.LabelExpiresAtUtc);
        b.HasQueryFilter(x => !x.Deleted);
    }
}

public class RmaLineItemConfiguration : IEntityTypeConfiguration<RmaLineItem>
{
    public void Configure(EntityTypeBuilder<RmaLineItem> b)
    {
        b.ToTable("RmaLineItems");
        b.HasKey(x => x.Id);
        b.Property(x => x.ProductName).HasMaxLength(400);
        b.Property(x => x.Sku).HasMaxLength(400);
        b.Property(x => x.LineReason).HasMaxLength(512);
        b.Property(x => x.CustomerComment).HasMaxLength(1000);
        b.Property(x => x.DeductionReason).HasMaxLength(500);
        b.Property(x => x.ConditionReported).HasConversion<int?>();
        b.Property(x => x.ConditionReceived).HasConversion<int?>();
        b.Property(x => x.Disposition).HasConversion<int?>();
        b.Property(x => x.UnitRefund).HasPrecision(18, 2);
        b.Property(x => x.Deduction).HasPrecision(18, 2);
        b.Property(x => x.LineRefund).HasPrecision(18, 2);

        b.HasOne(x => x.Rma)
            .WithMany(r => r.Lines)
            .HasForeignKey(x => x.RmaId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.RmaId);
        b.HasIndex(x => x.OrderLineItemId);
    }
}

public class RmaReasonConfiguration : IEntityTypeConfiguration<RmaReason>
{
    public void Configure(EntityTypeBuilder<RmaReason> b)
    {
        b.ToTable("RmaReasons");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.HasIndex(x => x.SortOrder);
    }
}

public class RmaHistoryConfiguration : IEntityTypeConfiguration<RmaHistory>
{
    public void Configure(EntityTypeBuilder<RmaHistory> b)
    {
        b.ToTable("RmaHistory");
        b.HasKey(x => x.Id);
        b.Property(x => x.FromStatus).HasConversion<int?>();
        b.Property(x => x.ToStatus).HasConversion<int>();
        b.Property(x => x.ActorType).HasConversion<int>();
        b.Property(x => x.ActorName).HasMaxLength(256);
        b.Property(x => x.Note).HasMaxLength(2000);
        b.Property(x => x.IpAddress).HasMaxLength(45);

        b.HasOne(x => x.Rma)
            .WithMany(r => r.History)
            .HasForeignKey(x => x.RmaId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.RmaId, x.CreatedOnUtc });
    }
}

public class RmaMessageConfiguration : IEntityTypeConfiguration<RmaMessage>
{
    public void Configure(EntityTypeBuilder<RmaMessage> b)
    {
        b.ToTable("RmaMessages");
        b.HasKey(x => x.Id);
        b.Property(x => x.AuthorType).HasConversion<int>();
        b.Property(x => x.AuthorName).HasMaxLength(256);
        b.Property(x => x.Body).HasMaxLength(4000).IsRequired();

        b.HasOne(x => x.Rma)
            .WithMany(r => r.Messages)
            .HasForeignKey(x => x.RmaId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.RmaId, x.CreatedOnUtc });
    }
}

public class RmaAttachmentConfiguration : IEntityTypeConfiguration<RmaAttachment>
{
    public void Configure(EntityTypeBuilder<RmaAttachment> b)
    {
        b.ToTable("RmaAttachments");
        b.HasKey(x => x.Id);
        b.Property(x => x.StorageKey).HasMaxLength(500).IsRequired();
        b.Property(x => x.OriginalFileName).HasMaxLength(260).IsRequired();
        b.Property(x => x.MimeType).HasMaxLength(100).IsRequired();
        b.Property(x => x.UploadedByType).HasConversion<int>();

        b.HasOne(x => x.Rma)
            .WithMany(r => r.Attachments)
            .HasForeignKey(x => x.RmaId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.RmaId);
    }
}
