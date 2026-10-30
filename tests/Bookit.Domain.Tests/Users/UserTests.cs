using Bookit.Domain.Common;
using Bookit.Domain.Users;
using Xunit;

namespace Bookit.Domain.Tests.Users;

public class UserTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Creates_a_user_and_normalizes_the_data()
    {
        var user = User.Create(" Ana@Example.COM ", "  Ana Perez ", "some-hash", Now);

        Assert.Equal("ana@example.com", user.Email);
        Assert.Equal("Ana Perez", user.FullName);
        Assert.Equal("some-hash", user.PasswordHash);
        Assert.Equal(Now, user.CreatedAt);
        Assert.NotEqual(Guid.Empty, user.Id);
    }

    [Fact]
    public void Rejects_an_invalid_email()
    {
        Assert.Throws<DomainException>(() => { _ = User.Create("not-an-email", "Ana Perez", "some-hash", Now); });
    }

    [Fact]
    public void Rejects_a_blank_name()
    {
        Assert.Throws<DomainException>(() => { _ = User.Create("ana@example.com", "  ", "some-hash", Now); });
    }

    [Fact]
    public void Rejects_a_missing_password_hash()
    {
        Assert.Throws<DomainException>(() => { _ = User.Create("ana@example.com", "Ana Perez", " ", Now); });
    }
}