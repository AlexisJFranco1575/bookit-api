using Bookit.Domain.Businesses;
using Bookit.Domain.Common;
using Xunit;

namespace Bookit.Domain.Tests.Businesses;

public class ServiceTests
{
    [Fact]
    public void Creates_a_service_with_its_duration()
    {
        var service = Service.Create(Guid.NewGuid(), "  Haircut ", 30);

        Assert.Equal("Haircut", service.Name);
        Assert.Equal(TimeSpan.FromMinutes(30), service.Duration);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(481)]
    public void Rejects_a_duration_outside_the_allowed_range(int minutes)
    {
        Assert.Throws<DomainException>(() => { _ = Service.Create(Guid.NewGuid(), "Haircut", minutes); });
    }

    [Fact]
    public void Rejects_a_blank_name()
    {
        Assert.Throws<DomainException>(() => { _ = Service.Create(Guid.NewGuid(), " ", 30); });
    }
}
