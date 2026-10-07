using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Application.Schools.DTOs;
using SchoolManagement.Application.Schools.Interfaces;
using SchoolManagement.Domain.Entities.Settings;

namespace SchoolManagement.Application.Schools.Services;

public sealed class SchoolSubscriptionService : ISchoolSubscriptionService
{
    private readonly IRepository<SchoolSubscription> _subscriptions;

    public SchoolSubscriptionService(IRepository<SchoolSubscription> subscriptions)
    {
        _subscriptions = subscriptions;
    }

    public async Task<SchoolSubscriptionDto> GetCurrentAsync(
        Guid schoolId,
        CancellationToken cancellationToken = default)
    {
        var rows = await _subscriptions.FindAsync(s => s.SchoolId == schoolId, cancellationToken);
        var row = rows.OrderByDescending(s => s.CreatedAt).FirstOrDefault();
        return Map(row, schoolId, DateTime.UtcNow);
    }

    public static SchoolSubscriptionDto Map(SchoolSubscription? row, Guid schoolId, DateTime utcNow)
    {
        if (row is null)
        {
            return new SchoolSubscriptionDto(
                IsConfigured: false,
                Id: null,
                SchoolId: schoolId,
                InstallationDate: null,
                DurationMonths: null,
                ExpirationDate: null,
                IsActive: false,
                IsExpired: false,
                IsValid: false,
                Status: SchoolSubscriptionStatuses.NotConfigured);
        }

        var expired = utcNow >= row.ExpirationDate;
        var valid = row.IsActive && !expired;
        var status = valid
            ? SchoolSubscriptionStatuses.Valid
            : SchoolSubscriptionStatuses.Expired;

        return new SchoolSubscriptionDto(
            IsConfigured: true,
            Id: row.Id,
            SchoolId: row.SchoolId,
            InstallationDate: row.InstallationDate,
            DurationMonths: row.DurationMonths,
            ExpirationDate: row.ExpirationDate,
            IsActive: row.IsActive,
            IsExpired: expired,
            IsValid: valid,
            Status: status);
    }
}
