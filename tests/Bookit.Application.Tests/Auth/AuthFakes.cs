using Bookit.Application.Auth;
using Bookit.Domain.Users;

namespace Bookit.Application.Tests;

internal sealed class FakeUserRepository : IUserRepository
{
    public List<User> Items { get; } = [];

    public Task AddAsync(User user, CancellationToken cancellationToken)
    {
        Items.Add(user);
        return Task.CompletedTask;
    }

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(user =>
            string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase)));
}

/// <summary>Deterministic stand-in for the real hasher; it is NOT secure and only used in tests.</summary>
internal sealed class FakePasswordHasher : IPasswordHasher
{
    public string Hash(string password) => $"hashed::{password}";

    public bool Verify(string passwordHash, string password) => passwordHash == $"hashed::{password}";
}

internal sealed class FakeTokenService : ITokenService
{
    public AccessToken CreateAccessToken(User user) =>
        new($"token-for-{user.Email}", DateTimeOffset.UnixEpoch.AddHours(1));
}