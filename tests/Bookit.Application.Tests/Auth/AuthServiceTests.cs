#pragma warning disable xUnit1051

using Bookit.Application.Auth;
using Bookit.Application.Common;
using Bookit.Domain.Common;
using Xunit;

namespace Bookit.Application.Tests;

public class AuthServiceTests
{
    private const string StrongPassword = "Str0ng_Passw0rd!";

    private readonly FakeUserRepository _users = new();
    private readonly AuthService _auth;

    public AuthServiceTests()
    {
        _auth = new AuthService(
            _users,
            new FakePasswordHasher(),
            new FakeTokenService(),
            new FixedTimeProvider(BookingWorld.Now));
    }

    private Task<UserResponse> RegisterAsync(string email = "ana@example.com", string password = StrongPassword) =>
        _auth.RegisterAsync(new RegisterRequest(email, "Ana Perez", password), CancellationToken.None);

    [Fact]
    public async Task Registers_a_user_and_stores_only_the_password_hash()
    {
        var response = await RegisterAsync();

        Assert.Equal("ana@example.com", response.Email);
        Assert.Single(_users.Items);
        Assert.Equal($"hashed::{StrongPassword}", _users.Items[0].PasswordHash);
        Assert.DoesNotContain(
            typeof(UserResponse).GetProperties().Select(property => property.Name),
            name => name.Contains("Password", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Rejects_a_second_account_with_the_same_email_whatever_its_case()
    {
        await RegisterAsync("ana@example.com");

        await Assert.ThrowsAsync<ConflictException>(() => RegisterAsync("ANA@Example.com"));
    }

    [Theory]
    [InlineData("short1")]
    [InlineData("onlylettersnodigits")]
    [InlineData("12345678901234")]
    public async Task Rejects_a_weak_password(string password)
    {
        await Assert.ThrowsAsync<DomainException>(() => RegisterAsync(password: password));

        Assert.Empty(_users.Items);
    }

    [Fact]
    public async Task Logs_in_with_the_right_password()
    {
        await RegisterAsync();

        var response = await _auth.LoginAsync(
            new LoginRequest("  Ana@Example.com ", StrongPassword),
            CancellationToken.None);

        Assert.Equal("Bearer", response.TokenType);
        Assert.Equal("token-for-ana@example.com", response.AccessToken);
    }

    [Fact]
    public async Task Rejects_a_wrong_password()
    {
        await RegisterAsync();

        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            _auth.LoginAsync(new LoginRequest("ana@example.com", "Wrong_Passw0rd!"), CancellationToken.None));
    }

    [Fact]
    public async Task Rejects_an_unknown_email_with_the_same_error_as_a_wrong_password()
    {
        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            _auth.LoginAsync(new LoginRequest("nobody@example.com", StrongPassword), CancellationToken.None));
    }
}