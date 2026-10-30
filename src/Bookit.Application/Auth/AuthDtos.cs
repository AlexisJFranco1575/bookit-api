using Bookit.Domain.Users;

namespace Bookit.Application.Auth;

public sealed record RegisterRequest(string? Email, string? FullName, string? Password);

public sealed record LoginRequest(string? Email, string? Password);

public sealed record UserResponse(Guid Id, string Email, string FullName, DateTimeOffset CreatedAt)
{
    public static UserResponse From(User user) => new(user.Id, user.Email, user.FullName, user.CreatedAt);
}

public sealed record LoginResponse(string AccessToken, string TokenType, DateTimeOffset ExpiresAt);