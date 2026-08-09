namespace SchoolManagement.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Domain.Entities.Security;

public sealed class UserAccountRepository : Repository<UserAccount>, IUserAccountRepository
{
    public UserAccountRepository(SchoolDbContext context) : base(context)
    {
    }

    public async Task<UserAccount?> GetByUserNameAsync(Guid schoolId, string userName, CancellationToken cancellationToken = default) =>
        await Context.UserAccounts
            .FirstOrDefaultAsync(u => u.SchoolId == schoolId && u.UserName == userName, cancellationToken);

    public async Task<UserAccount?> GetWithRolesAndPermissionsAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await Context.UserAccounts
            .IgnoreQueryFilters()
            .Include(u => u.Roles).ThenInclude(ur => ur.Role)
            .ThenInclude(r => r.Permissions).ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, cancellationToken);
}

public sealed class RefreshTokenRepository : Repository<RefreshToken>, IRefreshTokenRepository
{
    public RefreshTokenRepository(SchoolDbContext context) : base(context)
    {
    }

    public async Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken cancellationToken = default) =>
        await Context.RefreshTokens
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Token == token && !t.IsDeleted, cancellationToken);

    public async Task RevokeAllForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var tokens = await Context.RefreshTokens
            .IgnoreQueryFilters()
            .Where(t => t.UserId == userId && !t.IsRevoked && !t.IsDeleted)
            .ToListAsync(cancellationToken);

        foreach (var token in tokens)
        {
            token.IsRevoked = true;
            token.RevokedAt = DateTime.UtcNow;
        }
    }
}
