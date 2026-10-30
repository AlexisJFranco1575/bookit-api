using System.Net.Mail;
using Bookit.Domain.Common;

namespace Bookit.Domain.Users;

/// <summary>A person who signs in to manage a business. Customers who book appointments do not need an account.</summary>
public sealed class User
{
    public const int MaxNameLength = 100;
    public const int MaxEmailLength = 254;

    private User()
    {
    }

    public Guid Id { get; private set; }

    public string Email { get; private set; } = string.Empty;

    public string FullName { get; private set; } = string.Empty;

    public string PasswordHash { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Trims and lower-cases an email and rejects anything that is not a plain address.</summary>
    public static string NormalizeEmail(string? email)
    {
        var value = email?.Trim().ToLowerInvariant() ?? string.Empty;

        if (value.Length is 0 or > MaxEmailLength
            || !MailAddress.TryCreate(value, out var parsed)
            || parsed.Address != value)
        {
            throw new DomainException("A valid email is required.");
        }

        return value;
    }

    public static User Create(string? email, string? fullName, string? passwordHash, DateTimeOffset now)
    {
        var cleanName = fullName?.Trim() ?? string.Empty;

        if (cleanName.Length is 0 or > MaxNameLength)
        {
            throw new DomainException($"The name is required and can have at most {MaxNameLength} characters.");
        }

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new DomainException("A password hash is required.");
        }

        return new User
        {
            Id = Guid.CreateVersion7(),
            Email = NormalizeEmail(email),
            FullName = cleanName,
            PasswordHash = passwordHash,
            CreatedAt = now
        };
    }
}