namespace SchoolManagement.Desktop.Services;

using SchoolManagement.Application.Auth.DTOs;

public interface IAuthSessionService
{
    string? AccessToken { get; }

    string? RefreshToken { get; }

    UserProfileDto? CurrentUser { get; }

    bool IsAuthenticated { get; }

    void SetSession(AuthResponse response);

    void Clear();
}

public interface ISchoolApiService
{
    Task<SchoolManagement.Application.Schools.DTOs.SchoolDto?> GetCurrentSchoolAsync(CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.Schools.DTOs.SchoolDto> UpdateSchoolAsync(
        SchoolManagement.Application.Schools.DTOs.UpdateSchoolRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SchoolManagement.Application.Schools.DTOs.AcademicYearDto>> GetAcademicYearsAsync(
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.Schools.DTOs.AcademicYearDto> CreateAcademicYearAsync(
        SchoolManagement.Application.Schools.DTOs.CreateAcademicYearRequest request,
        CancellationToken cancellationToken = default);

    Task SetCurrentAcademicYearAsync(
        Guid yearId,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.Schools.DTOs.SchoolLookupsDto> GetLookupsAsync(
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.Schools.DTOs.SchoolRegulationDto> GetRegulationAsync(
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.Schools.DTOs.SchoolRegulationDto> UpdateRegulationAsync(
        SchoolManagement.Application.Schools.DTOs.UpdateSchoolRegulationRequest request,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.Schools.DTOs.PedagogicalStructureSummaryDto> GetPedagogicalSummaryAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SchoolManagement.Application.Schools.DTOs.PedagogicalClassDto>> GetPedagogicalClassesAsync(
        string? search = null,
        SchoolManagement.Domain.Enums.SchoolProgram? program = null,
        bool? enabledOnly = null,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.Schools.DTOs.PedagogicalClassDto> UpdatePedagogicalClassAsync(
        Guid classId,
        SchoolManagement.Application.Schools.DTOs.UpdatePedagogicalClassRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SchoolManagement.Application.Schools.DTOs.PedagogicalClassDto>> BulkUpdatePedagogicalClassesAsync(
        SchoolManagement.Application.Schools.DTOs.BulkUpdatePedagogicalClassesRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SchoolManagement.Application.Schools.DTOs.ClassLocalDto>> GetClassLocalsAsync(
        Guid pedagogicalClassId,
        Guid? academicYearId = null,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.Schools.DTOs.ClassLocalDto> CreateClassLocalAsync(
        SchoolManagement.Application.Schools.DTOs.CreateClassLocalRequest request,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.Schools.DTOs.ClassLocalDto> UpdateClassLocalAsync(
        Guid localId,
        SchoolManagement.Application.Schools.DTOs.UpdateClassLocalRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteClassLocalAsync(Guid localId, CancellationToken cancellationToken = default);

    Task InitializePedagogicalStructureAsync(CancellationToken cancellationToken = default);
}

public interface IPaymentApiService
{
    Task<SchoolManagement.Application.Payments.DTOs.PaymentListDto> SearchAsync(
        SchoolManagement.Application.Payments.DTOs.PaymentSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.Payments.DTOs.PaymentDto> CreateAsync(
        SchoolManagement.Application.Payments.DTOs.CreatePaymentRequest request,
        CancellationToken cancellationToken = default);
}

public interface IGradeApiService
{
    Task<IReadOnlyList<SchoolManagement.Application.Grades.DTOs.EvaluationDto>> GetEvaluationsAsync(
        Guid classRoomId, Guid academicPeriodId, CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.Grades.DTOs.EvaluationDto> CreateEvaluationAsync(
        SchoolManagement.Application.Grades.DTOs.CreateEvaluationRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SchoolManagement.Application.Grades.DTOs.PeriodResultDto>> CalculateResultsAsync(
        SchoolManagement.Application.Grades.DTOs.CalculatePeriodResultsRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SchoolManagement.Application.Grades.DTOs.GradeEntryDto>> GetGradeEntriesAsync(
        Guid evaluationId,
        CancellationToken cancellationToken = default);

    Task SubmitGradesAsync(
        SchoolManagement.Application.Grades.DTOs.SubmitGradesRequest request,
        CancellationToken cancellationToken = default);
}

public interface IStudentApiService
{
    Task<SchoolManagement.Application.Students.DTOs.StudentListDto> SearchAsync(
        SchoolManagement.Application.Students.DTOs.StudentSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.Students.DTOs.StudentDto> CreateAsync(
        SchoolManagement.Application.Students.DTOs.CreateStudentRequest request,
        CancellationToken cancellationToken = default);

    Task ArchiveAsync(Guid studentId, CancellationToken cancellationToken = default);
}

public interface IEnrollmentWizardApiService
{
    Task<SchoolManagement.Application.EnrollmentWizard.DTOs.EnrollmentPrerequisitesDto> GetPrerequisitesAsync(
        CancellationToken cancellationToken = default);

    Task<string> GenerateRegistrationNumberAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SchoolManagement.Application.EnrollmentWizard.DTOs.EnrollmentStudentSearchResultDto>> SearchStudentsAsync(
        string search,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.EnrollmentWizard.DTOs.EnrollmentStructureOptionsDto> GetStructureOptionsAsync(
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.EnrollmentWizard.DTOs.ClassCapacityDto> GetClassCapacityAsync(
        Guid classRoomId,
        Guid academicYearId,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.EnrollmentWizard.DTOs.EnrollmentFeeSummaryDto> CalculateFeesAsync(
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.EnrollmentWizard.DTOs.EnrollmentValidationResultDto> ValidateAsync(
        SchoolManagement.Application.EnrollmentWizard.DTOs.CompleteEnrollmentRequest request,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.EnrollmentWizard.DTOs.CompleteEnrollmentResultDto> CompleteAsync(
        SchoolManagement.Application.EnrollmentWizard.DTOs.CompleteEnrollmentRequest request,
        CancellationToken cancellationToken = default);
}

public interface IAcademicApiService
{
    Task<IReadOnlyList<SchoolManagement.Application.Academic.DTOs.SectionDto>> GetSectionsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SchoolManagement.Application.Academic.DTOs.ClassRoomDto>> GetClassRoomsAsync(
        Guid? academicYearId = null,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.Academic.DTOs.ClassRoomDto> CreateClassRoomAsync(
        SchoolManagement.Application.Academic.DTOs.CreateClassRoomRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SchoolManagement.Application.Academic.DTOs.CourseDto>> GetCoursesAsync(
        Guid? classRoomId = null,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.Academic.DTOs.CourseDto> CreateCourseAsync(
        SchoolManagement.Application.Academic.DTOs.CreateCourseRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SchoolManagement.Application.Academic.DTOs.EnrollmentDto>> GetEnrollmentsAsync(
        Guid? classRoomId = null,
        Guid? academicYearId = null,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.Academic.DTOs.EnrollmentDto> CreateEnrollmentAsync(
        SchoolManagement.Application.Academic.DTOs.CreateEnrollmentRequest request,
        CancellationToken cancellationToken = default);
}

public interface IDocumentApiService
{
    Task<IReadOnlyList<SchoolManagement.Application.Documents.DTOs.StudentDocumentDto>> ListAsync(
        Guid? studentId = null,
        CancellationToken cancellationToken = default);

    Task UploadAsync(Guid studentId, string documentType, string filePath, CancellationToken cancellationToken = default);

    Task DownloadAsync(Guid documentId, string destinationPath, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid documentId, CancellationToken cancellationToken = default);
}

public interface IReportApiService
{
    Task<SchoolManagement.Application.Reports.DTOs.DashboardStatsDto> GetDashboardAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SchoolManagement.Application.Reports.DTOs.EnrollmentByClassDto>> GetEnrollmentByClassAsync(
        Guid? academicYearId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SchoolManagement.Application.Reports.DTOs.ClassAverageReportDto>> GetClassAveragesAsync(
        Guid? academicPeriodId = null,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.Reports.DTOs.FinancialSummaryDto> GetFinancialSummaryAsync(
        Guid? academicYearId = null,
        CancellationToken cancellationToken = default);
}

public interface IAdminApiService
{
    Task<IReadOnlyList<SchoolManagement.Application.Admin.DTOs.UserAccountDto>> GetUsersAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SchoolManagement.Application.Admin.DTOs.RoleDto>> GetRolesAsync(
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.Admin.DTOs.UserAccountDto> CreateUserAsync(
        SchoolManagement.Application.Admin.DTOs.CreateUserRequest request,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.Admin.DTOs.UserAccountDto> UpdateUserAsync(
        Guid userId,
        SchoolManagement.Application.Admin.DTOs.UpdateUserRequest request,
        CancellationToken cancellationToken = default);

    Task<SchoolManagement.Application.Admin.DTOs.UserAccountDto> SetUserRolesAsync(
        Guid userId,
        SchoolManagement.Application.Admin.DTOs.SetUserRolesRequest request,
        CancellationToken cancellationToken = default);
}
