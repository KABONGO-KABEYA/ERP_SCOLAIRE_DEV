namespace SchoolManagement.Application.Enrollment.Interfaces;

public sealed record EnrollmentResetResultDto(
    int StudentsRemoved,
    int EnrollmentsRemoved,
    int GuardiansRemoved,
    int FilesRemoved,
    int ClassRoomsRepaired,
    string Message);

public interface IEnrollmentMaintenanceService
{
    Task<EnrollmentResetResultDto> ResetEnrollmentDataAsync(Guid schoolId, CancellationToken cancellationToken = default);
}
