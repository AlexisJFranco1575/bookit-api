using Bookit.Application.Common;
using Bookit.Domain.Common;
using Bookit.Domain.Users;

namespace Bookit.Application.Auth;

public sealed class AuthService(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    TimeProvider clock)
{
    public const int MinPasswordLength = 10;
    public const int MaxPasswordLength = 128;

    public async Task<UserResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var password = ValidatePassword(request.Password);
        var email = User.NormalizeEmail(request.Email);

        if (await users.GetByEmailAsync(email, cancellationToken) is not null)
        {
            throw new ConflictException("An account with this email already exists.");
        }

        var user = User.Create(email, request.FullName, passwordHasher.Hash(password), clock.GetUtcNow());

        await users.AddAsync(user, cancellationToken);

        return UserResponse.From(user);
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email?.Trim().ToLowerInvariant() ?? string.Empty;
        var user = email.Length == 0 ? null : await users.GetByEmailAsync(email, cancellationToken);

        if (user is null)
        {
            // Do the same amount of hashing work as a real login, so the response time
            // does not reveal whether the account exists.
            passwordHasher.Hash(request.Password ?? string.Empty);
            throw new InvalidCredentialsException();
        }

        if (string.IsNullOrEmpty(request.Password) || !passwordHasher.Verify(user.PasswordHash, request.Password))
        {
            throw new InvalidCredentialsException();
        }

        var token = tokenService.CreateAccessToken(user);

        return new LoginResponse(token.Value, "Bearer", token.ExpiresAt);
    }

    private static string ValidatePassword(string? password)
    {
        if (password is null
            || password.Length is < MinPasswordLength or > MaxPasswordLength
            || !password.Any(char.IsLetter)
            || !password.Any(char.IsDigit))
        {
            throw new DomainException(
                $"The password must have {MinPasswordLength} to {MaxPasswordLength} characters, with at least one letter and one digit.");
        }

        return password;
    }
}