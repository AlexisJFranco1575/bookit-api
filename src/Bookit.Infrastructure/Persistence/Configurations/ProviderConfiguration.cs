using Bookit.Domain.Businesses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookit.Infrastructure.Persistence.Configurations;

public sealed class ProviderConfiguration : IEntityTypeConfiguration<Provider>
{
    public void Configure(EntityTypeBuilder<Provider> builder)
    {
        builder.ToTable("Providers");

        builder.HasKey(provider => provider.Id);
        builder.Property(provider => provider.Id).ValueGeneratedNever();

        builder.Property(provider => provider.Name).HasMaxLength(Provider.MaxNameLength).IsRequired();

        builder.Ignore(provider => provider.Schedule);

        builder.HasOne<Business>()
            .WithMany()
            .HasForeignKey(provider => provider.BusinessId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(provider => provider.BusinessId);

        // The weekly schedule is stored as rows in their own table, owned by the provider.
        builder.OwnsMany(provider => provider.WorkingPeriods, periods =>
        {
            periods.ToTable("ProviderWorkingPeriods");
            periods.WithOwner().HasForeignKey("ProviderId");

            periods.Property<int>("Id").ValueGeneratedOnAdd();
            periods.HasKey("Id");

            periods.Property(period => period.Day).IsRequired();
            periods.Property(period => period.Start).IsRequired();
            periods.Property(period => period.End).IsRequired();
        });

        builder.Navigation(provider => provider.WorkingPeriods).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
