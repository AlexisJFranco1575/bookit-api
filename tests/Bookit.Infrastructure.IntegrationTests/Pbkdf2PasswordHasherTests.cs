using Bookit.Infrastructure.Security;
using Xunit;

namespace Bookit.Infrastructure.IntegrationTests;

public class Pbkdf2PasswordHasherTests
{
    private readonly Pbkdf2PasswordHasher _hasher = new();

    [Fact]
    public void A_hash_never_contains_the_password()
    {
        var hash = _hasher.Hash("Str0ng_Passw0rd!");

        Assert.DoesNotContain("Str0ng_Passw0rd!", hash);
        Assert.StartsWith("PBKDF2-SHA256$600000$", hash);
    }

    [Fact]
    public void The_same_password_hashes_differently_each_time_because_of_the_random_salt()
    {
        Assert.NotEqual(_hasher.Hash("Str0ng_Passw0rd!"), _hasher.Hash("Str0ng_Passw0rd!"));
    }

    [Fact]
    public void Verifies_the_right_password()
    {
        var hash = _hasher.Hash("Str0ng_Passw0rd!");

        Assert.True(_hasher.Verify(hash, "Str0ng_Passw0rd!"));
    }

    [Fact]
    public void Rejects_a_wrong_password()
    {
        var hash = _hasher.Hash("Str0ng_Passw0rd!");

        Assert.False(_hasher.Verify(hash, "Str0ng_Passw0rd?"));
        Assert.False(_hasher.Verify(hash, string.Empty));
    }

    [Theory]
    [InlineData("")]
    [InlineData("plain-text")]
    [InlineData("PBKDF2-SHA256$600000$not-base64$not-base64")]
    [InlineData("PBKDF2-SHA256$abc$AAAAAAAAAAAAAAAAAAAAAA==$AAAA")]
    [InlineData("OTHER$600000$AAAAAAAAAAAAAAAAAAAAAA==$AAAA")]
    public void A_damaged_or_foreign_hash_is_rejected_without_throwing(string hash)
    {
        Assert.False(_hasher.Verify(hash, "Str0ng_Passw0rd!"));
    }
}