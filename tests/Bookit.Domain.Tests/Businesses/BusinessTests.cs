using Bookit.Domain.Businesses;
using Bookit.Domain.Common;
using Xunit;

namespace Bookit.Domain.Tests.Businesses;

public class BusinessTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Owner = Guid.NewGuid();

    [Fact]
    public void Creates_a_business_and_resolves_its_time_zone()
    {
        var business = Business.Create(Owner, "  Barber Shop ", "America/Guayaquil", Now);

        Assert.Equal("Barber Shop", business.Name);
        Assert.Equal(TimeSpan.FromHours(-5), business.TimeZone.BaseUtcOffset);
        Assert.Equal(Now, business.CreatedAt);
        Assert.NotEqual(Guid.Empty, business.Id);
        Assert.True(business.IsOwnedBy(Owner));
        Assert.False(business.IsOwnedBy(Guid.NewGuid()));
    }

    [Fact]
    public void Rejects_a_blank_name()
    {
        Assert.Throws<DomainException>(() => { _ = Business.Create(Owner, "   ", "America/Guayaquil", Now); });
    }

    [Fact]
    public void Rejects_a_missing_owner()
    {
        Assert.Throws<DomainException>(() => { _ = Business.Create(Guid.Empty, "Barber Shop", "America/Guayaquil", Now); });
    }

    [Theory]
    [InlineData("Mars/Olympus")]
    [InlineData("")]
    public void Rejects_an_unknown_time_zone(string zone)
    {
        Assert.Throws<DomainException>(() => { _ = Business.Create(Owner, "Barber Shop", zone, Now); });
    }
}