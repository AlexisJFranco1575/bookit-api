using Bookit.Application.Auth;
using Bookit.Application.Common;
using Bookit.Domain.Users;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Bookit.Infrastructure.Persistence;

public sealed class UserRepository(BookitDbContext db) : IUserRepository
{
    // SQL Server error numbers for "duplicate key" (unique index / unique constraint).
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;

    public async Task AddAsync(User user, CancellationToken cancellationToken)
    {
        db.Users.Add(user);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqlException { Number: UniqueIndexViolation or UniqueConstraintViolation })
        {
            // Two registrations with the same email arrived together; the unique index let only one through.
            db.Entry(user).State = EntityState.Detached;
            throw new ConflictException("An account with this email already exists.");
        }
    }

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken) =>
        db.Users.AsNoTracking().FirstOrDefaultAsync(user => user.Email == email, cancellationToken);
}