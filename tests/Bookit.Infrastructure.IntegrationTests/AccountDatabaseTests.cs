#pragma warning disable xUnit1051

using Bookit.Application.Common;
using Bookit.Domain.Businesses;
using Bookit.Domain.Users;
using Bookit.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bookit.Infrastructure.IntegrationTests;

public class AccountDatabaseTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);

    private static string UniqueEmail() => $"user-{Guid.NewGuid():N}@example.com";

    private static User NewUser(string email) => User.Create(email, "Test User", "not-a-real-hash", Now);

    private async Task<User> AddUserAsync(string? email = null)
    {
        await using var db = fixture.CreateContext();

        var user = NewUser(email ?? UniqueEmail());
        await new UserRepository(db).AddAsync(user, CancellationToken.None);

        return user;
    }

    [Fact]
    public async Task A_saved_user_can_be_found_by_email()
    {
        var email = UniqueEmail();
        var saved = await AddUserAsync(email);

        await using var db = fixture.CreateContext();
        var found = await new UserRepository(db).GetByEmailAsync(email, CancellationToken.None);

        Assert.NotNull(found);
        Assert.Equal(saved.Id, found.Id);
        Assert.Equal("Test User", found.FullName);
        Assert.Equal("not-a-real-hash", found.PasswordHash);
    }

    [Fact]
    public async Task An_unknown_email_finds_nobody()
    {
        await using var db = fixture.CreateContext();

        var found = await new UserRepository(db).GetByEmailAsync(UniqueEmail(), CancellationToken.None);

        Assert.Null(found);
    }

    [Fact]
    public async Task Registering_the_same_email_twice_fails_with_a_conflict()
    {
        var email = UniqueEmail();
        await AddUserAsync(email);

        await Assert.ThrowsAsync<ConflictException>(() => AddUserAsync(email));
    }

    [Fact]
    public async Task Twenty_simultaneous_registrations_with_the_same_email_create_exactly_one_account()
    {
        var email = UniqueEmail();

        var results = await Task.WhenAll(
            Enumerable.Range(0, 20).Select(async _ =>
            {
                try
                {
                    await AddUserAsync(email);
                    return true;
                }
                catch (ConflictException)
                {
                    return false;
                }
            }));

        Assert.Equal(1, results.Count(created => created));

        await using var check = fixture.CreateContext();
        Assert.Equal(1, await check.Users.CountAsync(user => user.Email == email));
    }

    [Fact]
    public async Task Listing_businesses_returns_only_those_of_the_owner()
    {
        var owner = await AddUserAsync();
        var other = await AddUserAsync();

        await using (var db = fixture.CreateContext())
        {
            var repository = new BusinessRepository(db);

            await repository.AddAsync(
                Business.Create(owner.Id, "Zeta Barbershop", "America/Guayaquil", Now), CancellationToken.None);
            await repository.AddAsync(
                Business.Create(owner.Id, "Alfa Clinic", "America/Guayaquil", Now), CancellationToken.None);
            await repository.AddAsync(
                Business.Create(other.Id, "Someone Else", "America/Guayaquil", Now), CancellationToken.None);
        }

        await using var check = fixture.CreateContext();
        var mine = await new BusinessRepository(check).ListByOwnerAsync(owner.Id, CancellationToken.None);

        Assert.Equal(["Alfa Clinic", "Zeta Barbershop"], mine.Select(business => business.Name));
        Assert.All(mine, business => Assert.True(business.IsOwnedBy(owner.Id)));
    }

    [Fact]
    public async Task A_business_cannot_belong_to_an_owner_that_does_not_exist()
    {
        await using var db = fixture.CreateContext();

        var orphan = Business.Create(Guid.NewGuid(), "Orphan Shop", "America/Guayaquil", Now);

        await Assert.ThrowsAsync<DbUpdateException>(() => new BusinessRepository(db).AddAsync(orphan, CancellationToken.None));
    }
}