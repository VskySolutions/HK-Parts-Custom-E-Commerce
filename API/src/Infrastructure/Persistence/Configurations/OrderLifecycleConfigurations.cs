using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Infrastructure.Persistence.Configurations;

public class OrderStatusHistoryConfiguration : IEntityTypeConfiguration<OrderStatusHistory>
{
    public void Configure(EntityTypeBuilder<OrderStatusHistory> b)
    {
        b.ToTable("OrderStatusHistory");
        b.HasKey(x => x.Id);
        b.Property(x => x.FromStatus).HasConversion<int>();
        b.Property(x => x.ToStatus).HasConversion<int>();
        b.Property(x => x.Note).HasMaxLength(1000);

        b.HasOne(x => x.Order)
            .WithMany(o => o.StatusHistory)
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.OrderId);
    }
}
