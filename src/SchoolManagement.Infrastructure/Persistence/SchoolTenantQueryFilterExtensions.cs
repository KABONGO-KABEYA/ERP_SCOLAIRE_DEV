namespace SchoolManagement.Infrastructure.Persistence;

using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using SchoolManagement.Domain.Common;

internal static class SchoolTenantQueryFilterExtensions
{
    internal static void ApplyTenantAndSoftDeleteQueryFilters(this ModelBuilder modelBuilder, SchoolDbContext context)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(AuditableEntity).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            var schoolIdProperty = entityType.FindProperty("SchoolId");
            if (schoolIdProperty?.ClrType == typeof(Guid))
            {
                ApplyCombinedFilter(modelBuilder, entityType.ClrType, context, hasSchoolScope: true);
            }
            else
            {
                ApplyCombinedFilter(modelBuilder, entityType.ClrType, context, hasSchoolScope: false);
            }
        }
    }

    private static void ApplyCombinedFilter(
        ModelBuilder modelBuilder,
        Type entityType,
        SchoolDbContext context,
        bool hasSchoolScope)
    {
        var method = typeof(SchoolTenantQueryFilterExtensions)
            .GetMethod(nameof(ConfigureFilter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .MakeGenericMethod(entityType);

        method.Invoke(null, [modelBuilder, context, hasSchoolScope]);
    }

    private static void ConfigureFilter<TEntity>(
        ModelBuilder modelBuilder,
        SchoolDbContext context,
        bool hasSchoolScope)
        where TEntity : AuditableEntity
    {
        Expression<Func<TEntity, bool>> filter = hasSchoolScope
            ? e => !e.IsDeleted && (context.IgnoreSchoolScope
                || (context.EffectiveTenantSchoolId != null
                    && EF.Property<Guid>(e, "SchoolId") == context.EffectiveTenantSchoolId))
            : e => !e.IsDeleted;

        modelBuilder.Entity<TEntity>().HasQueryFilter(filter);
    }
}
