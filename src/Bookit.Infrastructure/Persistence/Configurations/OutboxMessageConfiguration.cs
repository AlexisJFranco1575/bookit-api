using Bookit.Domain.Appointments;
using Bookit.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookit.Infrastructure.Persistence.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");

        builder.HasKey(message => message.Id);
        builder.Property(message => message.Id).ValueGeneratedNever();

        // Stored as text so the table is readable and reordering an enum can never corrupt old rows.
        builder.Property(message => message.Type).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(message => message.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(message => message.Detail).HasMaxLength(OutboxMessage.MaxDetailLength);

        // The worker's question: "what is pending and due?"
        builder.HasIndex(message => new { message.Status, message.NextAttemptAt });

        // One confirmation, one cancellation notice and one reminder per appointment, even if two processes
        // try to create the same one at the same instant.
        builder.HasIndex(message => new { message.AppointmentId, message.Type }).IsUnique();

        builder.HasOne<Appointment>()
            .WithMany()
            .HasForeignKey(message => message.AppointmentId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);
    }
}