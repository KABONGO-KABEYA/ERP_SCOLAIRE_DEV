namespace SchoolManagement.Desktop.Services;

using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Http;
using SchoolManagement.Application.Auth.DTOs;
using SchoolManagement.Shared.Models;

public sealed class AuthDelegatingHandler : DelegatingHandler
{
    private readonly IAuthSessionService _session;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public AuthDelegatingHandler(IAuthSessionService session, IHttpClientFactory httpClientFactory)
    {
        _session = session;
        _httpClientFactory = httpClientFactory;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(_session.AccessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _session.AccessToken);
        }

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode != HttpStatusCode.Unauthorized || string.IsNullOrEmpty(_session.RefreshToken))
        {
            return response;
        }

        response.Dispose();

        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            if (!await TryRefreshTokenAsync(cancellationToken))
            {
                return new HttpResponseMessage(HttpStatusCode.Unauthorized);
            }

            var retryRequest = await CloneHttpRequestMessageAsync(request, cancellationToken);
            retryRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _session.AccessToken);
            return await base.SendAsync(retryRequest, cancellationToken);
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private async Task<bool> TryRefreshTokenAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_session.RefreshToken))
        {
            return false;
        }

        var client = _httpClientFactory.CreateClient("SchoolApi");
        var refreshResponse = await client.PostAsJsonAsync(
            "api/v1/auth/refresh",
            new RefreshTokenRequest(_session.RefreshToken),
            cancellationToken);

        if (!refreshResponse.IsSuccessStatusCode)
        {
            _session.Clear();
            return false;
        }

        var body = await refreshResponse.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>(cancellationToken: cancellationToken);
        if (body?.Success != true || body.Data is null)
        {
            _session.Clear();
            return false;
        }

        _session.SetSession(body.Data);
        return true;
    }

    private static async Task<HttpRequestMessage> CloneHttpRequestMessageAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);

        if (request.Content is not null)
        {
            var contentBytes = await request.Content.ReadAsByteArrayAsync(cancellationToken);
            clone.Content = new ByteArrayContent(contentBytes);

            foreach (var header in request.Content.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        foreach (var header in request.Headers)
        {
            if (header.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }
}

public sealed class AuthSessionService : IAuthSessionService
{
    public string? AccessToken { get; private set; }

    public string? RefreshToken { get; private set; }

    public UserProfileDto? CurrentUser { get; private set; }

    public bool IsAuthenticated => !string.IsNullOrEmpty(AccessToken);

    public void SetSession(AuthResponse response)
    {
        AccessToken = response.AccessToken;
        RefreshToken = response.RefreshToken;
        CurrentUser = response.User;
    }

    public void Clear()
    {
        AccessToken = null;
        RefreshToken = null;
        CurrentUser = null;
    }
}

public sealed class AuthApiService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IAuthSessionService _session;

    public AuthApiService(IHttpClientFactory httpClientFactory, IAuthSessionService session)
    {
        _httpClientFactory = httpClientFactory;
        _session = session;
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient("SchoolApi");
        var response = await client.PostAsJsonAsync("api/v1/auth/login", request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new UnauthorizedAccessException("Identifiants invalides.");
        }

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Réponse API invalide.");

        if (!body.Success || body.Data is null)
        {
            throw new UnauthorizedAccessException(body.Message ?? "Échec de connexion.");
        }

        _session.SetSession(body.Data);
        return body.Data;
    }

    public async Task<AuthResponse> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient("SchoolApiAuth");
        var response = await client.PostAsJsonAsync("api/v1/auth/change-password", request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadFromJsonAsync<ApiResponse<object>>(cancellationToken: cancellationToken);
            throw new InvalidOperationException(errorBody?.Message ?? "Impossible de changer le mot de passe.");
        }

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Réponse API invalide.");

        if (!body.Success || body.Data is null)
        {
            throw new InvalidOperationException(body.Message ?? "Échec du changement de mot de passe.");
        }

        _session.SetSession(body.Data);
        return body.Data;
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(_session.RefreshToken))
        {
            var client = _httpClientFactory.CreateClient("SchoolApiAuth");
            try
            {
                await client.PostAsJsonAsync("api/v1/auth/logout", new RefreshTokenRequest(_session.RefreshToken), cancellationToken);
            }
            catch
            {
                // ignore logout errors
            }
        }

        _session.Clear();
    }
}

public sealed class ApiClient : IApiClient
{
    private readonly IHttpClientFactory _httpClientFactory;

    public ApiClient(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("SchoolApi");
            var response = await client.GetAsync("api/v1/health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}

public abstract class ApiServiceBase
{
    protected readonly IHttpClientFactory HttpClientFactory;

    protected ApiServiceBase(IHttpClientFactory httpClientFactory)
    {
        HttpClientFactory = httpClientFactory;
    }

    protected async Task<T> GetAsync<T>(string url, CancellationToken cancellationToken)
    {
        var client = HttpClientFactory.CreateClient("SchoolApiAuth");
        var response = await client.GetAsync(url, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<T>>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Réponse API invalide.");
        return body.Data ?? throw new InvalidOperationException(body.Message ?? "Données absentes.");
    }

    protected async Task<T> PostAsync<T>(string url, object payload, CancellationToken cancellationToken)
    {
        var client = HttpClientFactory.CreateClient("SchoolApiAuth");
        var response = await client.PostAsJsonAsync(url, payload, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<T>>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Réponse API invalide.");
        return body.Data ?? throw new InvalidOperationException(body.Message ?? "Données absentes.");
    }

    protected async Task<T> PutAsync<T>(string url, object payload, CancellationToken cancellationToken)
    {
        var client = HttpClientFactory.CreateClient("SchoolApiAuth");
        var response = await client.PutAsJsonAsync(url, payload, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<T>>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Réponse API invalide.");
        return body.Data ?? throw new InvalidOperationException(body.Message ?? "Données absentes.");
    }

    protected async Task DeleteAsync(string url, CancellationToken cancellationToken)
    {
        var client = HttpClientFactory.CreateClient("SchoolApiAuth");
        var response = await client.DeleteAsync(url, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var error = await response.Content.ReadFromJsonAsync<ApiResponse<object>>(cancellationToken: cancellationToken);
        throw new HttpRequestException(error?.Message ?? $"Erreur API ({(int)response.StatusCode})");
    }
}

public sealed class SchoolApiService : ApiServiceBase, ISchoolApiService
{
    public SchoolApiService(IHttpClientFactory httpClientFactory) : base(httpClientFactory)
    {
    }

    public async Task<SchoolManagement.Application.Schools.DTOs.SchoolDto?> GetCurrentSchoolAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await GetAsync<SchoolManagement.Application.Schools.DTOs.SchoolDto>("api/v1/schools/current", cancellationToken);
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public Task<SchoolManagement.Application.Schools.DTOs.SchoolDto> UpdateSchoolAsync(
        SchoolManagement.Application.Schools.DTOs.UpdateSchoolRequest request,
        CancellationToken cancellationToken = default) =>
        PutAsync<SchoolManagement.Application.Schools.DTOs.SchoolDto>("api/v1/schools/current", request, cancellationToken);

    public Task<IReadOnlyList<SchoolManagement.Application.Schools.DTOs.AcademicYearDto>> GetAcademicYearsAsync(
        CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<SchoolManagement.Application.Schools.DTOs.AcademicYearDto>>("api/v1/schools/current/academic-years", cancellationToken);

    public Task<SchoolManagement.Application.Schools.DTOs.AcademicYearDto> CreateAcademicYearAsync(
        SchoolManagement.Application.Schools.DTOs.CreateAcademicYearRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.Schools.DTOs.AcademicYearDto>("api/v1/schools/current/academic-years", request, cancellationToken);

    public async Task SetCurrentAcademicYearAsync(
        Guid yearId,
        CancellationToken cancellationToken = default)
    {
        var client = HttpClientFactory.CreateClient("SchoolApiAuth");
        var response = await client.PutAsync($"api/v1/schools/current/academic-years/{yearId}/set-current", null, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadFromJsonAsync<ApiResponse<object>>(cancellationToken: cancellationToken);
            throw new HttpRequestException(error?.Message ?? $"Erreur API ({(int)response.StatusCode})");
        }
    }

    public Task<SchoolManagement.Application.Schools.DTOs.SchoolLookupsDto> GetLookupsAsync(
        CancellationToken cancellationToken = default) =>
        GetAsync<SchoolManagement.Application.Schools.DTOs.SchoolLookupsDto>("api/v1/schools/current/lookups", cancellationToken);

    public Task<SchoolManagement.Application.Schools.DTOs.SchoolRegulationDto> GetRegulationAsync(
        CancellationToken cancellationToken = default) =>
        GetAsync<SchoolManagement.Application.Schools.DTOs.SchoolRegulationDto>("api/v1/schools/current/regulation", cancellationToken);

    public Task<SchoolManagement.Application.Schools.DTOs.SchoolRegulationDto> UpdateRegulationAsync(
        SchoolManagement.Application.Schools.DTOs.UpdateSchoolRegulationRequest request,
        CancellationToken cancellationToken = default) =>
        PutAsync<SchoolManagement.Application.Schools.DTOs.SchoolRegulationDto>("api/v1/schools/current/regulation", request, cancellationToken);

    public Task<SchoolManagement.Application.Schools.DTOs.PedagogicalStructureSummaryDto> GetPedagogicalSummaryAsync(
        CancellationToken cancellationToken = default) =>
        GetAsync<SchoolManagement.Application.Schools.DTOs.PedagogicalStructureSummaryDto>(
            "api/v1/schools/current/pedagogical-structure/summary", cancellationToken);

    public Task<IReadOnlyList<SchoolManagement.Application.Schools.DTOs.PedagogicalClassDto>> GetPedagogicalClassesAsync(
        string? search = null,
        SchoolManagement.Domain.Enums.SchoolProgram? program = null,
        bool? enabledOnly = null,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(search)) query.Add($"search={Uri.EscapeDataString(search)}");
        if (program.HasValue) query.Add($"program={(int)program.Value}");
        if (enabledOnly.HasValue) query.Add($"enabledOnly={enabledOnly.Value.ToString().ToLowerInvariant()}");
        var url = "api/v1/schools/current/pedagogical-structure/classes";
        if (query.Count > 0) url += "?" + string.Join("&", query);
        return GetAsync<IReadOnlyList<SchoolManagement.Application.Schools.DTOs.PedagogicalClassDto>>(url, cancellationToken);
    }

    public Task<SchoolManagement.Application.Schools.DTOs.PedagogicalClassDto> UpdatePedagogicalClassAsync(
        Guid classId,
        SchoolManagement.Application.Schools.DTOs.UpdatePedagogicalClassRequest request,
        CancellationToken cancellationToken = default) =>
        PutAsync<SchoolManagement.Application.Schools.DTOs.PedagogicalClassDto>(
            $"api/v1/schools/current/pedagogical-structure/classes/{classId}", request, cancellationToken);

    public Task<IReadOnlyList<SchoolManagement.Application.Schools.DTOs.PedagogicalClassDto>> BulkUpdatePedagogicalClassesAsync(
        SchoolManagement.Application.Schools.DTOs.BulkUpdatePedagogicalClassesRequest request,
        CancellationToken cancellationToken = default) =>
        PutAsync<IReadOnlyList<SchoolManagement.Application.Schools.DTOs.PedagogicalClassDto>>(
            "api/v1/schools/current/pedagogical-structure/classes", request, cancellationToken);

    public Task<IReadOnlyList<SchoolManagement.Application.Schools.DTOs.ClassLocalDto>> GetClassLocalsAsync(
        Guid pedagogicalClassId,
        Guid? academicYearId = null,
        CancellationToken cancellationToken = default)
    {
        var url = $"api/v1/schools/current/pedagogical-structure/classes/{pedagogicalClassId}/locals";
        if (academicYearId.HasValue) url += $"?academicYearId={academicYearId}";
        return GetAsync<IReadOnlyList<SchoolManagement.Application.Schools.DTOs.ClassLocalDto>>(url, cancellationToken);
    }

    public Task<SchoolManagement.Application.Schools.DTOs.ClassLocalDto> CreateClassLocalAsync(
        SchoolManagement.Application.Schools.DTOs.CreateClassLocalRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.Schools.DTOs.ClassLocalDto>(
            "api/v1/schools/current/pedagogical-structure/locals", request, cancellationToken);

    public Task<SchoolManagement.Application.Schools.DTOs.ClassLocalDto> UpdateClassLocalAsync(
        Guid localId,
        SchoolManagement.Application.Schools.DTOs.UpdateClassLocalRequest request,
        CancellationToken cancellationToken = default) =>
        PutAsync<SchoolManagement.Application.Schools.DTOs.ClassLocalDto>(
            $"api/v1/schools/current/pedagogical-structure/locals/{localId}", request, cancellationToken);

    public Task DeleteClassLocalAsync(Guid localId, CancellationToken cancellationToken = default) =>
        DeleteAsync($"api/v1/schools/current/pedagogical-structure/locals/{localId}", cancellationToken);

    public Task InitializePedagogicalStructureAsync(CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.Schools.DTOs.PedagogicalStructureSummaryDto>(
            "api/v1/schools/current/pedagogical-structure/initialize", new { }, cancellationToken);
}

public sealed class StudentApiService : ApiServiceBase, IStudentApiService
{
    public StudentApiService(IHttpClientFactory httpClientFactory) : base(httpClientFactory)
    {
    }

    public Task<SchoolManagement.Application.Students.DTOs.StudentListDto> SearchAsync(
        SchoolManagement.Application.Students.DTOs.StudentSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var url = $"api/v1/students?search={Uri.EscapeDataString(request.Search ?? "")}&page={request.Page}&pageSize={request.PageSize}";
        return GetAsync<SchoolManagement.Application.Students.DTOs.StudentListDto>(url, cancellationToken);
    }

    public Task<SchoolManagement.Application.Students.DTOs.StudentDto> CreateAsync(
        SchoolManagement.Application.Students.DTOs.CreateStudentRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.Students.DTOs.StudentDto>("api/v1/students", request, cancellationToken);

    public Task ArchiveAsync(Guid studentId, CancellationToken cancellationToken = default) =>
        DeleteAsync($"api/v1/students/{studentId}", cancellationToken);
}

public sealed class PaymentApiService : ApiServiceBase, IPaymentApiService
{
    public PaymentApiService(IHttpClientFactory httpClientFactory) : base(httpClientFactory) { }

    public Task<SchoolManagement.Application.Payments.DTOs.PaymentListDto> SearchAsync(
        SchoolManagement.Application.Payments.DTOs.PaymentSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var url = $"api/v1/payments?page={request.Page}&pageSize={request.PageSize}";
        if (request.StudentId.HasValue) url += $"&studentId={request.StudentId}";
        return GetAsync<SchoolManagement.Application.Payments.DTOs.PaymentListDto>(url, cancellationToken);
    }

    public Task<SchoolManagement.Application.Payments.DTOs.PaymentDto> CreateAsync(
        SchoolManagement.Application.Payments.DTOs.CreatePaymentRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.Payments.DTOs.PaymentDto>("api/v1/payments", request, cancellationToken);
}

public sealed class GradeApiService : ApiServiceBase, IGradeApiService
{
    public GradeApiService(IHttpClientFactory httpClientFactory) : base(httpClientFactory) { }

    public Task<IReadOnlyList<SchoolManagement.Application.Grades.DTOs.EvaluationDto>> GetEvaluationsAsync(
        Guid classRoomId, Guid academicPeriodId, CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<SchoolManagement.Application.Grades.DTOs.EvaluationDto>>(
            $"api/v1/grades/evaluations?classRoomId={classRoomId}&academicPeriodId={academicPeriodId}", cancellationToken);

    public Task<SchoolManagement.Application.Grades.DTOs.EvaluationDto> CreateEvaluationAsync(
        SchoolManagement.Application.Grades.DTOs.CreateEvaluationRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.Grades.DTOs.EvaluationDto>("api/v1/grades/evaluations", request, cancellationToken);

    public Task<IReadOnlyList<SchoolManagement.Application.Grades.DTOs.PeriodResultDto>> CalculateResultsAsync(
        SchoolManagement.Application.Grades.DTOs.CalculatePeriodResultsRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<IReadOnlyList<SchoolManagement.Application.Grades.DTOs.PeriodResultDto>>(
            "api/v1/grades/period-results/calculate", request, cancellationToken);

    public Task<IReadOnlyList<SchoolManagement.Application.Grades.DTOs.GradeEntryDto>> GetGradeEntriesAsync(
        Guid evaluationId,
        CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<SchoolManagement.Application.Grades.DTOs.GradeEntryDto>>(
            $"api/v1/grades/evaluations/{evaluationId}/entries", cancellationToken);

    public Task SubmitGradesAsync(
        SchoolManagement.Application.Grades.DTOs.SubmitGradesRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<object>("api/v1/grades/entries", request, cancellationToken);
}

public sealed class AcademicApiService : ApiServiceBase, IAcademicApiService
{
    public AcademicApiService(IHttpClientFactory httpClientFactory) : base(httpClientFactory) { }

    public Task<IReadOnlyList<SchoolManagement.Application.Academic.DTOs.SectionDto>> GetSectionsAsync(
        CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<SchoolManagement.Application.Academic.DTOs.SectionDto>>("api/v1/academic/sections", cancellationToken);

    public Task<IReadOnlyList<SchoolManagement.Application.Academic.DTOs.ClassRoomDto>> GetClassRoomsAsync(
        Guid? academicYearId = null,
        CancellationToken cancellationToken = default)
    {
        var url = "api/v1/academic/classrooms";
        if (academicYearId.HasValue) url += $"?academicYearId={academicYearId}";
        return GetAsync<IReadOnlyList<SchoolManagement.Application.Academic.DTOs.ClassRoomDto>>(url, cancellationToken);
    }

    public Task<SchoolManagement.Application.Academic.DTOs.ClassRoomDto> CreateClassRoomAsync(
        SchoolManagement.Application.Academic.DTOs.CreateClassRoomRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.Academic.DTOs.ClassRoomDto>("api/v1/academic/classrooms", request, cancellationToken);

    public Task<IReadOnlyList<SchoolManagement.Application.Academic.DTOs.CourseDto>> GetCoursesAsync(
        Guid? classRoomId = null,
        CancellationToken cancellationToken = default)
    {
        var url = "api/v1/academic/courses";
        if (classRoomId.HasValue) url += $"?classRoomId={classRoomId}";
        return GetAsync<IReadOnlyList<SchoolManagement.Application.Academic.DTOs.CourseDto>>(url, cancellationToken);
    }

    public Task<SchoolManagement.Application.Academic.DTOs.CourseDto> CreateCourseAsync(
        SchoolManagement.Application.Academic.DTOs.CreateCourseRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.Academic.DTOs.CourseDto>("api/v1/academic/courses", request, cancellationToken);

    public Task<IReadOnlyList<SchoolManagement.Application.Academic.DTOs.EnrollmentDto>> GetEnrollmentsAsync(
        Guid? classRoomId = null,
        Guid? academicYearId = null,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string>();
        if (classRoomId.HasValue) query.Add($"classRoomId={classRoomId}");
        if (academicYearId.HasValue) query.Add($"academicYearId={academicYearId}");
        var url = "api/v1/academic/enrollments" + (query.Count > 0 ? "?" + string.Join("&", query) : "");
        return GetAsync<IReadOnlyList<SchoolManagement.Application.Academic.DTOs.EnrollmentDto>>(url, cancellationToken);
    }

    public Task<SchoolManagement.Application.Academic.DTOs.EnrollmentDto> CreateEnrollmentAsync(
        SchoolManagement.Application.Academic.DTOs.CreateEnrollmentRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.Academic.DTOs.EnrollmentDto>("api/v1/academic/enrollments", request, cancellationToken);
}

public sealed class DocumentApiService : ApiServiceBase, IDocumentApiService
{
    public DocumentApiService(IHttpClientFactory httpClientFactory) : base(httpClientFactory) { }

    public Task<IReadOnlyList<SchoolManagement.Application.Documents.DTOs.StudentDocumentDto>> ListAsync(
        Guid? studentId = null,
        CancellationToken cancellationToken = default)
    {
        var url = "api/v1/documents";
        if (studentId.HasValue) url += $"?studentId={studentId}";
        return GetAsync<IReadOnlyList<SchoolManagement.Application.Documents.DTOs.StudentDocumentDto>>(url, cancellationToken);
    }

    public async Task UploadAsync(Guid studentId, string documentType, string filePath, CancellationToken cancellationToken = default)
    {
        var client = HttpClientFactory.CreateClient("SchoolApiAuth");
        await using var stream = File.OpenRead(filePath);
        using var content = new MultipartFormDataContent
        {
            { new StringContent(studentId.ToString()), "studentId" },
            { new StringContent(documentType), "documentType" },
            { new StreamContent(stream), "file", Path.GetFileName(filePath) }
        };

        var response = await client.PostAsync("api/v1/documents", content, cancellationToken);
        await EnsureSuccessPublicAsync(response, cancellationToken);
    }

    public async Task DownloadAsync(Guid documentId, string destinationPath, CancellationToken cancellationToken = default)
    {
        var client = HttpClientFactory.CreateClient("SchoolApiAuth");
        var response = await client.GetAsync($"api/v1/documents/{documentId}/download", cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var file = File.Create(destinationPath);
        await stream.CopyToAsync(file, cancellationToken);
    }

    public async Task DeleteAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var client = HttpClientFactory.CreateClient("SchoolApiAuth");
        var response = await client.DeleteAsync($"api/v1/documents/{documentId}", cancellationToken);
        await EnsureSuccessPublicAsync(response, cancellationToken);
    }

    private static async Task EnsureSuccessPublicAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;
        var error = await response.Content.ReadFromJsonAsync<ApiResponse<object>>(cancellationToken: cancellationToken);
        throw new HttpRequestException(error?.Message ?? $"Erreur API ({(int)response.StatusCode})");
    }
}

public sealed class ReportApiService : ApiServiceBase, IReportApiService
{
    public ReportApiService(IHttpClientFactory httpClientFactory) : base(httpClientFactory) { }

    public Task<SchoolManagement.Application.Reports.DTOs.DashboardStatsDto> GetDashboardAsync(
        CancellationToken cancellationToken = default) =>
        GetAsync<SchoolManagement.Application.Reports.DTOs.DashboardStatsDto>("api/v1/reports/dashboard", cancellationToken);

    public Task<IReadOnlyList<SchoolManagement.Application.Reports.DTOs.EnrollmentByClassDto>> GetEnrollmentByClassAsync(
        Guid? academicYearId = null,
        CancellationToken cancellationToken = default)
    {
        var url = "api/v1/reports/enrollment-by-class";
        if (academicYearId.HasValue) url += $"?academicYearId={academicYearId}";
        return GetAsync<IReadOnlyList<SchoolManagement.Application.Reports.DTOs.EnrollmentByClassDto>>(url, cancellationToken);
    }

    public Task<IReadOnlyList<SchoolManagement.Application.Reports.DTOs.ClassAverageReportDto>> GetClassAveragesAsync(
        Guid? academicPeriodId = null,
        CancellationToken cancellationToken = default)
    {
        var url = "api/v1/reports/class-averages";
        if (academicPeriodId.HasValue) url += $"?academicPeriodId={academicPeriodId}";
        return GetAsync<IReadOnlyList<SchoolManagement.Application.Reports.DTOs.ClassAverageReportDto>>(url, cancellationToken);
    }

    public Task<SchoolManagement.Application.Reports.DTOs.FinancialSummaryDto> GetFinancialSummaryAsync(
        Guid? academicYearId = null,
        CancellationToken cancellationToken = default)
    {
        var url = "api/v1/reports/financial-summary";
        if (academicYearId.HasValue) url += $"?academicYearId={academicYearId}";
        return GetAsync<SchoolManagement.Application.Reports.DTOs.FinancialSummaryDto>(url, cancellationToken);
    }
}

public sealed class AdminApiService : ApiServiceBase, IAdminApiService
{
    public AdminApiService(IHttpClientFactory httpClientFactory) : base(httpClientFactory) { }

    public Task<IReadOnlyList<SchoolManagement.Application.Admin.DTOs.UserAccountDto>> GetUsersAsync(
        CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<SchoolManagement.Application.Admin.DTOs.UserAccountDto>>("api/v1/admin/users", cancellationToken);

    public Task<IReadOnlyList<SchoolManagement.Application.Admin.DTOs.RoleDto>> GetRolesAsync(
        CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<SchoolManagement.Application.Admin.DTOs.RoleDto>>("api/v1/admin/roles", cancellationToken);

    public Task<SchoolManagement.Application.Admin.DTOs.UserAccountDto> CreateUserAsync(
        SchoolManagement.Application.Admin.DTOs.CreateUserRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.Admin.DTOs.UserAccountDto>("api/v1/admin/users", request, cancellationToken);

    public Task<SchoolManagement.Application.Admin.DTOs.UserAccountDto> UpdateUserAsync(
        Guid userId,
        SchoolManagement.Application.Admin.DTOs.UpdateUserRequest request,
        CancellationToken cancellationToken = default) =>
        PutAsync<SchoolManagement.Application.Admin.DTOs.UserAccountDto>($"api/v1/admin/users/{userId}", request, cancellationToken);

    public Task<SchoolManagement.Application.Admin.DTOs.UserAccountDto> SetUserRolesAsync(
        Guid userId,
        SchoolManagement.Application.Admin.DTOs.SetUserRolesRequest request,
        CancellationToken cancellationToken = default) =>
        PutAsync<SchoolManagement.Application.Admin.DTOs.UserAccountDto>($"api/v1/admin/users/{userId}/roles", request, cancellationToken);
}

public sealed class EnrollmentWizardApiService : ApiServiceBase, IEnrollmentWizardApiService
{
    public EnrollmentWizardApiService(IHttpClientFactory httpClientFactory) : base(httpClientFactory) { }

    public Task<SchoolManagement.Application.EnrollmentWizard.DTOs.EnrollmentPrerequisitesDto> GetPrerequisitesAsync(
        CancellationToken cancellationToken = default) =>
        GetAsync<SchoolManagement.Application.EnrollmentWizard.DTOs.EnrollmentPrerequisitesDto>(
            "api/v1/enrollment-wizard/prerequisites", cancellationToken);

    public async Task<string> GenerateRegistrationNumberAsync(CancellationToken cancellationToken = default)
    {
        var result = await GetAsync<SchoolManagement.Application.EnrollmentWizard.DTOs.GeneratedRegistrationNumberDto>(
            "api/v1/enrollment-wizard/registration-number", cancellationToken);
        return result.RegistrationNumber;
    }

    public Task<IReadOnlyList<SchoolManagement.Application.EnrollmentWizard.DTOs.EnrollmentStudentSearchResultDto>> SearchStudentsAsync(
        string search,
        CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<SchoolManagement.Application.EnrollmentWizard.DTOs.EnrollmentStudentSearchResultDto>>(
            $"api/v1/enrollment-wizard/search-students?search={Uri.EscapeDataString(search)}",
            cancellationToken);

    public Task<SchoolManagement.Application.EnrollmentWizard.DTOs.EnrollmentStructureOptionsDto> GetStructureOptionsAsync(
        CancellationToken cancellationToken = default) =>
        GetAsync<SchoolManagement.Application.EnrollmentWizard.DTOs.EnrollmentStructureOptionsDto>(
            "api/v1/enrollment-wizard/structure-options", cancellationToken);

    public Task<SchoolManagement.Application.EnrollmentWizard.DTOs.ClassCapacityDto> GetClassCapacityAsync(
        Guid classRoomId,
        Guid academicYearId,
        CancellationToken cancellationToken = default) =>
        GetAsync<SchoolManagement.Application.EnrollmentWizard.DTOs.ClassCapacityDto>(
            $"api/v1/enrollment-wizard/class-capacity?classRoomId={classRoomId}&academicYearId={academicYearId}",
            cancellationToken);

    public Task<SchoolManagement.Application.EnrollmentWizard.DTOs.EnrollmentFeeSummaryDto> CalculateFeesAsync(
        CancellationToken cancellationToken = default) =>
        GetAsync<SchoolManagement.Application.EnrollmentWizard.DTOs.EnrollmentFeeSummaryDto>(
            "api/v1/enrollment-wizard/fees", cancellationToken);

    public Task<SchoolManagement.Application.EnrollmentWizard.DTOs.EnrollmentValidationResultDto> ValidateAsync(
        SchoolManagement.Application.EnrollmentWizard.DTOs.CompleteEnrollmentRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.EnrollmentWizard.DTOs.EnrollmentValidationResultDto>(
            "api/v1/enrollment-wizard/validate", request, cancellationToken);

    public Task<SchoolManagement.Application.EnrollmentWizard.DTOs.CompleteEnrollmentResultDto> CompleteAsync(
        SchoolManagement.Application.EnrollmentWizard.DTOs.CompleteEnrollmentRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.EnrollmentWizard.DTOs.CompleteEnrollmentResultDto>(
            "api/v1/enrollment-wizard/complete", request, cancellationToken);
}
