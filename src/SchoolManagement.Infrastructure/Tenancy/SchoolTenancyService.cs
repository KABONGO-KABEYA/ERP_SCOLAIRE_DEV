namespace SchoolManagement.Infrastructure.Tenancy;

using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Common;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Domain.Common;
using SchoolManagement.Infrastructure.Persistence;

public sealed class SchoolTenancyService : ISchoolTenancyService
{
    private readonly SchoolDbContext _context;

    public SchoolTenancyService(SchoolDbContext context)
    {
        _context = context;
    }

    public async Task EnsureBelongsToSchoolAsync<TEntity>(
        Guid schoolId,
        Guid entityId,
        CancellationToken cancellationToken = default)
        where TEntity : AuditableEntity
    {
        _ = await RequireForSchoolAsync<TEntity>(schoolId, entityId, cancellationToken);
    }

    public async Task<TEntity?> TryGetForSchoolAsync<TEntity>(
        Guid schoolId,
        Guid entityId,
        CancellationToken cancellationToken = default)
        where TEntity : AuditableEntity
    {
        SchoolTenantGuard.EnsureNotEmpty(schoolId);

        var previousOverride = _context.OverrideTenantSchoolId;
        var previousIgnore = _context.IgnoreSchoolScope;
        _context.OverrideTenantSchoolId = schoolId;
        _context.IgnoreSchoolScope = false;

        try
        {
            return await _context.Set<TEntity>()
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == entityId, cancellationToken);
        }
        finally
        {
            _context.OverrideTenantSchoolId = previousOverride;
            _context.IgnoreSchoolScope = previousIgnore;
        }
    }

    public async Task<TEntity> RequireForSchoolAsync<TEntity>(
        Guid schoolId,
        Guid entityId,
        CancellationToken cancellationToken = default)
        where TEntity : AuditableEntity
    {
        SchoolTenantGuard.EnsureNotEmpty(schoolId);

        var previousOverride = _context.OverrideTenantSchoolId;
        var previousIgnore = _context.IgnoreSchoolScope;
        _context.OverrideTenantSchoolId = schoolId;
        _context.IgnoreSchoolScope = false;

        try
        {
            var entity = await _context.Set<TEntity>()
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == entityId, cancellationToken);

            if (entity is null)
            {
                throw new SchoolTenancyAccessDeniedException(typeof(TEntity).Name);
            }

            return entity;
        }
        finally
        {
            _context.OverrideTenantSchoolId = previousOverride;
            _context.IgnoreSchoolScope = previousIgnore;
        }
    }

    public async Task<Guid?> TryResolveSchoolIdAsync<TEntity>(
        Guid entityId,
        CancellationToken cancellationToken = default)
        where TEntity : AuditableEntity
    {
        var previousIgnore = _context.IgnoreSchoolScope;
        _context.IgnoreSchoolScope = true;
        try
        {
            if (typeof(ISchoolScoped).IsAssignableFrom(typeof(TEntity)))
            {
                var scoped = await _context.Set<TEntity>()
                    .AsNoTracking()
                    .Where(e => e.Id == entityId)
                    .Select(e => EF.Property<Guid>(e, "SchoolId"))
                    .FirstOrDefaultAsync(cancellationToken);
                return scoped == Guid.Empty ? null : scoped;
            }

            return typeof(TEntity).Name switch
            {
                nameof(Domain.Entities.Grades.Evaluation) => await ResolveViaClassRoomAsync<Domain.Entities.Grades.Evaluation>(
                    entityId, e => e.ClassRoomId, cancellationToken),
                nameof(Domain.Entities.Students.Enrollment) => await ResolveViaStudentAsync(entityId, cancellationToken),
                nameof(Domain.Entities.Grades.GradeEntry) => await ResolveViaStudentIdAsync<Domain.Entities.Grades.GradeEntry>(
                    entityId, cancellationToken),
                nameof(Domain.Entities.Finance.PaymentLine) => await ResolveViaPaymentAsync(entityId, cancellationToken),
                _ => null
            };
        }
        finally
        {
            _context.IgnoreSchoolScope = previousIgnore;
        }
    }

    private async Task<Guid?> ResolveViaClassRoomAsync<T>(
        Guid entityId,
        System.Linq.Expressions.Expression<Func<T, Guid>> classRoomIdSelector,
        CancellationToken cancellationToken)
        where T : AuditableEntity
    {
        var classRoomId = await _context.Set<T>()
            .AsNoTracking()
            .Where(e => e.Id == entityId)
            .Select(classRoomIdSelector)
            .FirstOrDefaultAsync(cancellationToken);

        if (classRoomId == Guid.Empty)
        {
            return null;
        }

        return await _context.ClassRooms.AsNoTracking()
            .Where(c => c.Id == classRoomId)
            .Select(c => c.SchoolId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<Guid?> ResolveViaStudentAsync(Guid enrollmentId, CancellationToken cancellationToken)
    {
        var studentId = await _context.Enrollments.AsNoTracking()
            .Where(e => e.Id == enrollmentId)
            .Select(e => e.StudentId)
            .FirstOrDefaultAsync(cancellationToken);

        if (studentId == Guid.Empty)
        {
            return null;
        }

        return await _context.Students.AsNoTracking()
            .Where(s => s.Id == studentId)
            .Select(s => s.SchoolId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<Guid?> ResolveViaStudentIdAsync<T>(Guid entityId, CancellationToken cancellationToken)
        where T : AuditableEntity
    {
        var studentId = await _context.Set<T>().AsNoTracking()
            .Where(e => e.Id == entityId)
            .Select(e => EF.Property<Guid>(e, "StudentId"))
            .FirstOrDefaultAsync(cancellationToken);

        if (studentId == Guid.Empty)
        {
            return null;
        }

        return await _context.Students.AsNoTracking()
            .Where(s => s.Id == studentId)
            .Select(s => s.SchoolId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<Guid?> ResolveViaPaymentAsync(Guid paymentLineId, CancellationToken cancellationToken)
    {
        var paymentId = await _context.Set<Domain.Entities.Finance.PaymentLine>().AsNoTracking()
            .Where(l => l.Id == paymentLineId)
            .Select(l => l.PaymentId)
            .FirstOrDefaultAsync(cancellationToken);

        if (paymentId == Guid.Empty)
        {
            return null;
        }

        return await _context.Payments.AsNoTracking()
            .Where(p => p.Id == paymentId)
            .Select(p => p.SchoolId)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
