using Bookit.Domain.Appointments;
using Bookit.Domain.Businesses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookit.Infrastructure.Persistence.Configurations;

public sealed class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.ToTable("Appointments");

        builder.HasKey(appointment => appointment.Id);
        builder.Property(appointment => appointment.Id).ValueGeneratedNever();

        builder.Property(appointment => appointment.CustomerName)
            .HasMaxLength(Appointment.MaxNameLength)
            .IsRequired();

        builder.Property(appointment => appointment.CustomerEmail)
            .HasMaxLength(Appointment.MaxEmailLength)
            .IsRequired();

        builder.Property(appointment => appointment.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Ignore(appointment => appointment.Range);

        builder.HasOne<Provider>()
            .WithMany()
            .HasForeignKey(appointment => appointment.ProviderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Service>()
            .WithMany()
            .HasForeignKey(appointment => appointment.ServiceId)
            .OnDelete(DeleteBehavior.Restrict);

        // Safety net behind the per-provider lock: two booked appointments of the same provider
        // can never start at the same instant, even if some code path forgot to take the lock.
        builder.HasIndex(appointment => new { appointment.ProviderId, appointment.StartsAt })
            .IsUnique()
            .HasFilter("[Status] = 'Booked'");
    }
}
