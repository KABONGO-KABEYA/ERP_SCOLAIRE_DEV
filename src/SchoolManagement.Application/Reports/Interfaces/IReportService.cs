namespace SchoolManagement.Application.Reports.Interfaces;

using SchoolManagement.Application.Reports.DTOs;

public interface IReportService
{
    Task<DashboardStatsDto> GetDashboardAsync(Guid schoolId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EnrollmentByClassDto>> GetEnrollmentByClassAsync(
        Guid schoolId,
        Guid? academicYearId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ClassAverageReportDto>> GetClassAveragesAsync(
        Guid schoolId,
        Guid? academicPeriodId = null,
        CancellationToken cancellationToken = default);

    Task<FinancialSummaryDto> GetFinancialSummaryAsync(
        Guid schoolId,
        Guid? academicYearId = null,
        CancellationToken cancellationToken = default);
}
