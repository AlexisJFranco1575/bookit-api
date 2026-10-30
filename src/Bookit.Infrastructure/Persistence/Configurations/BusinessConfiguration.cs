using Bookit.Domain.Businesses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookit.Infrastructure.Persistence.Configurations;

public sealed class BusinessConfiguration : IEntityTypeConfiguration<Business>
{
    public void Configure(EntityTypeBuilder<Business> builder)
    {
        builder.ToTable("Businesses");

        builder.HasKey(business => business.Id);
        builder.Property(business => business.Id).ValueGeneratedNever();

        // The foreign key to Users is declared in UserConfiguration; this makes the column explicit and indexed.
        builder.Property(business => business.OwnerId).IsRequired();
        builder.HasIndex(business => business.OwnerId);

        builder.Property(business => business.Name).HasMaxLength(Business.MaxNameLength).IsRequired();
        builder.Property(business => business.TimeZoneId).HasMaxLength(64).IsRequired();

        builder.Ignore(business => business.TimeZone);
    }
}