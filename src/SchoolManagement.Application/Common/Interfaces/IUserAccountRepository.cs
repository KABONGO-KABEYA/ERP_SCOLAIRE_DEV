namespace SchoolManagement.Application.Common.Interfaces;

using SchoolManagement.Domain.Entities.Security;

public interface IUserAccountRepository : IRepository<UserAccount>
{
    Task<UserAccount?> GetByUserNameAsync(Guid schoolId, string userName, CancellationToken cancellationToken = default);

    Task<UserAccount?> GetWithRolesAndPermissionsAsync(Guid userId, CancellationToken cancellationToken = default);
}

public interface IRefreshTokenRepository : IRepository<RefreshToken>
{
    Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken cancellationToken = default);

    Task RevokeAllForUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
