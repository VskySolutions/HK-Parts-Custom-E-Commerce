using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Infrastructure.Persistence.Configurations;

public class WebhookSubscriptionConfiguration : IEntityTypeConfiguration<WebhookSubscription>
{
    public void Configure(EntityTypeBuilder<WebhookSubscription> b)
    {
        b.ToTable("WebhookSubscriptions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Url).HasMaxLength(2048).IsRequired();
        b.Property(x => x.Secret).HasMaxLength(256).IsRequired();
        b.Property(x => x.Description).HasMaxLength(512);
        b.HasQueryFilter(x => !x.Deleted);
    }
}

public class WebhookSubscriptionEventConfiguration : IEntityTypeConfiguration<WebhookSubscriptionEvent>
{
    public void Configure(EntityTypeBuilder<WebhookSubscriptionEvent> b)
    {
        b.ToTable("WebhookSubscriptionEvents");
        b.HasKey(x => x.Id);
        b.Property(x => x.EventType).HasMaxLength(128).IsRequired();

        b.HasOne(x => x.Subscription)
            .WithMany(s => s.Events)
            .HasForeignKey(x => x.SubscriptionId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.SubscriptionId, x.EventType });
        b.HasIndex(x => x.EventType);
    }
}

public class WebhookDeliveryConfiguration : IEntityTypeConfiguration<WebhookDelivery>
{
    public void Configure(EntityTypeBuilder<WebhookDelivery> b)
    {
        b.ToTable("WebhookDeliveries");
        b.HasKey(x => x.Id);
        b.Property(x => x.EventType).HasMaxLength(128).IsRequired();
        b.Property(x => x.LastResponseBody).HasMaxLength(4000);

        // Keep delivery history even if the subscription is later removed.
        b.HasOne(x => x.Subscription)
            .WithMany()
            .HasForeignKey(x => x.SubscriptionId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasIndex(x => x.SubscriptionId);
        b.HasIndex(x => new { x.Status, x.NextAttemptOnUtc });
    }
}
