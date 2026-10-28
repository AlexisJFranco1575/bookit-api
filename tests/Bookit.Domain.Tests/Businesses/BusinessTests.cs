using Bookit.Domain.Businesses;
using Bookit.Domain.Common;
using Xunit;

namespace Bookit.Domain.Tests.Businesses;

public class BusinessTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Creates_a_business_and_resolves_its_time_zone()
    {
        var business = Business.Create("  Barber Shop ", "America/Guayaquil", Now);

        Assert.Equal("Barber Shop", business.Name);
        Assert.Equal(TimeSpan.FromHours(-5), business.TimeZone.BaseUtcOffset);
        Assert.Equal(Now, business.CreatedAt);
        Assert.NotEqual(Guid.Empty, business.Id);
    }

    [Fact]
    public void Rejects_a_blank_name()
    {
        Assert.Throws<DomainException>(() => { _ = Business.Create("   ", "America/Guayaquil", Now); });
    }

    [Theory]
    [InlineData("Mars/Olympus")]
    [InlineData("")]
    public void Rejects_an_unknown_time_zone(string zone)
    {
        Assert.Throws<DomainException>(() => { _ = Business.Create("Barber Shop", zone, Now); });
    }
}
