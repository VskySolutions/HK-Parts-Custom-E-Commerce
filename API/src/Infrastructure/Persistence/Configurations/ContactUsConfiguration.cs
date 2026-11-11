
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Infrastructure.Persistence.Configurations;

public class ContactUsConfiguration : IEntityTypeConfiguration<ContactUs>
{
    public void Configure(EntityTypeBuilder<ContactUs> b)
    {
        b.ToTable("ContactUsEnquiries");

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();

        b.Property(x => x.FirstName).HasMaxLength(200).IsRequired();
        b.Property(x => x.LastName).HasMaxLength(200).IsRequired();
        b.Property(x => x.Email).HasMaxLength(256).IsRequired();
        b.Property(x => x.PhoneNumber).HasMaxLength(50);
        b.Property(x => x.Comments).HasColumnType("nvarchar(max)");

        b.Property(x => x.Deleted).HasDefaultValue(false);

        b.Property(x => x.CreatedById);
        b.Property(x => x.CreatedOnUtc).HasColumnType("datetime2(7)").IsRequired();
        b.Property(x => x.UpdatedById);
        b.Property(x => x.UpdatedOnUtc).HasColumnType("datetime2(7)").IsRequired();
        b.Property(x => x.DeletedOnUtc).HasColumnType("datetime2(7)");
    }
}
