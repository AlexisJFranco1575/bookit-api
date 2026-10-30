using Bookit.Domain.Businesses;
using Bookit.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookit.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(user => user.Id);
        builder.Property(user => user.Id).ValueGeneratedNever();

        builder.Property(user => user.Email).HasMaxLength(User.MaxEmailLength).IsRequired();
        builder.Property(user => user.FullName).HasMaxLength(User.MaxNameLength).IsRequired();
        builder.Property(user => user.PasswordHash).HasMaxLength(256).IsRequired();

        // Two accounts can never share an email, even if two registrations arrive at the same instant.
        builder.HasIndex(user => user.Email).IsUnique();

        // Every business belongs to a user. A user with businesses cannot be deleted by accident.
        builder.HasMany<Business>()
            .WithOne()
            .HasForeignKey(business => business.OwnerId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);
    }
}