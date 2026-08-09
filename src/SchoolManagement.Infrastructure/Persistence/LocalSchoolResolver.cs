namespace SchoolManagement.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

/// <summary>
/// Déploiement local : une base = un établissement principal (premier actif).
/// </summary>
internal static class LocalSchoolResolver
{
    internal static async Task<Guid?> TryResolvePrimarySchoolIdAsync(
        SchoolDbContext db,
        CancellationToken cancellationToken = default)
    {
        db.IgnoreSchoolScope = true;
        try
        {
            return await db.Schools
                .AsNoTracking()
                .Where(s => s.IsActive)
                .OrderBy(s => s.CreatedAt)
                .Select(s => (Guid?)s.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }
        finally
        {
            db.IgnoreSchoolScope = false;
        }
    }
}
