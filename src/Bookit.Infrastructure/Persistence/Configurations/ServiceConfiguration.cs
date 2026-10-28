using Bookit.Domain.Businesses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookit.Infrastructure.Persistence.Configurations;

public sealed class ServiceConfiguration : IEntityTypeConfiguration<Service>
{
    public void Configure(EntityTypeBuilder<Service> builder)
    {
        builder.ToTable("Services");

        builder.HasKey(service => service.Id);
        builder.Property(service => service.Id).ValueGeneratedNever();

        builder.Property(service => service.Name).HasMaxLength(Service.MaxNameLength).IsRequired();

        builder.Ignore(service => service.Duration);

        builder.HasOne<Business>()
            .WithMany()
            .HasForeignKey(service => service.BusinessId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(service => service.BusinessId);
    }
}
