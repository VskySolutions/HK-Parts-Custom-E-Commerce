using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Infrastructure.Persistence.Configurations;

public class ApplicationLogConfiguration : IEntityTypeConfiguration<ApplicationLog>
{
    public void Configure(EntityTypeBuilder<ApplicationLog> b)
    {
        // Column layout matches the Serilog.Sinks.MSSqlServer standard + additional columns so the
        // sink writes directly into this EF-managed table (see WO-6).
        b.ToTable("ApplicationLogs");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.Level).HasMaxLength(128).IsRequired();
        b.Property(x => x.Properties).HasColumnType("xml"); // Serilog MSSQL sink writes XML properties.
        b.Property(x => x.CorrelationId).HasMaxLength(64);
        b.Property(x => x.Source).HasMaxLength(50);
        b.Property(x => x.Route).HasMaxLength(500);

        b.HasIndex(x => x.CorrelationId);
        b.HasIndex(x => x.TimeStamp);
    }
}

public class AuditTrailConfiguration : IEntityTypeConfiguration<AuditTrail>
{
    public void Configure(EntityTypeBuilder<AuditTrail> b)
    {
        b.ToTable("AuditTrails");
        b.HasKey(x => x.Id);
        b.Property(x => x.ActorName).HasMaxLength(200);
        b.Property(x => x.Action).HasMaxLength(100).IsRequired();
        b.Property(x => x.EntityType).HasMaxLength(100);
        b.Property(x => x.EntityId).HasMaxLength(100);
        b.Property(x => x.CorrelationId).HasMaxLength(64);
        b.Property(x => x.IpAddress).HasMaxLength(64);

        b.HasIndex(x => x.TimestampUtc);
        b.HasIndex(x => new { x.EntityType, x.EntityId });
    }
}

public class AdminAlertConfiguration : IEntityTypeConfiguration<AdminAlert>
{
    public void Configure(EntityTypeBuilder<AdminAlert> b)
    {
        b.ToTable("AdminAlerts");
        b.HasKey(x => x.Id);
        b.Property(x => x.AlertType).HasMaxLength(100).IsRequired();
        b.Property(x => x.Severity).HasMaxLength(20).IsRequired();
        b.Property(x => x.Title).HasMaxLength(300).IsRequired();
        b.Property(x => x.Source).HasMaxLength(100);
        b.Property(x => x.RelatedEntityType).HasMaxLength(100);
        b.Property(x => x.RelatedEntityId).HasMaxLength(100);

        b.HasIndex(x => new { x.IsResolved, x.CreatedOnUtc });
    }
}

public class BackgroundTaskLogConfiguration : IEntityTypeConfiguration<BackgroundTaskLog>
{
    public void Configure(EntityTypeBuilder<BackgroundTaskLog> b)
    {
        b.ToTable("BackgroundTaskLogs");
        b.HasKey(x => x.Id);
        b.Property(x => x.TaskName).HasMaxLength(200).IsRequired();
        b.Property(x => x.Status).HasMaxLength(50).IsRequired();

        b.HasIndex(x => new { x.TaskName, x.StartedOnUtc });
    }
}
