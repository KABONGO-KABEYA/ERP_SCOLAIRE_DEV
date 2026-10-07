using SchoolManagement.Application.Schools.DTOs;

namespace SchoolManagement.Application.Schools.Interfaces;

public interface ISchoolSubscriptionService
{
    Task<SchoolSubscriptionDto> GetCurrentAsync(Guid schoolId, CancellationToken cancellationToken = default);
}
