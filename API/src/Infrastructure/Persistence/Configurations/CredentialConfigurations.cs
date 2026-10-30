using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Infrastructure.Persistence.Configurations;

public class SmtpAccountConfiguration : IEntityTypeConfiguration<SmtpAccount>
{
    public void Configure(EntityTypeBuilder<SmtpAccount> b)
    {
        b.ToTable("SmtpAccounts");
        b.HasKey(x => x.Id);
        b.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
        b.Property(x => x.Host).HasMaxLength(255).IsRequired();
        b.Property(x => x.Username).HasMaxLength(255);
        b.Property(x => x.FromName).HasMaxLength(200).IsRequired();
        b.Property(x => x.FromEmail).HasMaxLength(255).IsRequired();
    }
}
