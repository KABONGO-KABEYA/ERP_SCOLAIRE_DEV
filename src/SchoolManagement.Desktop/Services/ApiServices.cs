namespace SchoolManagement.Desktop.Services;

using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Http;
using SchoolManagement.Application.Auth.DTOs;
using SchoolManagement.Shared.Constants;
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

    public bool IsAdministrator =>
        HasPermission(Permissions.AdminFull);

    public bool HasPermission(string permissionCode)
    {
        if (string.IsNullOrWhiteSpace(permissionCode))
        {
            return false;
        }

        var user = CurrentUser;
        if (user?.Permissions is null || user.Permissions.Count == 0)
        {
            return false;
        }

        return user.Permissions.Any(p => string.Equals(p, permissionCode, StringComparison.OrdinalIgnoreCase));
    }

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
            var response = await client.GetAsync("api/health", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                response = await client.GetAsync("api/v1/health", cancellationToken);
            }
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

        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(raw))
        {
            var code = (int)response.StatusCode;
            var hint = response.StatusCode == HttpStatusCode.NotFound
                ? "Ressource ou endpoint introuvable (API peut-être obsolète). Relancez l'API à jour."
                : $"Erreur API ({code}) sans détail.";
            throw new HttpRequestException(hint);
        }

        try
        {
            var error = System.Text.Json.JsonSerializer.Deserialize<ApiResponse<object>>(
                raw,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            throw new HttpRequestException(error?.Message ?? $"Erreur API ({(int)response.StatusCode})");
        }
        catch (HttpRequestException)
        {
            throw;
        }
        catch (System.Text.Json.JsonException)
        {
            throw new HttpRequestException($"Erreur API ({(int)response.StatusCode}) : {raw.Trim()[..Math.Min(raw.Trim().Length, 200)]}");
        }
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
        Guid? academicYearId = null,
        CancellationToken cancellationToken = default)
    {
        var url = "api/v1/schools/current/pedagogical-structure/summary";
        if (academicYearId.HasValue)
        {
            url += $"?academicYearId={academicYearId}";
        }

        return GetAsync<SchoolManagement.Application.Schools.DTOs.PedagogicalStructureSummaryDto>(url, cancellationToken);
    }

    public Task<IReadOnlyList<SchoolManagement.Application.Schools.DTOs.PedagogicalClassDto>> GetPedagogicalClassesAsync(
        string? search = null,
        SchoolManagement.Domain.Enums.SchoolProgram? program = null,
        bool? enabledOnly = null,
        Guid? academicYearId = null,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(search)) query.Add($"search={Uri.EscapeDataString(search)}");
        if (program.HasValue) query.Add($"program={(int)program.Value}");
        if (enabledOnly.HasValue) query.Add($"enabledOnly={enabledOnly.Value.ToString().ToLowerInvariant()}");
        if (academicYearId.HasValue) query.Add($"academicYearId={academicYearId}");
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
        var query = new List<string>
        {
            $"page={request.Page}",
            $"pageSize={request.PageSize}",
            $"applyFilters={request.ApplyFilters.ToString().ToLowerInvariant()}",
            $"includeAll={request.IncludeAll.ToString().ToLowerInvariant()}"
        };

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            query.Add($"search={Uri.EscapeDataString(request.Search)}");
        }

        if (request.AcademicYearId.HasValue)
        {
            query.Add($"academicYearId={request.AcademicYearId}");
        }

        if (request.SectionId.HasValue)
        {
            query.Add($"sectionId={request.SectionId}");
        }

        if (request.PedagogicalClassId.HasValue)
        {
            query.Add($"pedagogicalClassId={request.PedagogicalClassId}");
        }

        if (request.ClassRoomId.HasValue)
        {
            query.Add($"classRoomId={request.ClassRoomId}");
        }

        if (!string.IsNullOrWhiteSpace(request.StudyOption))
        {
            query.Add($"studyOption={Uri.EscapeDataString(request.StudyOption)}");
        }

        query.Add($"includeInscrits={request.IncludeInscrits.ToString().ToLowerInvariant()}");
        query.Add($"includeExcluded={request.IncludeExcluded.ToString().ToLowerInvariant()}");
        query.Add($"includeAbandoned={request.IncludeAbandoned.ToString().ToLowerInvariant()}");

        var url = "api/v1/students?" + string.Join("&", query);
        return GetAsync<SchoolManagement.Application.Students.DTOs.StudentListDto>(url, cancellationToken);
    }

    public Task<SchoolManagement.Application.Students.DTOs.StudentProfileDto> GetProfileAsync(
        Guid studentId,
        CancellationToken cancellationToken = default) =>
        GetAsync<SchoolManagement.Application.Students.DTOs.StudentProfileDto>(
            $"api/v1/students/{studentId}/profile", cancellationToken);

    public Task<SchoolManagement.Application.Students.DTOs.StudentDto> CreateAsync(
        SchoolManagement.Application.Students.DTOs.CreateStudentRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.Students.DTOs.StudentDto>("api/v1/students", request, cancellationToken);

    public Task<SchoolManagement.Application.Students.DTOs.StudentDto> UpdateAsync(
        Guid studentId,
        SchoolManagement.Application.Students.DTOs.UpdateStudentRequest request,
        CancellationToken cancellationToken = default) =>
        PutAsync<SchoolManagement.Application.Students.DTOs.StudentDto>($"api/v1/students/{studentId}", request, cancellationToken);

    public Task WithdrawFromCurrentYearAsync(
        Guid studentId,
        SchoolManagement.Application.Students.DTOs.WithdrawFromCurrentYearRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<object>($"api/v1/students/{studentId}/withdraw-current-year", request, cancellationToken);

    public Task<SchoolManagement.Application.Students.WithdrawalReasonsDto> GetWithdrawalReasonsAsync(
        CancellationToken cancellationToken = default) =>
        GetAsync<SchoolManagement.Application.Students.WithdrawalReasonsDto>(
            "api/v1/students/withdrawal-reasons", cancellationToken);

    public Task ExcludeFromCurrentYearAsync(Guid studentId, CancellationToken cancellationToken = default) =>
        PostAsync<object>($"api/v1/students/{studentId}/exclude-current-year", new { }, cancellationToken);

    public Task ArchiveAsync(Guid studentId, CancellationToken cancellationToken = default) =>
        DeleteAsync($"api/v1/students/{studentId}", cancellationToken);

    public Task<IReadOnlyList<SchoolManagement.Application.Students.DTOs.StudentDossierFileDto>> ListDossierFilesAsync(
        Guid studentId,
        CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<SchoolManagement.Application.Students.DTOs.StudentDossierFileDto>>(
            $"api/v1/students/{studentId}/dossier-files", cancellationToken);
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

    public Task<SchoolManagement.Application.Payments.DTOs.PaymentDetailDto> GetByIdAsync(
        Guid paymentId,
        CancellationToken cancellationToken = default) =>
        GetAsync<SchoolManagement.Application.Payments.DTOs.PaymentDetailDto>(
            $"api/v1/payments/{paymentId}", cancellationToken);

    public Task<SchoolManagement.Application.Payments.DTOs.FeeTypeStatementDto> GetFeeTypeStatementAsync(
        Guid paymentId,
        Guid? feeTypeId = null,
        CancellationToken cancellationToken = default)
    {
        var url = $"api/v1/payments/{paymentId}/fee-type-statement";
        if (feeTypeId.HasValue)
        {
            url += $"?feeTypeId={feeTypeId}";
        }

        return GetAsync<SchoolManagement.Application.Payments.DTOs.FeeTypeStatementDto>(url, cancellationToken);
    }

    public Task<SchoolManagement.Application.Payments.DTOs.FeeTypeStatementDto> GetFeeTypeStatementForStudentAsync(
        Guid studentId,
        Guid academicYearId,
        Guid feeTypeId,
        CancellationToken cancellationToken = default)
    {
        var url =
            $"api/v1/payments/fee-type-statement?studentId={studentId}&academicYearId={academicYearId}&feeTypeId={feeTypeId}";
        return GetAsync<SchoolManagement.Application.Payments.DTOs.FeeTypeStatementDto>(url, cancellationToken);
    }

    public async Task<byte[]> ExportFeeTypeStatementPdfAsync(
        Guid paymentId,
        Guid? feeTypeId = null,
        CancellationToken cancellationToken = default)
    {
        var url = $"api/v1/payments/{paymentId}/fee-type-statement/pdf";
        if (feeTypeId.HasValue)
        {
            url += $"?feeTypeId={feeTypeId}";
        }

        var client = HttpClientFactory.CreateClient("SchoolApiAuth");
        using var response = await client.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    public async Task<byte[]> ExportFeeTypeStatementPdfForStudentAsync(
        Guid studentId,
        Guid academicYearId,
        Guid feeTypeId,
        CancellationToken cancellationToken = default)
    {
        var url =
            $"api/v1/payments/fee-type-statement/pdf?studentId={studentId}&academicYearId={academicYearId}&feeTypeId={feeTypeId}";
        var client = HttpClientFactory.CreateClient("SchoolApiAuth");
        using var response = await client.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    public Task<SchoolManagement.Application.Payments.DTOs.StudentFinancialSummaryDto> GetStudentFinancialSummaryAsync(
        Guid studentId,
        Guid academicYearId,
        CancellationToken cancellationToken = default) =>
        GetAsync<SchoolManagement.Application.Payments.DTOs.StudentFinancialSummaryDto>(
            $"api/v1/payments/student/{studentId}/summary?academicYearId={academicYearId}", cancellationToken);

    public Task<SchoolManagement.Application.Payments.DTOs.PaymentMutationGateDto> GetMutationGateAsync(
        Guid academicYearId,
        Guid feeTypeId,
        CancellationToken cancellationToken = default) =>
        GetAsync<SchoolManagement.Application.Payments.DTOs.PaymentMutationGateDto>(
            $"api/v1/payments/mutation-gate?academicYearId={academicYearId}&feeTypeId={feeTypeId}",
            cancellationToken);

    public async Task CancelAsync(
        Guid paymentId,
        SchoolManagement.Application.Payments.DTOs.CancelPaymentRequest request,
        CancellationToken cancellationToken = default) =>
        await PostAsync<object>($"api/v1/payments/{paymentId}/cancel", request, cancellationToken);

    public Task<SchoolManagement.Application.Payments.DTOs.PaymentDetailDto> UpdateNotesAsync(
        Guid paymentId,
        SchoolManagement.Application.Payments.DTOs.UpdatePaymentNotesRequest request,
        CancellationToken cancellationToken = default) =>
        PutAsync<SchoolManagement.Application.Payments.DTOs.PaymentDetailDto>(
            $"api/v1/payments/{paymentId}/notes", request, cancellationToken);

    public Task<SchoolManagement.Application.Payments.DTOs.PaymentDetailDto> UpdateAmountAsync(
        Guid paymentId,
        SchoolManagement.Application.Payments.DTOs.UpdatePaymentAmountRequest request,
        CancellationToken cancellationToken = default) =>
        PutAsync<SchoolManagement.Application.Payments.DTOs.PaymentDetailDto>(
            $"api/v1/payments/{paymentId}/amount", request, cancellationToken);
}

public sealed class RevenueAllocationApiService : ApiServiceBase, IRevenueAllocationApiService
{
    public RevenueAllocationApiService(IHttpClientFactory httpClientFactory) : base(httpClientFactory) { }

    public Task<IReadOnlyList<SchoolManagement.Application.RevenueAllocation.DTOs.RevenueDestinationDto>> GetDestinationsAsync(
        bool activeOnly = false,
        CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<SchoolManagement.Application.RevenueAllocation.DTOs.RevenueDestinationDto>>(
            $"api/v1/revenue-allocation/destinations?activeOnly={activeOnly}", cancellationToken);

    public Task<SchoolManagement.Application.RevenueAllocation.DTOs.RevenueDestinationDto> CreateDestinationAsync(
        SchoolManagement.Application.RevenueAllocation.DTOs.SaveRevenueDestinationRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.RevenueAllocation.DTOs.RevenueDestinationDto>(
            "api/v1/revenue-allocation/destinations", request, cancellationToken);

    public Task<SchoolManagement.Application.RevenueAllocation.DTOs.RevenueDestinationDto> UpdateDestinationAsync(
        Guid id,
        SchoolManagement.Application.RevenueAllocation.DTOs.SaveRevenueDestinationRequest request,
        CancellationToken cancellationToken = default) =>
        PutAsync<SchoolManagement.Application.RevenueAllocation.DTOs.RevenueDestinationDto>(
            $"api/v1/revenue-allocation/destinations/{id}", request, cancellationToken);

    public Task DeactivateDestinationAsync(Guid id, CancellationToken cancellationToken = default) =>
        PostAsync<object>($"api/v1/revenue-allocation/destinations/{id}/deactivate", new { }, cancellationToken);

    public Task<IReadOnlyList<SchoolManagement.Application.RevenueAllocation.DTOs.RevenueAllocationKeyDto>> GetKeysAsync(
        Guid? academicYearId = null,
        CancellationToken cancellationToken = default)
    {
        var url = "api/v1/revenue-allocation/keys";
        if (academicYearId.HasValue)
        {
            url += $"?academicYearId={academicYearId}";
        }

        return GetAsync<IReadOnlyList<SchoolManagement.Application.RevenueAllocation.DTOs.RevenueAllocationKeyDto>>(url, cancellationToken);
    }

    public Task<SchoolManagement.Application.RevenueAllocation.DTOs.RevenueAllocationKeyDto> CreateKeyAsync(
        SchoolManagement.Application.RevenueAllocation.DTOs.CreateRevenueAllocationKeyRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.RevenueAllocation.DTOs.RevenueAllocationKeyDto>(
            "api/v1/revenue-allocation/keys", request, cancellationToken);

    public Task<SchoolManagement.Application.RevenueAllocation.DTOs.RevenueAllocationKeyDto> UpdateKeyAsync(
        Guid id,
        SchoolManagement.Application.RevenueAllocation.DTOs.UpdateRevenueAllocationKeyRequest request,
        CancellationToken cancellationToken = default) =>
        PutAsync<SchoolManagement.Application.RevenueAllocation.DTOs.RevenueAllocationKeyDto>(
            $"api/v1/revenue-allocation/keys/{id}", request, cancellationToken);

    public Task ActivateKeyAsync(Guid id, CancellationToken cancellationToken = default) =>
        PostAsync<object>($"api/v1/revenue-allocation/keys/{id}/activate", new { }, cancellationToken);

    public Task CloseKeyAsync(Guid id, DateOnly? endDate = null, CancellationToken cancellationToken = default) =>
        PostAsync<object>(
            $"api/v1/revenue-allocation/keys/{id}/close",
            new SchoolManagement.Application.RevenueAllocation.DTOs.CloseRevenueAllocationKeyRequest(endDate),
            cancellationToken);

    public Task DeactivateKeyAsync(Guid id, CancellationToken cancellationToken = default) =>
        PostAsync<object>($"api/v1/revenue-allocation/keys/{id}/deactivate", new { }, cancellationToken);

    public Task DeleteKeyAsync(Guid id, CancellationToken cancellationToken = default) =>
        DeleteAsync($"api/v1/revenue-allocation/keys/{id}", cancellationToken);

    public Task<SchoolManagement.Application.RevenueAllocation.DTOs.RevenueAllocationSearchResultDto> SearchEntriesAsync(
        SchoolManagement.Application.RevenueAllocation.DTOs.RevenueAllocationSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var parts = new List<string>
        {
            $"page={request.Page}",
            $"pageSize={request.PageSize}"
        };
        if (request.AcademicYearId.HasValue) parts.Add($"academicYearId={request.AcademicYearId}");
        if (request.FromDate.HasValue) parts.Add($"fromDate={request.FromDate:yyyy-MM-dd}");
        if (request.ToDate.HasValue) parts.Add($"toDate={request.ToDate:yyyy-MM-dd}");
        if (request.StudentId.HasValue) parts.Add($"studentId={request.StudentId}");
        if (request.PaymentId.HasValue) parts.Add($"paymentId={request.PaymentId}");
        if (request.DestinationId.HasValue) parts.Add($"destinationId={request.DestinationId}");
        if (request.FeeTypeId.HasValue) parts.Add($"feeTypeId={request.FeeTypeId}");
        if (request.SectionId.HasValue) parts.Add($"sectionId={request.SectionId}");
        if (request.ClassRoomId.HasValue) parts.Add($"classRoomId={request.ClassRoomId}");

        return GetAsync<SchoolManagement.Application.RevenueAllocation.DTOs.RevenueAllocationSearchResultDto>(
            $"api/v1/revenue-allocation/entries?{string.Join("&", parts)}", cancellationToken);
    }

    public Task<IReadOnlyList<SchoolManagement.Application.RevenueAllocation.DTOs.FeeTypeAllocationSummaryGroupDto>> GetAllocationSummaryByFeeTypeAsync(
        SchoolManagement.Application.RevenueAllocation.DTOs.RevenueAllocationSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var parts = new List<string>();
        if (request.AcademicYearId.HasValue) parts.Add($"academicYearId={request.AcademicYearId}");
        if (request.FromDate.HasValue) parts.Add($"fromDate={request.FromDate:yyyy-MM-dd}");
        if (request.ToDate.HasValue) parts.Add($"toDate={request.ToDate:yyyy-MM-dd}");
        if (request.StudentId.HasValue) parts.Add($"studentId={request.StudentId}");
        if (request.PaymentId.HasValue) parts.Add($"paymentId={request.PaymentId}");
        if (request.DestinationId.HasValue) parts.Add($"destinationId={request.DestinationId}");
        if (request.FeeTypeId.HasValue) parts.Add($"feeTypeId={request.FeeTypeId}");
        if (request.SectionId.HasValue) parts.Add($"sectionId={request.SectionId}");
        if (request.ClassRoomId.HasValue) parts.Add($"classRoomId={request.ClassRoomId}");

        var query = parts.Count > 0 ? $"?{string.Join("&", parts)}" : string.Empty;
        return GetAsync<IReadOnlyList<SchoolManagement.Application.RevenueAllocation.DTOs.FeeTypeAllocationSummaryGroupDto>>(
            $"api/v1/revenue-allocation/entries/summary-by-fee-type{query}", cancellationToken);
    }

    public Task<SchoolManagement.Application.RevenueAllocation.DTOs.AllocationCashFlowResultDto> GetAllocationCashFlowAsync(
        SchoolManagement.Application.RevenueAllocation.DTOs.RevenueAllocationSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var parts = new List<string>();
        if (request.AcademicYearId.HasValue) parts.Add($"academicYearId={request.AcademicYearId}");
        if (request.FromDate.HasValue) parts.Add($"fromDate={request.FromDate:yyyy-MM-dd}");
        if (request.ToDate.HasValue) parts.Add($"toDate={request.ToDate:yyyy-MM-dd}");
        if (request.StudentId.HasValue) parts.Add($"studentId={request.StudentId}");
        if (request.PaymentId.HasValue) parts.Add($"paymentId={request.PaymentId}");
        if (request.DestinationId.HasValue) parts.Add($"destinationId={request.DestinationId}");
        if (request.FeeTypeId.HasValue) parts.Add($"feeTypeId={request.FeeTypeId}");
        if (request.SectionId.HasValue) parts.Add($"sectionId={request.SectionId}");
        if (request.ClassRoomId.HasValue) parts.Add($"classRoomId={request.ClassRoomId}");

        var query = parts.Count > 0 ? $"?{string.Join("&", parts)}" : string.Empty;
        return GetAsync<SchoolManagement.Application.RevenueAllocation.DTOs.AllocationCashFlowResultDto>(
            $"api/v1/revenue-allocation/entries/cash-flow{query}", cancellationToken);
    }

    public Task<SchoolManagement.Application.RevenueAllocation.DTOs.WithholdingReportResultDto> GetWithholdingReportAsync(
        SchoolManagement.Application.RevenueAllocation.DTOs.RevenueAllocationSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var parts = new List<string>();
        if (request.AcademicYearId.HasValue) parts.Add($"academicYearId={request.AcademicYearId}");
        if (request.FromDate.HasValue) parts.Add($"fromDate={request.FromDate:yyyy-MM-dd}");
        if (request.ToDate.HasValue) parts.Add($"toDate={request.ToDate:yyyy-MM-dd}");
        if (request.StudentId.HasValue) parts.Add($"studentId={request.StudentId}");
        if (request.PaymentId.HasValue) parts.Add($"paymentId={request.PaymentId}");
        if (request.DestinationId.HasValue) parts.Add($"destinationId={request.DestinationId}");
        if (request.FeeTypeId.HasValue) parts.Add($"feeTypeId={request.FeeTypeId}");
        if (request.SectionId.HasValue) parts.Add($"sectionId={request.SectionId}");
        if (request.ClassRoomId.HasValue) parts.Add($"classRoomId={request.ClassRoomId}");

        var query = parts.Count > 0 ? $"?{string.Join("&", parts)}" : string.Empty;
        return GetAsync<SchoolManagement.Application.RevenueAllocation.DTOs.WithholdingReportResultDto>(
            $"api/v1/revenue-allocation/entries/withholdings{query}", cancellationToken);
    }

    public async Task<byte[]> ExportExcelAsync(
        SchoolManagement.Application.RevenueAllocation.DTOs.RevenueAllocationSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = BuildExportQuery(request);
        var client = HttpClientFactory.CreateClient("SchoolApiAuth");
        var response = await client.GetAsync($"api/v1/revenue-allocation/entries/export/excel?{query}", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Erreur export Excel ({(int)response.StatusCode})");
        }

        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    public async Task<byte[]> ExportPdfAsync(
        SchoolManagement.Application.RevenueAllocation.DTOs.RevenueAllocationSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = BuildExportQuery(request);
        var client = HttpClientFactory.CreateClient("SchoolApiAuth");
        var response = await client.GetAsync($"api/v1/revenue-allocation/entries/export/pdf?{query}", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Erreur export PDF ({(int)response.StatusCode})");
        }

        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    private static string BuildExportQuery(SchoolManagement.Application.RevenueAllocation.DTOs.RevenueAllocationSearchRequest request)
    {
        var parts = new List<string>();
        if (request.AcademicYearId.HasValue) parts.Add($"academicYearId={request.AcademicYearId}");
        if (request.FromDate.HasValue) parts.Add($"fromDate={request.FromDate:yyyy-MM-dd}");
        if (request.ToDate.HasValue) parts.Add($"toDate={request.ToDate:yyyy-MM-dd}");
        if (request.StudentId.HasValue) parts.Add($"studentId={request.StudentId}");
        if (request.PaymentId.HasValue) parts.Add($"paymentId={request.PaymentId}");
        if (request.DestinationId.HasValue) parts.Add($"destinationId={request.DestinationId}");
        if (request.FeeTypeId.HasValue) parts.Add($"feeTypeId={request.FeeTypeId}");
        if (request.SectionId.HasValue) parts.Add($"sectionId={request.SectionId}");
        if (request.ClassRoomId.HasValue) parts.Add($"classRoomId={request.ClassRoomId}");
        return string.Join("&", parts);
    }
}

public sealed class WithholdingApiService : ApiServiceBase, IWithholdingApiService
{
    public WithholdingApiService(IHttpClientFactory httpClientFactory) : base(httpClientFactory) { }

    public Task<IReadOnlyList<SchoolManagement.Application.Withholdings.DTOs.WithholdingTypeDto>> GetTypesAsync(
        bool activeOnly = false,
        CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<SchoolManagement.Application.Withholdings.DTOs.WithholdingTypeDto>>(
            $"api/v1/withholdings/types?activeOnly={activeOnly}", cancellationToken);

    public Task<SchoolManagement.Application.Withholdings.DTOs.WithholdingTypeDto> CreateTypeAsync(
        SchoolManagement.Application.Withholdings.DTOs.SaveWithholdingTypeRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.Withholdings.DTOs.WithholdingTypeDto>(
            "api/v1/withholdings/types", request, cancellationToken);

    public Task<SchoolManagement.Application.Withholdings.DTOs.WithholdingTypeDto> UpdateTypeAsync(
        Guid id,
        SchoolManagement.Application.Withholdings.DTOs.SaveWithholdingTypeRequest request,
        CancellationToken cancellationToken = default) =>
        PutAsync<SchoolManagement.Application.Withholdings.DTOs.WithholdingTypeDto>(
            $"api/v1/withholdings/types/{id}", request, cancellationToken);

    public Task DeactivateTypeAsync(Guid id, CancellationToken cancellationToken = default) =>
        PostAsync<object>($"api/v1/withholdings/types/{id}/deactivate", new { }, cancellationToken);

    public Task<SchoolManagement.Application.Withholdings.DTOs.WithholdingConfigurationSearchResultDto> SearchConfigurationsAsync(
        SchoolManagement.Application.Withholdings.DTOs.WithholdingConfigurationSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var parts = new List<string>
        {
            $"page={request.Page}",
            $"pageSize={request.PageSize}"
        };
        if (request.AcademicYearId.HasValue) parts.Add($"academicYearId={request.AcademicYearId}");
        if (request.WithholdingTypeId.HasValue) parts.Add($"withholdingTypeId={request.WithholdingTypeId}");
        if (request.FeeTypeId.HasValue) parts.Add($"feeTypeId={request.FeeTypeId}");
        if (request.FeeInstallmentId.HasValue) parts.Add($"feeInstallmentId={request.FeeInstallmentId}");
        if (request.PricingCategoryId.HasValue) parts.Add($"pricingCategoryId={request.PricingCategoryId}");
        if (request.CalculationMode.HasValue) parts.Add($"calculationMode={request.CalculationMode}");
        if (request.ActiveOnly.HasValue) parts.Add($"activeOnly={request.ActiveOnly}");
        if (!string.IsNullOrWhiteSpace(request.Search)) parts.Add($"search={Uri.EscapeDataString(request.Search)}");

        return GetAsync<SchoolManagement.Application.Withholdings.DTOs.WithholdingConfigurationSearchResultDto>(
            $"api/v1/withholdings/configurations?{string.Join("&", parts)}", cancellationToken);
    }

    public Task<SchoolManagement.Application.Withholdings.DTOs.WithholdingConfigurationDto> CreateConfigurationAsync(
        SchoolManagement.Application.Withholdings.DTOs.SaveWithholdingConfigurationRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.Withholdings.DTOs.WithholdingConfigurationDto>(
            "api/v1/withholdings/configurations", request, cancellationToken);

    public Task<SchoolManagement.Application.Withholdings.DTOs.WithholdingConfigurationDto> UpdateConfigurationAsync(
        Guid id,
        SchoolManagement.Application.Withholdings.DTOs.SaveWithholdingConfigurationRequest request,
        CancellationToken cancellationToken = default) =>
        PutAsync<SchoolManagement.Application.Withholdings.DTOs.WithholdingConfigurationDto>(
            $"api/v1/withholdings/configurations/{id}", request, cancellationToken);

    public Task DeactivateConfigurationAsync(Guid id, CancellationToken cancellationToken = default) =>
        PostAsync<object>($"api/v1/withholdings/configurations/{id}/deactivate", new { }, cancellationToken);

    public Task DeleteConfigurationAsync(Guid id, CancellationToken cancellationToken = default) =>
        DeleteAsync($"api/v1/withholdings/configurations/{id}", cancellationToken);

    public Task<SchoolManagement.Application.Withholdings.DTOs.WithholdingCalculationResult> CalculateAsync(
        SchoolManagement.Application.Withholdings.DTOs.WithholdingCalculateRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.Withholdings.DTOs.WithholdingCalculationResult>(
            "api/v1/withholdings/calculate", request, cancellationToken);

    public async Task<byte[]> ExportExcelAsync(
        SchoolManagement.Application.Withholdings.DTOs.WithholdingConfigurationSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = BuildConfigQuery(request);
        var client = HttpClientFactory.CreateClient("SchoolApiAuth");
        var response = await client.GetAsync($"api/v1/withholdings/configurations/export/excel?{query}", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Erreur export Excel ({(int)response.StatusCode})");
        }

        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    public async Task<byte[]> ExportPdfAsync(
        SchoolManagement.Application.Withholdings.DTOs.WithholdingConfigurationSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = BuildConfigQuery(request);
        var client = HttpClientFactory.CreateClient("SchoolApiAuth");
        var response = await client.GetAsync($"api/v1/withholdings/configurations/export/pdf?{query}", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Erreur export PDF ({(int)response.StatusCode})");
        }

        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    private static string BuildConfigQuery(SchoolManagement.Application.Withholdings.DTOs.WithholdingConfigurationSearchRequest request)
    {
        var parts = new List<string>();
        if (request.AcademicYearId.HasValue) parts.Add($"academicYearId={request.AcademicYearId}");
        if (request.WithholdingTypeId.HasValue) parts.Add($"withholdingTypeId={request.WithholdingTypeId}");
        if (request.FeeTypeId.HasValue) parts.Add($"feeTypeId={request.FeeTypeId}");
        if (request.FeeInstallmentId.HasValue) parts.Add($"feeInstallmentId={request.FeeInstallmentId}");
        if (request.PricingCategoryId.HasValue) parts.Add($"pricingCategoryId={request.PricingCategoryId}");
        if (request.CalculationMode.HasValue) parts.Add($"calculationMode={request.CalculationMode}");
        if (request.ActiveOnly.HasValue) parts.Add($"activeOnly={request.ActiveOnly}");
        if (!string.IsNullOrWhiteSpace(request.Search)) parts.Add($"search={Uri.EscapeDataString(request.Search)}");
        return string.Join("&", parts);
    }
}

public sealed class CurrencyApiService : ApiServiceBase, ICurrencyApiService
{
    public CurrencyApiService(IHttpClientFactory httpClientFactory) : base(httpClientFactory) { }

    public Task<IReadOnlyList<SchoolManagement.Application.CurrencyManagement.DTOs.CurrencyDefinitionDto>> SearchCurrenciesAsync(
        string? search = null,
        bool? activeOnly = null,
        CancellationToken cancellationToken = default)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(search)) parts.Add($"search={Uri.EscapeDataString(search)}");
        if (activeOnly.HasValue) parts.Add($"activeOnly={activeOnly.Value}");
        var q = parts.Count == 0 ? string.Empty : "?" + string.Join("&", parts);
        return GetAsync<IReadOnlyList<SchoolManagement.Application.CurrencyManagement.DTOs.CurrencyDefinitionDto>>(
            $"api/v1/currencies{q}", cancellationToken);
    }

    public Task<SchoolManagement.Application.CurrencyManagement.DTOs.CurrencyDefinitionDto> CreateCurrencyAsync(
        SchoolManagement.Application.CurrencyManagement.DTOs.SaveCurrencyDefinitionRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.CurrencyManagement.DTOs.CurrencyDefinitionDto>(
            "api/v1/currencies", request, cancellationToken);

    public Task<SchoolManagement.Application.CurrencyManagement.DTOs.CurrencyDefinitionDto> UpdateCurrencyAsync(
        Guid id,
        SchoolManagement.Application.CurrencyManagement.DTOs.SaveCurrencyDefinitionRequest request,
        CancellationToken cancellationToken = default) =>
        PutAsync<SchoolManagement.Application.CurrencyManagement.DTOs.CurrencyDefinitionDto>(
            $"api/v1/currencies/{id}", request, cancellationToken);

    public Task SetCurrencyActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default) =>
        PostAsync<object>(
            $"api/v1/currencies/{id}/{(isActive ? "activate" : "deactivate")}",
            new { },
            cancellationToken);

    public Task<IReadOnlyList<SchoolManagement.Application.CurrencyManagement.DTOs.SchoolCurrencyDto>> GetSchoolCurrenciesAsync(
        bool paymentOnly = false,
        CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<SchoolManagement.Application.CurrencyManagement.DTOs.SchoolCurrencyDto>>(
            $"api/v1/school-currencies?paymentOnly={paymentOnly}", cancellationToken);

    public Task<SchoolManagement.Application.CurrencyManagement.DTOs.SchoolCurrencyDto> UpsertSchoolCurrencyAsync(
        SchoolManagement.Application.CurrencyManagement.DTOs.SaveSchoolCurrencyRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.CurrencyManagement.DTOs.SchoolCurrencyDto>(
            "api/v1/school-currencies", request, cancellationToken);

    public Task RemoveSchoolCurrencyAsync(Guid id, CancellationToken cancellationToken = default) =>
        DeleteAsync($"api/v1/school-currencies/{id}", cancellationToken);

    public Task<IReadOnlyList<SchoolManagement.Application.CurrencyManagement.DTOs.ExchangeRateTypeDto>> GetRateTypesAsync(
        bool activeOnly = false,
        CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<SchoolManagement.Application.CurrencyManagement.DTOs.ExchangeRateTypeDto>>(
            $"api/v1/exchange-rate-types?activeOnly={activeOnly}", cancellationToken);

    public Task<SchoolManagement.Application.CurrencyManagement.DTOs.ExchangeRateTypeDto> CreateRateTypeAsync(
        SchoolManagement.Application.CurrencyManagement.DTOs.SaveExchangeRateTypeRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.CurrencyManagement.DTOs.ExchangeRateTypeDto>(
            "api/v1/exchange-rate-types", request, cancellationToken);

    public Task<IReadOnlyList<SchoolManagement.Application.CurrencyManagement.DTOs.ExchangeRateDto>> SearchExchangeRatesAsync(
        Guid? sourceCurrencyId = null,
        Guid? targetCurrencyId = null,
        Guid? rateTypeId = null,
        bool? activeOnly = null,
        CancellationToken cancellationToken = default)
    {
        var parts = new List<string>();
        if (sourceCurrencyId.HasValue) parts.Add($"sourceCurrencyId={sourceCurrencyId}");
        if (targetCurrencyId.HasValue) parts.Add($"targetCurrencyId={targetCurrencyId}");
        if (rateTypeId.HasValue) parts.Add($"rateTypeId={rateTypeId}");
        if (activeOnly.HasValue) parts.Add($"activeOnly={activeOnly}");
        var q = parts.Count == 0 ? string.Empty : "?" + string.Join("&", parts);
        return GetAsync<IReadOnlyList<SchoolManagement.Application.CurrencyManagement.DTOs.ExchangeRateDto>>(
            $"api/v1/exchange-rates{q}", cancellationToken);
    }

    public Task<SchoolManagement.Application.CurrencyManagement.DTOs.ExchangeRateDto> CreateExchangeRateAsync(
        SchoolManagement.Application.CurrencyManagement.DTOs.SaveExchangeRateRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.CurrencyManagement.DTOs.ExchangeRateDto>(
            "api/v1/exchange-rates", request, cancellationToken);

    public Task<SchoolManagement.Application.CurrencyManagement.DTOs.ExchangeRateDto> UpdateExchangeRateAsync(
        Guid id,
        SchoolManagement.Application.CurrencyManagement.DTOs.SaveExchangeRateRequest request,
        CancellationToken cancellationToken = default) =>
        PutAsync<SchoolManagement.Application.CurrencyManagement.DTOs.ExchangeRateDto>(
            $"api/v1/exchange-rates/{id}", request, cancellationToken);

    public Task ActivateExchangeRateAsync(Guid id, CancellationToken cancellationToken = default) =>
        PostAsync<object>($"api/v1/exchange-rates/{id}/activate", new { }, cancellationToken);

    public Task DeactivateExchangeRateAsync(Guid id, CancellationToken cancellationToken = default) =>
        PostAsync<object>($"api/v1/exchange-rates/{id}/deactivate", new { }, cancellationToken);

    public Task<IReadOnlyList<SchoolManagement.Application.CurrencyManagement.DTOs.ExchangeRateHistoryDto>> GetHistoryAsync(
        Guid? exchangeRateId = null,
        int take = 200,
        CancellationToken cancellationToken = default)
    {
        var parts = new List<string> { $"take={take}" };
        if (exchangeRateId.HasValue) parts.Add($"exchangeRateId={exchangeRateId}");
        return GetAsync<IReadOnlyList<SchoolManagement.Application.CurrencyManagement.DTOs.ExchangeRateHistoryDto>>(
            $"api/v1/exchange-rates/history?{string.Join("&", parts)}", cancellationToken);
    }

    public Task<SchoolManagement.Application.CurrencyManagement.DTOs.CurrencyConversionResultDto> ConvertAsync(
        SchoolManagement.Application.CurrencyManagement.DTOs.CurrencyConversionRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.CurrencyManagement.DTOs.CurrencyConversionResultDto>(
            "api/v1/exchange-rates/convert", request, cancellationToken);
}

public sealed class StudentCardApiService : ApiServiceBase, IStudentCardApiService
{
    public StudentCardApiService(IHttpClientFactory httpClientFactory) : base(httpClientFactory) { }

    public Task<SchoolManagement.Application.StudentCards.DTOs.StudentCardDashboardDto> GetDashboardAsync(
        Guid? academicYearId = null,
        CancellationToken cancellationToken = default)
    {
        var q = academicYearId.HasValue ? $"?academicYearId={academicYearId}" : string.Empty;
        return GetAsync<SchoolManagement.Application.StudentCards.DTOs.StudentCardDashboardDto>(
            $"api/v1/cards/dashboard{q}", cancellationToken);
    }

    public Task<SchoolManagement.Shared.Models.PagedResult<SchoolManagement.Application.StudentCards.DTOs.StudentCardListItemDto>> SearchAsync(
        SchoolManagement.Application.StudentCards.DTOs.StudentCardSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var parts = new List<string>
        {
            $"page={request.Page}",
            $"pageSize={request.PageSize}"
        };
        if (request.AcademicYearId.HasValue) parts.Add($"academicYearId={request.AcademicYearId}");
        if (request.ClassRoomId.HasValue) parts.Add($"classRoomId={request.ClassRoomId}");
        if (request.SectionId.HasValue) parts.Add($"sectionId={request.SectionId}");
        if (request.Status.HasValue) parts.Add($"status={request.Status}");
        if (!string.IsNullOrWhiteSpace(request.Search)) parts.Add($"search={Uri.EscapeDataString(request.Search)}");
        return GetAsync<SchoolManagement.Shared.Models.PagedResult<SchoolManagement.Application.StudentCards.DTOs.StudentCardListItemDto>>(
            $"api/v1/cards?{string.Join("&", parts)}", cancellationToken);
    }

    public Task<SchoolManagement.Application.StudentCards.DTOs.StudentCardDetailDto> GetByIdAsync(
        Guid cardId,
        CancellationToken cancellationToken = default) =>
        GetAsync<SchoolManagement.Application.StudentCards.DTOs.StudentCardDetailDto>(
            $"api/v1/cards/{cardId}", cancellationToken);

    public Task<SchoolManagement.Application.StudentCards.DTOs.StudentCardDetailDto> CreateAsync(
        SchoolManagement.Application.StudentCards.DTOs.CreateStudentCardRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.StudentCards.DTOs.StudentCardDetailDto>(
            "api/v1/cards", request, cancellationToken);

    public Task<SchoolManagement.Application.StudentCards.DTOs.BulkCreateStudentCardsResult> BulkCreateAsync(
        SchoolManagement.Application.StudentCards.DTOs.BulkCreateStudentCardsRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.StudentCards.DTOs.BulkCreateStudentCardsResult>(
            "api/v1/cards/bulk", request, cancellationToken);

    public Task<SchoolManagement.Application.StudentCards.DTOs.PrintStudentCardsResult> PrintAsync(
        SchoolManagement.Application.StudentCards.DTOs.PrintStudentCardsRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.StudentCards.DTOs.PrintStudentCardsResult>(
            "api/v1/cards/print", request, cancellationToken);

    public Task<SchoolManagement.Application.StudentCards.DTOs.StudentCardDetailDto> ReprintAsync(
        Guid cardId,
        SchoolManagement.Application.StudentCards.DTOs.ReprintStudentCardRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.StudentCards.DTOs.StudentCardDetailDto>(
            $"api/v1/cards/{cardId}/reprint", request, cancellationToken);

    public Task<SchoolManagement.Application.StudentCards.DTOs.StudentCardDetailDto> RenewAsync(
        Guid cardId,
        SchoolManagement.Application.StudentCards.DTOs.RenewStudentCardRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.StudentCards.DTOs.StudentCardDetailDto>(
            $"api/v1/cards/{cardId}/renew", request, cancellationToken);

    public Task<SchoolManagement.Application.StudentCards.DTOs.StudentCardDetailDto> DeclareLostAsync(
        Guid cardId,
        SchoolManagement.Application.StudentCards.DTOs.DeclareCardIncidentRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.StudentCards.DTOs.StudentCardDetailDto>(
            $"api/v1/cards/{cardId}/lost", request, cancellationToken);

    public Task<SchoolManagement.Application.StudentCards.DTOs.StudentCardDetailDto> DeclareStolenAsync(
        Guid cardId,
        SchoolManagement.Application.StudentCards.DTOs.DeclareCardIncidentRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.StudentCards.DTOs.StudentCardDetailDto>(
            $"api/v1/cards/{cardId}/stolen", request, cancellationToken);

    public Task<SchoolManagement.Application.StudentCards.DTOs.StudentCardDetailDto> DeactivateAsync(
        Guid cardId,
        SchoolManagement.Application.StudentCards.DTOs.DeactivateStudentCardRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.StudentCards.DTOs.StudentCardDetailDto>(
            $"api/v1/cards/{cardId}/deactivate", request, cancellationToken);

    public Task<SchoolManagement.Application.StudentCards.DTOs.StudentCardDetailDto> ActivateAsync(
        Guid cardId,
        SchoolManagement.Application.StudentCards.DTOs.ActivateStudentCardRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.StudentCards.DTOs.StudentCardDetailDto>(
            $"api/v1/cards/{cardId}/activate", request, cancellationToken);

    public Task<SchoolManagement.Application.StudentCards.DTOs.StudentCardDetailDto> SuspendAsync(
        Guid cardId,
        SchoolManagement.Application.StudentCards.DTOs.SuspendStudentCardRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.StudentCards.DTOs.StudentCardDetailDto>(
            $"api/v1/cards/{cardId}/suspend", request, cancellationToken);

    public Task SoftDeleteAsync(Guid cardId, CancellationToken cancellationToken = default) =>
        DeleteAsync($"api/v1/cards/{cardId}", cancellationToken);

    public Task<IReadOnlyList<SchoolManagement.Application.StudentCards.DTOs.CardTemplateDto>> ListTemplatesAsync(
        bool activeOnly = false,
        CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<SchoolManagement.Application.StudentCards.DTOs.CardTemplateDto>>(
            $"api/v1/card-templates?activeOnly={activeOnly}", cancellationToken);

    public Task<SchoolManagement.Application.StudentCards.DTOs.CardTemplateDto> CreateTemplateAsync(
        SchoolManagement.Application.StudentCards.DTOs.SaveCardTemplateRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.StudentCards.DTOs.CardTemplateDto>(
            "api/v1/card-templates", request, cancellationToken);

    public Task<SchoolManagement.Application.StudentCards.DTOs.CardTemplateDto> UpdateTemplateAsync(
        Guid templateId,
        SchoolManagement.Application.StudentCards.DTOs.SaveCardTemplateRequest request,
        CancellationToken cancellationToken = default) =>
        PutAsync<SchoolManagement.Application.StudentCards.DTOs.CardTemplateDto>(
            $"api/v1/card-templates/{templateId}", request, cancellationToken);

    public Task<SchoolManagement.Application.StudentCards.DTOs.CardSchoolSettingsDto> GetSettingsAsync(
        CancellationToken cancellationToken = default) =>
        GetAsync<SchoolManagement.Application.StudentCards.DTOs.CardSchoolSettingsDto>(
            "api/v1/cards/settings", cancellationToken);

    public Task<SchoolManagement.Application.StudentCards.DTOs.CardSchoolSettingsDto> SaveSettingsAsync(
        SchoolManagement.Application.StudentCards.DTOs.SaveCardSchoolSettingsRequest request,
        CancellationToken cancellationToken = default) =>
        PutAsync<SchoolManagement.Application.StudentCards.DTOs.CardSchoolSettingsDto>(
            "api/v1/cards/settings", request, cancellationToken);
}

public sealed class FinanceApiService : ApiServiceBase, IFinanceApiService
{
    public FinanceApiService(IHttpClientFactory httpClientFactory) : base(httpClientFactory) { }

    public Task<SchoolManagement.Application.Finance.DTOs.StudentPaymentSituationSearchResultDto> SearchPaymentSituationsAsync(
        SchoolManagement.Application.Finance.DTOs.StudentPaymentSituationSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var parts = new List<string>
        {
            $"page={request.Page}",
            $"pageSize={request.PageSize}"
        };
        if (request.AcademicYearId.HasValue) parts.Add($"academicYearId={request.AcademicYearId}");
        if (request.SectionId.HasValue) parts.Add($"sectionId={request.SectionId}");
        if (request.PedagogicalClassId.HasValue) parts.Add($"pedagogicalClassId={request.PedagogicalClassId}");
        if (request.ClassRoomId.HasValue) parts.Add($"classRoomId={request.ClassRoomId}");
        if (request.FeePricingCategoryId.HasValue) parts.Add($"feePricingCategoryId={request.FeePricingCategoryId}");
        if (request.FeeTypeId.HasValue) parts.Add($"feeTypeId={request.FeeTypeId}");
        if (request.PaymentStatus.HasValue) parts.Add($"paymentStatus={request.PaymentStatus}");
        if (!string.IsNullOrWhiteSpace(request.Search)) parts.Add($"search={Uri.EscapeDataString(request.Search)}");

        return GetAsync<SchoolManagement.Application.Finance.DTOs.StudentPaymentSituationSearchResultDto>(
            $"api/v1/finance/payment-situations?{string.Join("&", parts)}", cancellationToken);
    }

    public Task<SchoolManagement.Application.Finance.DTOs.StudentInstallmentPaymentPlanDto> GetInstallmentPaymentPlanAsync(
        Guid enrollmentId,
        Guid feeTypeId,
        CancellationToken cancellationToken = default) =>
        GetAsync<SchoolManagement.Application.Finance.DTOs.StudentInstallmentPaymentPlanDto>(
            $"api/v1/finance/payment-situations/{enrollmentId}/installment-plan?feeTypeId={feeTypeId}",
            cancellationToken);

    public Task<SchoolManagement.Application.Finance.DTOs.StudentPricingAssignmentSearchResultDto> SearchPricingAssignmentsAsync(
        SchoolManagement.Application.Finance.DTOs.StudentPricingAssignmentSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var parts = new List<string>
        {
            $"page={request.Page}",
            $"pageSize={request.PageSize}"
        };
        if (request.AcademicYearId.HasValue) parts.Add($"academicYearId={request.AcademicYearId}");
        if (request.SectionId.HasValue) parts.Add($"sectionId={request.SectionId}");
        if (request.PedagogicalClassId.HasValue) parts.Add($"pedagogicalClassId={request.PedagogicalClassId}");
        if (request.ClassRoomId.HasValue) parts.Add($"classRoomId={request.ClassRoomId}");
        if (request.FeePricingCategoryId.HasValue) parts.Add($"feePricingCategoryId={request.FeePricingCategoryId}");
        if (!string.IsNullOrWhiteSpace(request.Search)) parts.Add($"search={Uri.EscapeDataString(request.Search)}");

        return GetAsync<SchoolManagement.Application.Finance.DTOs.StudentPricingAssignmentSearchResultDto>(
            $"api/v1/finance/pricing-assignments?{string.Join("&", parts)}", cancellationToken);
    }

    public Task<SchoolManagement.Application.Finance.DTOs.StudentPricingAssignmentDto> UpdatePricingAssignmentAsync(
        Guid enrollmentId,
        SchoolManagement.Application.Finance.DTOs.UpdateEnrollmentPricingCategoryRequest request,
        CancellationToken cancellationToken = default) =>
        PutAsync<SchoolManagement.Application.Finance.DTOs.StudentPricingAssignmentDto>(
            $"api/v1/finance/pricing-assignments/{enrollmentId}", request, cancellationToken);

    public Task<IReadOnlyList<SchoolManagement.Application.Finance.DTOs.PricingCategoryHistoryLineDto>> GetPricingCategoryHistoryAsync(
        Guid enrollmentId,
        CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<SchoolManagement.Application.Finance.DTOs.PricingCategoryHistoryLineDto>>(
            $"api/v1/finance/pricing-assignments/{enrollmentId}/history", cancellationToken);

    public Task<SchoolManagement.Application.Finance.DTOs.StudentApplicableFeesDto> GetApplicableFeesAsync(
        Guid enrollmentId,
        CancellationToken cancellationToken = default) =>
        GetAsync<SchoolManagement.Application.Finance.DTOs.StudentApplicableFeesDto>(
            $"api/v1/finance/pricing-assignments/{enrollmentId}/applicable-fees", cancellationToken);
}

public sealed class GradeApiService : ApiServiceBase, IGradeApiService
{
    public GradeApiService(IHttpClientFactory httpClientFactory) : base(httpClientFactory) { }

    public Task<IReadOnlyList<SchoolManagement.Application.Grades.DTOs.EvaluationTypeDto>> GetEvaluationTypesAsync(
        CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<SchoolManagement.Application.Grades.DTOs.EvaluationTypeDto>>(
            "api/v1/grades/evaluation-types", cancellationToken);

    public Task<SchoolManagement.Application.Grades.DTOs.CotationSessionDto> OpenCotationSessionAsync(
        SchoolManagement.Application.Grades.DTOs.OpenCotationSessionRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.Grades.DTOs.CotationSessionDto>(
            "api/v1/grades/cotation/session", request, cancellationToken);

    public Task<IReadOnlyList<SchoolManagement.Application.Grades.DTOs.CotationPeriodDto>> GetCotationPeriodsAsync(
        Guid academicYearId,
        Guid classRoomId,
        CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<SchoolManagement.Application.Grades.DTOs.CotationPeriodDto>>(
            $"api/v1/grades/cotation/periods?academicYearId={academicYearId}&classRoomId={classRoomId}",
            cancellationToken);

    public Task<IReadOnlyList<SchoolManagement.Application.Grades.DTOs.CotationAssignmentDto>> GetCotationAssignmentsAsync(
        Guid academicYearId,
        Guid teacherId,
        CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<SchoolManagement.Application.Grades.DTOs.CotationAssignmentDto>>(
            $"api/v1/grades/cotation/assignments?academicYearId={academicYearId}&teacherId={teacherId}",
            cancellationToken);

    public Task<SchoolManagement.Application.Grades.DTOs.GlobalCotationGridDto> GetGlobalCotationGridAsync(
        Guid academicYearId,
        Guid classRoomId,
        Guid teacherId,
        CancellationToken cancellationToken = default) =>
        GetAsync<SchoolManagement.Application.Grades.DTOs.GlobalCotationGridDto>(
            $"api/v1/grades/cotation/global-grid?academicYearId={academicYearId}&classRoomId={classRoomId}&teacherId={teacherId}",
            cancellationToken);

    public Task<SchoolManagement.Application.Grades.DTOs.SaveGlobalCotationResultDto> SaveGlobalCotationAsync(
        SchoolManagement.Application.Grades.DTOs.SaveGlobalCotationRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.Grades.DTOs.SaveGlobalCotationResultDto>(
            "api/v1/grades/cotation/global-save",
            request,
            cancellationToken);

    public Task<IReadOnlyList<SchoolManagement.Application.Grades.DTOs.GlobalCotationSessionSummaryDto>> GetGlobalCotationSessionsAsync(
        Guid academicYearId,
        Guid classRoomId,
        Guid academicPeriodId,
        Guid teacherId,
        CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<SchoolManagement.Application.Grades.DTOs.GlobalCotationSessionSummaryDto>>(
            $"api/v1/grades/cotation/global-sessions?academicYearId={academicYearId}&classRoomId={classRoomId}&academicPeriodId={academicPeriodId}&teacherId={teacherId}",
            cancellationToken);

    public Task<SchoolManagement.Application.Grades.DTOs.GlobalCotationSessionLoadDto> LoadGlobalCotationSessionAsync(
        Guid academicYearId,
        Guid classRoomId,
        Guid academicPeriodId,
        Guid teacherId,
        Guid evaluationTypeId,
        string title,
        CancellationToken cancellationToken = default) =>
        GetAsync<SchoolManagement.Application.Grades.DTOs.GlobalCotationSessionLoadDto>(
            $"api/v1/grades/cotation/global-session?academicYearId={academicYearId}&classRoomId={classRoomId}&academicPeriodId={academicPeriodId}&teacherId={teacherId}&evaluationTypeId={evaluationTypeId}&title={Uri.EscapeDataString(title)}",
            cancellationToken);

    public Task<SchoolManagement.Application.Grades.DTOs.CourseNotesGridDto> GetCourseNotesGridAsync(
        Guid academicYearId,
        Guid classRoomId,
        Guid courseId,
        Guid academicPeriodId,
        CancellationToken cancellationToken = default) =>
        GetAsync<SchoolManagement.Application.Grades.DTOs.CourseNotesGridDto>(
            $"api/v1/grades/cotation/course-notes-grid?academicYearId={academicYearId}&classRoomId={classRoomId}&courseId={courseId}&academicPeriodId={academicPeriodId}",
            cancellationToken);

    public Task<SchoolManagement.Application.Grades.DTOs.PedagogicalSheetContextDto> GetPedagogicalSheetContextAsync(
        Guid academicYearId,
        Guid classRoomId,
        CancellationToken cancellationToken = default) =>
        GetAsync<SchoolManagement.Application.Grades.DTOs.PedagogicalSheetContextDto>(
            $"api/v1/grades/cotation/pedagogical-sheet/context?academicYearId={academicYearId}&classRoomId={classRoomId}",
            cancellationToken);

    public Task<SchoolManagement.Application.Grades.DTOs.PedagogicalSheetDto> GetPedagogicalSheetAsync(
        Guid academicYearId,
        Guid classRoomId,
        SchoolManagement.Application.Grades.DTOs.PedagogicalSheetPeriodMode mode,
        Guid periodId,
        Guid teacherId,
        CancellationToken cancellationToken = default) =>
        GetAsync<SchoolManagement.Application.Grades.DTOs.PedagogicalSheetDto>(
            $"api/v1/grades/cotation/pedagogical-sheet?academicYearId={academicYearId}&classRoomId={classRoomId}&mode={(int)mode}&periodId={periodId}&teacherId={teacherId}",
            cancellationToken);

    public Task<SchoolManagement.Application.Grades.DTOs.ClassResultsSheetDto> GetClassResultsSheetAsync(
        Guid academicYearId,
        Guid classRoomId,
        SchoolManagement.Application.Grades.DTOs.PedagogicalSheetPeriodMode mode,
        Guid periodId,
        CancellationToken cancellationToken = default) =>
        GetAsync<SchoolManagement.Application.Grades.DTOs.ClassResultsSheetDto>(
            $"api/v1/grades/results/class-sheet?academicYearId={academicYearId}&classRoomId={classRoomId}&mode={(int)mode}&periodId={periodId}",
            cancellationToken);

    public Task<SchoolManagement.Application.Grades.DTOs.IndividualResultDto> GetIndividualResultAsync(
        Guid academicYearId,
        Guid classRoomId,
        Guid studentId,
        SchoolManagement.Application.Grades.DTOs.PedagogicalSheetPeriodMode mode,
        Guid periodId,
        CancellationToken cancellationToken = default) =>
        GetAsync<SchoolManagement.Application.Grades.DTOs.IndividualResultDto>(
            $"api/v1/grades/results/individual?academicYearId={academicYearId}&classRoomId={classRoomId}&studentId={studentId}&mode={(int)mode}&periodId={periodId}",
            cancellationToken);

    public Task<IReadOnlyList<SchoolManagement.Application.Grades.DTOs.EvaluationDto>> GetEvaluationsAsync(
        Guid classRoomId, Guid academicPeriodId, CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<SchoolManagement.Application.Grades.DTOs.EvaluationDto>>(
            $"api/v1/grades/evaluations?classRoomId={classRoomId}&academicPeriodId={academicPeriodId}", cancellationToken);

    public Task<SchoolManagement.Application.Grades.DTOs.EvaluationDto> CreateEvaluationAsync(
        SchoolManagement.Application.Grades.DTOs.CreateEvaluationRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.Grades.DTOs.EvaluationDto>("api/v1/grades/evaluations", request, cancellationToken);

    public Task<SchoolManagement.Application.Grades.DTOs.EvaluationDto> UpdateEvaluationAsync(
        Guid evaluationId,
        SchoolManagement.Application.Grades.DTOs.UpdateEvaluationRequest request,
        CancellationToken cancellationToken = default) =>
        PutAsync<SchoolManagement.Application.Grades.DTOs.EvaluationDto>(
            $"api/v1/grades/evaluations/{evaluationId}",
            request,
            cancellationToken);

    public Task DeleteEvaluationAsync(
        Guid evaluationId,
        CancellationToken cancellationToken = default) =>
        DeleteAsync($"api/v1/grades/evaluations/{evaluationId}", cancellationToken);

    public Task<IReadOnlyList<SchoolManagement.Application.Grades.DTOs.PeriodResultDto>> CalculateResultsAsync(
        SchoolManagement.Application.Grades.DTOs.CalculatePeriodResultsRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<IReadOnlyList<SchoolManagement.Application.Grades.DTOs.PeriodResultDto>>(
            "api/v1/grades/period-results/calculate", request, cancellationToken);

    public Task<IReadOnlyList<SchoolManagement.Application.Grades.DTOs.PeriodResultDto>> GetPeriodResultsAsync(
        Guid classRoomId,
        Guid academicPeriodId,
        CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<SchoolManagement.Application.Grades.DTOs.PeriodResultDto>>(
            $"api/v1/grades/period-results?classRoomId={classRoomId}&academicPeriodId={academicPeriodId}",
            cancellationToken);

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

/// <summary>Client Bulletins — pas de calcul ; délègue à l'API / ResultCalculationService.</summary>
public sealed class BulletinApiService : ApiServiceBase, IBulletinApiService
{
    public BulletinApiService(IHttpClientFactory httpClientFactory) : base(httpClientFactory) { }

    public Task<SchoolManagement.Application.Bulletins.DTOs.IndividualBulletinDto> GetIndividualBulletinAsync(
        SchoolManagement.Application.Bulletins.DTOs.IndividualBulletinRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.Bulletins.DTOs.IndividualBulletinDto>(
            "api/v1/bulletins/individual", request, cancellationToken);

    public Task<SchoolManagement.Application.Bulletins.DTOs.ClassBulletinsBatchDto> GetClassBulletinsAsync(
        SchoolManagement.Application.Bulletins.DTOs.ClassBulletinsRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.Bulletins.DTOs.ClassBulletinsBatchDto>(
            "api/v1/bulletins/class", request, cancellationToken);

    public Task<IReadOnlyList<SchoolManagement.Application.Bulletins.DTOs.BulletinPrintHistoryDto>> GetPrintHistoryAsync(
        Guid? academicYearId = null,
        Guid? classRoomId = null,
        Guid? studentId = null,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string>();
        if (academicYearId.HasValue)
        {
            query.Add($"academicYearId={academicYearId}");
        }

        if (classRoomId.HasValue)
        {
            query.Add($"classRoomId={classRoomId}");
        }

        if (studentId.HasValue)
        {
            query.Add($"studentId={studentId}");
        }

        var suffix = query.Count == 0 ? string.Empty : "?" + string.Join("&", query);
        return GetAsync<IReadOnlyList<SchoolManagement.Application.Bulletins.DTOs.BulletinPrintHistoryDto>>(
            $"api/v1/bulletins/print-history{suffix}", cancellationToken);
    }

    public Task<SchoolManagement.Application.Bulletins.DTOs.BulletinPrintHistoryDto> RecordPrintAsync(
        SchoolManagement.Application.Bulletins.DTOs.RecordBulletinPrintRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.Bulletins.DTOs.BulletinPrintHistoryDto>(
            "api/v1/bulletins/print-history", request, cancellationToken);
}

public sealed class ResultValidationApiService : ApiServiceBase, IResultValidationApiService
{
    public ResultValidationApiService(IHttpClientFactory httpClientFactory) : base(httpClientFactory) { }

    public Task<SchoolManagement.Application.ResultValidation.DTOs.ResultValidationSheetDto> GetSheetAsync(
        Guid academicYearId,
        Guid classRoomId,
        Guid academicPeriodId,
        CancellationToken cancellationToken = default) =>
        GetAsync<SchoolManagement.Application.ResultValidation.DTOs.ResultValidationSheetDto>(
            $"api/v1/result-validation/sheet?academicYearId={academicYearId}&classRoomId={classRoomId}&academicPeriodId={academicPeriodId}",
            cancellationToken);

    public Task<SchoolManagement.Application.ResultValidation.DTOs.ResultValidationReadinessDto> GetReadinessAsync(
        Guid academicYearId,
        Guid classRoomId,
        Guid academicPeriodId,
        CancellationToken cancellationToken = default) =>
        GetAsync<SchoolManagement.Application.ResultValidation.DTOs.ResultValidationReadinessDto>(
            $"api/v1/result-validation/readiness?academicYearId={academicYearId}&classRoomId={classRoomId}&academicPeriodId={academicPeriodId}",
            cancellationToken);

    public Task<SchoolManagement.Application.ResultValidation.DTOs.ResultValidationSheetDto> ValidateAsync(
        SchoolManagement.Application.ResultValidation.DTOs.ResultValidationActionRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.ResultValidation.DTOs.ResultValidationSheetDto>(
            "api/v1/result-validation/validate", request, cancellationToken);

    public Task<SchoolManagement.Application.ResultValidation.DTOs.ResultValidationSheetDto> CancelAsync(
        SchoolManagement.Application.ResultValidation.DTOs.ResultValidationActionRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.ResultValidation.DTOs.ResultValidationSheetDto>(
            "api/v1/result-validation/cancel", request, cancellationToken);

    public Task<SchoolManagement.Application.ResultValidation.DTOs.ResultValidationSheetDto> LockAsync(
        SchoolManagement.Application.ResultValidation.DTOs.ResultValidationActionRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.ResultValidation.DTOs.ResultValidationSheetDto>(
            "api/v1/result-validation/lock", request, cancellationToken);

    public Task<SchoolManagement.Application.ResultValidation.DTOs.ResultValidationSheetDto> UnlockAsync(
        SchoolManagement.Application.ResultValidation.DTOs.ResultValidationActionRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.ResultValidation.DTOs.ResultValidationSheetDto>(
            "api/v1/result-validation/unlock", request, cancellationToken);
}

public sealed class DeliberationApiService : ApiServiceBase, IDeliberationApiService
{
    public DeliberationApiService(IHttpClientFactory httpClientFactory) : base(httpClientFactory) { }

    public Task<SchoolManagement.Application.Deliberation.DTOs.DeliberationSheetDto> GetSheetAsync(
        Guid academicYearId,
        Guid classRoomId,
        Guid academicPeriodId,
        CancellationToken cancellationToken = default) =>
        GetAsync<SchoolManagement.Application.Deliberation.DTOs.DeliberationSheetDto>(
            $"api/v1/deliberation/sheet?academicYearId={academicYearId}&classRoomId={classRoomId}&academicPeriodId={academicPeriodId}",
            cancellationToken);

    public Task<SchoolManagement.Application.Deliberation.DTOs.DeliberationMinutesDto> GetMinutesAsync(
        Guid academicYearId,
        Guid classRoomId,
        Guid academicPeriodId,
        CancellationToken cancellationToken = default) =>
        GetAsync<SchoolManagement.Application.Deliberation.DTOs.DeliberationMinutesDto>(
            $"api/v1/deliberation/minutes?academicYearId={academicYearId}&classRoomId={classRoomId}&academicPeriodId={academicPeriodId}",
            cancellationToken);

    public Task<SchoolManagement.Application.Deliberation.DTOs.DeliberationMinutesDto> SaveMinutesAsync(
        SchoolManagement.Application.Deliberation.DTOs.SaveDeliberationMinutesRequest request,
        CancellationToken cancellationToken = default) =>
        PutAsync<SchoolManagement.Application.Deliberation.DTOs.DeliberationMinutesDto>(
            "api/v1/deliberation/minutes", request, cancellationToken);

    public Task<SchoolManagement.Application.Deliberation.DTOs.DeliberationDecisionDialogDto> GetDecisionAsync(
        Guid academicYearId,
        Guid classRoomId,
        Guid academicPeriodId,
        Guid studentId,
        CancellationToken cancellationToken = default) =>
        GetAsync<SchoolManagement.Application.Deliberation.DTOs.DeliberationDecisionDialogDto>(
            $"api/v1/deliberation/decision?academicYearId={academicYearId}&classRoomId={classRoomId}&academicPeriodId={academicPeriodId}&studentId={studentId}",
            cancellationToken);

    public Task<SchoolManagement.Application.Deliberation.DTOs.DeliberationDecisionDialogDto> SaveDecisionAsync(
        SchoolManagement.Application.Deliberation.DTOs.SaveDeliberationDecisionRequest request,
        CancellationToken cancellationToken = default) =>
        PutAsync<SchoolManagement.Application.Deliberation.DTOs.DeliberationDecisionDialogDto>(
            "api/v1/deliberation/decision", request, cancellationToken);

    public Task<SchoolManagement.Application.Deliberation.DTOs.DeliberationSheetDto> SaveConductAsync(
        SchoolManagement.Application.Deliberation.DTOs.SaveStudentConductRequest request,
        CancellationToken cancellationToken = default) =>
        PutAsync<SchoolManagement.Application.Deliberation.DTOs.DeliberationSheetDto>(
            "api/v1/deliberation/conduct", request, cancellationToken);

    public Task<SchoolManagement.Application.Deliberation.DTOs.DeliberationSheetDto> SaveBonusAsync(
        SchoolManagement.Application.Deliberation.DTOs.SavePedagogicalBonusRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.Deliberation.DTOs.DeliberationSheetDto>(
            "api/v1/deliberation/bonus", request, cancellationToken);

    public Task<SchoolManagement.Application.Deliberation.DTOs.PedagogicalBonusDialogDto> GetBonusDialogAsync(
        Guid academicYearId,
        Guid classRoomId,
        Guid academicPeriodId,
        Guid studentId,
        CancellationToken cancellationToken = default) =>
        GetAsync<SchoolManagement.Application.Deliberation.DTOs.PedagogicalBonusDialogDto>(
            $"api/v1/deliberation/bonus-dialog?academicYearId={academicYearId}&classRoomId={classRoomId}&academicPeriodId={academicPeriodId}&studentId={studentId}",
            cancellationToken);

    public Task<SchoolManagement.Application.Deliberation.DTOs.ValidateDeliberationClassResultDto> ValidateClassAsync(
        SchoolManagement.Application.Deliberation.DTOs.ValidateDeliberationClassRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.Deliberation.DTOs.ValidateDeliberationClassResultDto>(
            "api/v1/deliberation/validate-class", request, cancellationToken);

    public Task<SchoolManagement.Application.Deliberation.DTOs.ValidateDeliberationClassResultDto> CancelClassValidationAsync(
        SchoolManagement.Application.Deliberation.DTOs.ValidateDeliberationClassRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.Deliberation.DTOs.ValidateDeliberationClassResultDto>(
            "api/v1/deliberation/cancel-class-validation", request, cancellationToken);
}

public sealed class MentionsApiService : ApiServiceBase, IMentionsApiService
{
    public MentionsApiService(IHttpClientFactory httpClientFactory) : base(httpClientFactory) { }

    public Task<IReadOnlyList<SchoolManagement.Application.Mentions.DTOs.ResultMentionDto>> GetAllAsync(
        CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<SchoolManagement.Application.Mentions.DTOs.ResultMentionDto>>(
            "api/v1/mentions", cancellationToken);

    public Task<SchoolManagement.Application.Mentions.DTOs.ResultMentionDto> CreateAsync(
        SchoolManagement.Application.Mentions.DTOs.CreateResultMentionRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.Mentions.DTOs.ResultMentionDto>(
            "api/v1/mentions", request, cancellationToken);

    public Task<SchoolManagement.Application.Mentions.DTOs.ResultMentionDto> UpdateAsync(
        Guid id,
        SchoolManagement.Application.Mentions.DTOs.UpdateResultMentionRequest request,
        CancellationToken cancellationToken = default) =>
        PutAsync<SchoolManagement.Application.Mentions.DTOs.ResultMentionDto>(
            $"api/v1/mentions/{id}", request, cancellationToken);

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        DeleteAsync($"api/v1/mentions/{id}", cancellationToken);
}

public sealed class PedagogicalPeriodApiService : ApiServiceBase, IPedagogicalPeriodApiService
{
    public PedagogicalPeriodApiService(IHttpClientFactory httpClientFactory) : base(httpClientFactory) { }

    public Task<SchoolManagement.Application.PedagogicalPeriods.DTOs.PedagogicalPeriodStructureDto> GetStructureAsync(
        Guid academicYearId,
        CancellationToken cancellationToken = default) =>
        GetAsync<SchoolManagement.Application.PedagogicalPeriods.DTOs.PedagogicalPeriodStructureDto>(
            $"api/v1/pedagogical-periods/structure?academicYearId={academicYearId}", cancellationToken);

    public Task<SchoolManagement.Application.PedagogicalPeriods.DTOs.PedagogicalPeriodStructureDto> CreateStructureAsync(
        SchoolManagement.Application.PedagogicalPeriods.DTOs.CreatePedagogicalStructureRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.PedagogicalPeriods.DTOs.PedagogicalPeriodStructureDto>(
            "api/v1/pedagogical-periods/structure", request, cancellationToken);

    public Task<SchoolManagement.Application.PedagogicalPeriods.DTOs.PedagogicalPeriodStructureDto> ProposeDatesAsync(
        Guid academicYearId,
        CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.PedagogicalPeriods.DTOs.PedagogicalPeriodStructureDto>(
            $"api/v1/pedagogical-periods/structure/propose-dates?academicYearId={academicYearId}",
            new { },
            cancellationToken);

    public Task<SchoolManagement.Application.PedagogicalPeriods.DTOs.PedagogicalSubPeriodDto> OpenSubPeriodAsync(
        Guid subPeriodId,
        SchoolManagement.Application.PedagogicalPeriods.DTOs.OpenSubPeriodRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.PedagogicalPeriods.DTOs.PedagogicalSubPeriodDto>(
            $"api/v1/pedagogical-periods/sub-periods/{subPeriodId}/open",
            request,
            cancellationToken);

    public Task<SchoolManagement.Application.PedagogicalPeriods.DTOs.PedagogicalSubPeriodDto> CloseSubPeriodAsync(
        Guid subPeriodId,
        CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.PedagogicalPeriods.DTOs.PedagogicalSubPeriodDto>(
            $"api/v1/pedagogical-periods/sub-periods/{subPeriodId}/close",
            new { },
            cancellationToken);

    public Task<SchoolManagement.Application.PedagogicalPeriods.DTOs.PedagogicalSubPeriodDto> LockSubPeriodAsync(
        Guid subPeriodId,
        CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.PedagogicalPeriods.DTOs.PedagogicalSubPeriodDto>(
            $"api/v1/pedagogical-periods/sub-periods/{subPeriodId}/lock",
            new { },
            cancellationToken);

    public Task<SchoolManagement.Application.PedagogicalPeriods.DTOs.PedagogicalSubPeriodDto> UnlockSubPeriodAsync(
        Guid subPeriodId,
        CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.PedagogicalPeriods.DTOs.PedagogicalSubPeriodDto>(
            $"api/v1/pedagogical-periods/sub-periods/{subPeriodId}/unlock",
            new { },
            cancellationToken);

    public Task<SchoolManagement.Application.PedagogicalPeriods.DTOs.PedagogicalSubPeriodDto> UpdateSettingsAsync(
        Guid subPeriodId,
        SchoolManagement.Application.PedagogicalPeriods.DTOs.UpdateSubPeriodSettingsRequest request,
        CancellationToken cancellationToken = default) =>
        PutAsync<SchoolManagement.Application.PedagogicalPeriods.DTOs.PedagogicalSubPeriodDto>(
            $"api/v1/pedagogical-periods/sub-periods/{subPeriodId}/settings",
            request,
            cancellationToken);

    public Task<SchoolManagement.Application.PedagogicalPeriods.DTOs.ActiveSubPeriodDto?> GetActiveAsync(
        Guid academicYearId,
        SchoolManagement.Domain.Enums.PedagogicalCycleGroup cycleGroup,
        CancellationToken cancellationToken = default) =>
        GetAsync<SchoolManagement.Application.PedagogicalPeriods.DTOs.ActiveSubPeriodDto?>(
            $"api/v1/pedagogical-periods/active?academicYearId={academicYearId}&cycleGroup={(int)cycleGroup}",
            cancellationToken);
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

    public Task<SchoolManagement.Application.Academic.DTOs.CourseDto> UpdateCourseAsync(
        Guid courseId,
        SchoolManagement.Application.Academic.DTOs.UpdateCourseRequest request,
        CancellationToken cancellationToken = default) =>
        PutAsync<SchoolManagement.Application.Academic.DTOs.CourseDto>(
            $"api/v1/academic/courses/{courseId}", request, cancellationToken);

    public Task DeleteCourseAsync(Guid courseId, CancellationToken cancellationToken = default) =>
        DeleteAsync($"api/v1/academic/courses/{courseId}", cancellationToken);

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

public sealed class PromoterDashboardApiService : ApiServiceBase, IPromoterDashboardApiService
{
    public PromoterDashboardApiService(IHttpClientFactory httpClientFactory) : base(httpClientFactory) { }

    public Task<SchoolManagement.Application.Dashboard.DTOs.PromoterDashboardOverviewDto> GetOverviewAsync(
        SchoolManagement.Application.Dashboard.DTOs.DashboardPeriod period = SchoolManagement.Application.Dashboard.DTOs.DashboardPeriod.Month,
        SchoolManagement.Application.Dashboard.DTOs.RevenueGranularity granularity = SchoolManagement.Application.Dashboard.DTOs.RevenueGranularity.Daily,
        Guid? feeTypeId = null,
        CancellationToken cancellationToken = default)
    {
        var url = $"api/v1/dashboard/overview?period={period}&granularity={granularity}";
        if (feeTypeId.HasValue) url += $"&feeTypeId={feeTypeId}";
        return GetAsync<SchoolManagement.Application.Dashboard.DTOs.PromoterDashboardOverviewDto>(url, cancellationToken);
    }

    public Task<IReadOnlyList<SchoolManagement.Application.Dashboard.DTOs.RevenuePointDto>> GetRevenueAsync(
        SchoolManagement.Application.Dashboard.DTOs.DashboardPeriod period = SchoolManagement.Application.Dashboard.DTOs.DashboardPeriod.Year,
        SchoolManagement.Application.Dashboard.DTOs.RevenueGranularity granularity = SchoolManagement.Application.Dashboard.DTOs.RevenueGranularity.Monthly,
        CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<SchoolManagement.Application.Dashboard.DTOs.RevenuePointDto>>(
            $"api/v1/dashboard/revenue?period={period}&granularity={granularity}", cancellationToken);

    public Task<IReadOnlyList<SchoolManagement.Application.Dashboard.DTOs.NamedAmountShareDto>> GetRepartitionAsync(
        SchoolManagement.Application.Dashboard.DTOs.DashboardPeriod period = SchoolManagement.Application.Dashboard.DTOs.DashboardPeriod.Month,
        CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<SchoolManagement.Application.Dashboard.DTOs.NamedAmountShareDto>>(
            $"api/v1/dashboard/repartition?period={period}", cancellationToken);

    public Task<IReadOnlyList<SchoolManagement.Application.Dashboard.DTOs.FundAllocationShareDto>> GetDistributionAsync(
        SchoolManagement.Application.Dashboard.DTOs.DashboardPeriod period = SchoolManagement.Application.Dashboard.DTOs.DashboardPeriod.Month,
        Guid? feeTypeId = null,
        CancellationToken cancellationToken = default)
    {
        var url = $"api/v1/dashboard/distribution?period={period}";
        if (feeTypeId.HasValue) url += $"&feeTypeId={feeTypeId}";
        return GetAsync<IReadOnlyList<SchoolManagement.Application.Dashboard.DTOs.FundAllocationShareDto>>(url, cancellationToken);
    }

    public Task<IReadOnlyList<SchoolManagement.Application.Dashboard.DTOs.DashboardAlertDto>> GetAlertsAsync(
        SchoolManagement.Application.Dashboard.DTOs.DashboardPeriod period = SchoolManagement.Application.Dashboard.DTOs.DashboardPeriod.Month,
        CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<SchoolManagement.Application.Dashboard.DTOs.DashboardAlertDto>>(
            $"api/v1/dashboard/alerts?period={period}", cancellationToken);

    public Task<IReadOnlyList<SchoolManagement.Application.Dashboard.DTOs.DashboardActivityDto>> GetActivitiesAsync(
        int take = 20,
        CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<SchoolManagement.Application.Dashboard.DTOs.DashboardActivityDto>>(
            $"api/v1/dashboard/activities?take={take}", cancellationToken);

    public Task<IReadOnlyList<SchoolManagement.Application.Dashboard.DTOs.DashboardPaymentLineDto>> GetPaymentsAsync(
        SchoolManagement.Application.Dashboard.DTOs.DashboardDetailScope scope = SchoolManagement.Application.Dashboard.DTOs.DashboardDetailScope.Today,
        CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<SchoolManagement.Application.Dashboard.DTOs.DashboardPaymentLineDto>>(
            $"api/v1/dashboard/payments?scope={scope}", cancellationToken);

    public Task<IReadOnlyList<SchoolManagement.Application.Dashboard.DTOs.DashboardExpenseLineDto>> GetExpensesAsync(
        SchoolManagement.Application.Dashboard.DTOs.DashboardDetailScope scope = SchoolManagement.Application.Dashboard.DTOs.DashboardDetailScope.Month,
        CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<SchoolManagement.Application.Dashboard.DTOs.DashboardExpenseLineDto>>(
            $"api/v1/dashboard/expenses?scope={scope}", cancellationToken);

    public Task<SchoolManagement.Application.Dashboard.DTOs.EnrolledStudentsBySectionDto> GetEnrolledStudentsAsync(
        CancellationToken cancellationToken = default) =>
        GetAsync<SchoolManagement.Application.Dashboard.DTOs.EnrolledStudentsBySectionDto>(
            "api/v1/dashboard/enrolled-students", cancellationToken);
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

    public Task<SchoolManagement.Application.Reports.DTOs.RealizedReceiptsResultDto> GetRealizedReceiptsAsync(
        SchoolManagement.Application.Reports.DTOs.RealizedReceiptsRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = BuildRealizedReceiptsQuery(request, includePaging: true);
        return GetAsync<SchoolManagement.Application.Reports.DTOs.RealizedReceiptsResultDto>(
            $"api/v1/reports/financial-realized-receipts?{query}", cancellationToken);
    }

    public async Task<byte[]> ExportRealizedReceiptsPdfAsync(
        SchoolManagement.Application.Reports.DTOs.RealizedReceiptsRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = BuildRealizedReceiptsQuery(request, includePaging: false);
        var client = HttpClientFactory.CreateClient("SchoolApiAuth");
        var response = await client.GetAsync(
            $"api/v1/reports/financial-realized-receipts/export/pdf?{query}", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Erreur export PDF ({(int)response.StatusCode})");
        }

        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    public async Task<byte[]> ExportRealizedReceiptsExcelAsync(
        SchoolManagement.Application.Reports.DTOs.RealizedReceiptsRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = BuildRealizedReceiptsQuery(request, includePaging: false);
        var client = HttpClientFactory.CreateClient("SchoolApiAuth");
        var response = await client.GetAsync(
            $"api/v1/reports/financial-realized-receipts/export/excel?{query}", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Erreur export Excel ({(int)response.StatusCode})");
        }

        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    public Task<SchoolManagement.Application.Reports.DTOs.PaymentSituationReportResultDto> GetPaymentSituationReportAsync(
        SchoolManagement.Application.Reports.DTOs.PaymentSituationReportRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = BuildPaymentSituationQuery(request);
        return GetAsync<SchoolManagement.Application.Reports.DTOs.PaymentSituationReportResultDto>(
            $"api/v1/reports/payment-situations?{query}", cancellationToken);
    }

    public async Task<byte[]> ExportPaymentSituationReportPdfAsync(
        SchoolManagement.Application.Reports.DTOs.PaymentSituationReportRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = BuildPaymentSituationQuery(request);
        var client = HttpClientFactory.CreateClient("SchoolApiAuth");
        var response = await client.GetAsync($"api/v1/reports/payment-situations/export/pdf?{query}", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Erreur export PDF ({(int)response.StatusCode})");
        }

        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    public async Task<byte[]> ExportPaymentSituationReportExcelAsync(
        SchoolManagement.Application.Reports.DTOs.PaymentSituationReportRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = BuildPaymentSituationQuery(request);
        var client = HttpClientFactory.CreateClient("SchoolApiAuth");
        var response = await client.GetAsync($"api/v1/reports/payment-situations/export/excel?{query}", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Erreur export Excel ({(int)response.StatusCode})");
        }

        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    private static string BuildPaymentSituationQuery(
        SchoolManagement.Application.Reports.DTOs.PaymentSituationReportRequest request)
    {
        var parts = new List<string>
        {
            $"academicYearId={request.AcademicYearId}",
            $"feeTypeId={request.FeeTypeId}",
            $"scopeKind={(int)request.ScopeKind}",
            $"situationFilter={(int)request.SituationFilter}",
            $"sortBy={(int)request.SortBy}"
        };
        if (request.FeeInstallmentIds is { Count: > 0 })
        {
            foreach (var id in request.FeeInstallmentIds)
            {
                parts.Add($"feeInstallmentIds={id}");
            }
        }

        if (request.EducationCycle.HasValue) parts.Add($"educationCycle={(int)request.EducationCycle.Value}");
        if (request.SectionId.HasValue) parts.Add($"sectionId={request.SectionId}");
        if (request.PedagogicalClassId.HasValue) parts.Add($"pedagogicalClassId={request.PedagogicalClassId}");
        if (request.ClassRoomId.HasValue) parts.Add($"classRoomId={request.ClassRoomId}");
        if (!string.IsNullOrWhiteSpace(request.StudyOption)) parts.Add($"studyOption={Uri.EscapeDataString(request.StudyOption)}");
        if (request.FeePricingCategoryId.HasValue) parts.Add($"feePricingCategoryId={request.FeePricingCategoryId}");
        return string.Join("&", parts);
    }

    private static string BuildRealizedReceiptsQuery(
        SchoolManagement.Application.Reports.DTOs.RealizedReceiptsRequest request,
        bool includePaging)
    {
        var parts = new List<string>
        {
            $"fromDate={request.FromDate:yyyy-MM-dd}",
            $"toDate={request.ToDate:yyyy-MM-dd}"
        };
        if (request.AcademicYearId.HasValue) parts.Add($"academicYearId={request.AcademicYearId}");
        if (request.FeeTypeId.HasValue) parts.Add($"feeTypeId={request.FeeTypeId}");
        if (request.ClassRoomId.HasValue) parts.Add($"classRoomId={request.ClassRoomId}");
        if (request.SectionId.HasValue) parts.Add($"sectionId={request.SectionId}");
        if (includePaging)
        {
            parts.Add($"page={request.Page}");
            parts.Add($"pageSize={request.PageSize}");
        }

        return string.Join("&", parts);
    }
}

public sealed class SecurityAdminApiService : ApiServiceBase, ISecurityAdminApiService
{
    public SecurityAdminApiService(IHttpClientFactory httpClientFactory) : base(httpClientFactory) { }

    private const string Base = "api/v1/security";

    public Task<IReadOnlyList<SchoolManagement.Application.Security.DTOs.SecurityUserDto>> GetUsersAsync(
        CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<SchoolManagement.Application.Security.DTOs.SecurityUserDto>>($"{Base}/users", cancellationToken);

    public Task<IReadOnlyList<SchoolManagement.Application.Security.DTOs.SecurityPersonnelCandidateDto>> SearchPersonnelCandidatesAsync(
        string? search,
        CancellationToken cancellationToken = default)
    {
        var query = string.IsNullOrWhiteSpace(search)
            ? $"{Base}/users/personnel-candidates"
            : $"{Base}/users/personnel-candidates?search={Uri.EscapeDataString(search.Trim())}";
        return GetAsync<IReadOnlyList<SchoolManagement.Application.Security.DTOs.SecurityPersonnelCandidateDto>>(
            query, cancellationToken);
    }

    public Task<SchoolManagement.Application.Security.DTOs.SecurityUserDto> CreateUserAsync(
        SchoolManagement.Application.Security.DTOs.CreateSecurityUserRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.Security.DTOs.SecurityUserDto>($"{Base}/users", request, cancellationToken);

    public Task<SchoolManagement.Application.Security.DTOs.SecurityUserDto> UpdateUserAsync(
        Guid userId,
        SchoolManagement.Application.Security.DTOs.UpdateSecurityUserRequest request,
        CancellationToken cancellationToken = default) =>
        PutAsync<SchoolManagement.Application.Security.DTOs.SecurityUserDto>($"{Base}/users/{userId}", request, cancellationToken);

    public Task<SchoolManagement.Application.Security.DTOs.SecurityUserDto> SetUserRolesAsync(
        Guid userId,
        SchoolManagement.Application.Security.DTOs.SetSecurityUserRolesRequest request,
        CancellationToken cancellationToken = default) =>
        PutAsync<SchoolManagement.Application.Security.DTOs.SecurityUserDto>($"{Base}/users/{userId}/roles", request, cancellationToken);

    public Task ResetPasswordAsync(
        Guid userId,
        SchoolManagement.Application.Security.DTOs.ResetPasswordRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<object>($"{Base}/users/{userId}/reset-password", request, cancellationToken);

    public Task<SchoolManagement.Application.Security.DTOs.EffectivePermissionExplanationDto> GetEffectivePermissionsAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        GetAsync<SchoolManagement.Application.Security.DTOs.EffectivePermissionExplanationDto>(
            $"{Base}/users/{userId}/effective-permissions", cancellationToken);

    public Task<IReadOnlyList<SchoolManagement.Application.Security.DTOs.SecurityRoleDto>> GetRolesAsync(
        CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<SchoolManagement.Application.Security.DTOs.SecurityRoleDto>>($"{Base}/roles", cancellationToken);

    public Task<SchoolManagement.Application.Security.DTOs.SecurityRoleDto> CreateRoleAsync(
        SchoolManagement.Application.Security.DTOs.CreateSecurityRoleRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.Security.DTOs.SecurityRoleDto>($"{Base}/roles", request, cancellationToken);

    public Task<SchoolManagement.Application.Security.DTOs.SecurityRoleDto> UpdateRoleAsync(
        Guid roleId,
        SchoolManagement.Application.Security.DTOs.UpdateSecurityRoleRequest request,
        CancellationToken cancellationToken = default) =>
        PutAsync<SchoolManagement.Application.Security.DTOs.SecurityRoleDto>($"{Base}/roles/{roleId}", request, cancellationToken);

    public Task DeleteRoleAsync(Guid roleId, CancellationToken cancellationToken = default) =>
        DeleteAsync($"{Base}/roles/{roleId}", cancellationToken);

    public Task<SchoolManagement.Application.Security.DTOs.RolePermissionsDto> GetRolePermissionsAsync(
        Guid roleId,
        CancellationToken cancellationToken = default) =>
        GetAsync<SchoolManagement.Application.Security.DTOs.RolePermissionsDto>($"{Base}/roles/{roleId}/permissions", cancellationToken);

    public Task<SchoolManagement.Application.Security.DTOs.RolePermissionsDto> SetRolePermissionsAsync(
        Guid roleId,
        SchoolManagement.Application.Security.DTOs.SetRolePermissionsRequest request,
        CancellationToken cancellationToken = default) =>
        PutAsync<SchoolManagement.Application.Security.DTOs.RolePermissionsDto>($"{Base}/roles/{roleId}/permissions", request, cancellationToken);

    public Task<IReadOnlyList<SchoolManagement.Application.Security.DTOs.PermissionCatalogItemDto>> GetPermissionCatalogAsync(
        CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<SchoolManagement.Application.Security.DTOs.PermissionCatalogItemDto>>($"{Base}/permissions/catalog", cancellationToken);

    public Task<IReadOnlyList<SchoolManagement.Application.Security.DTOs.SecurityExceptionDto>> GetExceptionsAsync(
        Guid? userId = null,
        CancellationToken cancellationToken = default)
    {
        var url = userId is null ? $"{Base}/exceptions" : $"{Base}/exceptions?userId={userId}";
        return GetAsync<IReadOnlyList<SchoolManagement.Application.Security.DTOs.SecurityExceptionDto>>(url, cancellationToken);
    }

    public Task<SchoolManagement.Application.Security.DTOs.SecurityExceptionDto> CreateExceptionAsync(
        SchoolManagement.Application.Security.DTOs.CreateSecurityExceptionRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.Security.DTOs.SecurityExceptionDto>($"{Base}/exceptions", request, cancellationToken);

    public Task<SchoolManagement.Application.Security.DTOs.SecurityExceptionDto> UpdateExceptionAsync(
        Guid exceptionId,
        SchoolManagement.Application.Security.DTOs.UpdateSecurityExceptionRequest request,
        CancellationToken cancellationToken = default) =>
        PutAsync<SchoolManagement.Application.Security.DTOs.SecurityExceptionDto>($"{Base}/exceptions/{exceptionId}", request, cancellationToken);

    public Task CloseExceptionAsync(Guid exceptionId, CancellationToken cancellationToken = default) =>
        PostAsync<object>($"{Base}/exceptions/{exceptionId}/close", new { }, cancellationToken);

    public Task<IReadOnlyList<SchoolManagement.Application.Security.DTOs.SecurityAuditLogDto>> QueryAuditAsync(
        SchoolManagement.Application.Security.DTOs.SecurityAuditQuery query,
        CancellationToken cancellationToken = default)
    {
        var parts = new List<string>();
        if (query.FromUtc is not null) parts.Add($"fromUtc={Uri.EscapeDataString(query.FromUtc.Value.ToString("O"))}");
        if (query.ToUtc is not null) parts.Add($"toUtc={Uri.EscapeDataString(query.ToUtc.Value.ToString("O"))}");
        if (!string.IsNullOrWhiteSpace(query.ActionType)) parts.Add($"actionType={Uri.EscapeDataString(query.ActionType)}");
        if (query.ActorUserId is not null) parts.Add($"actorUserId={query.ActorUserId}");
        if (!string.IsNullOrWhiteSpace(query.TargetUserName)) parts.Add($"targetUserName={Uri.EscapeDataString(query.TargetUserName)}");
        parts.Add($"skip={query.Skip}");
        parts.Add($"take={query.Take}");
        var qs = parts.Count > 0 ? "?" + string.Join("&", parts) : string.Empty;
        return GetAsync<IReadOnlyList<SchoolManagement.Application.Security.DTOs.SecurityAuditLogDto>>($"{Base}/audit{qs}", cancellationToken);
    }

    public Task<SchoolManagement.Application.Security.DTOs.SecurityAuditLogDto> GetAuditAsync(
        Guid auditId,
        CancellationToken cancellationToken = default) =>
        GetAsync<SchoolManagement.Application.Security.DTOs.SecurityAuditLogDto>($"{Base}/audit/{auditId}", cancellationToken);
}

public sealed class PlatformCatalogApiService : ApiServiceBase, IPlatformCatalogApiService
{
    public PlatformCatalogApiService(IHttpClientFactory httpClientFactory) : base(httpClientFactory) { }

    private const string Base = "api/v1/platform";

    public Task<SchoolManagement.Application.Security.DTOs.CatalogTreeDto> GetTreeAsync(
        CancellationToken cancellationToken = default) =>
        GetAsync<SchoolManagement.Application.Security.DTOs.CatalogTreeDto>($"{Base}/catalog/tree", cancellationToken);

    public Task<SchoolManagement.Application.Security.DTOs.SecurityModuleDto> CreateModuleAsync(
        SchoolManagement.Application.Security.DTOs.UpsertSecurityModuleRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.Security.DTOs.SecurityModuleDto>($"{Base}/catalog/modules", request, cancellationToken);

    public Task<SchoolManagement.Application.Security.DTOs.SecurityModuleDto> UpdateModuleAsync(
        Guid id,
        SchoolManagement.Application.Security.DTOs.UpsertSecurityModuleRequest request,
        CancellationToken cancellationToken = default) =>
        PutAsync<SchoolManagement.Application.Security.DTOs.SecurityModuleDto>($"{Base}/catalog/modules/{id}", request, cancellationToken);

    public Task<SchoolManagement.Application.Security.DTOs.SecurityFunctionDto> CreateFunctionAsync(
        SchoolManagement.Application.Security.DTOs.UpsertSecurityFunctionRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.Security.DTOs.SecurityFunctionDto>($"{Base}/catalog/functions", request, cancellationToken);

    public Task<SchoolManagement.Application.Security.DTOs.SecurityFunctionDto> UpdateFunctionAsync(
        Guid id,
        SchoolManagement.Application.Security.DTOs.UpsertSecurityFunctionRequest request,
        CancellationToken cancellationToken = default) =>
        PutAsync<SchoolManagement.Application.Security.DTOs.SecurityFunctionDto>($"{Base}/catalog/functions/{id}", request, cancellationToken);

    public Task<SchoolManagement.Application.Security.DTOs.SecurityPageDto> CreatePageAsync(
        SchoolManagement.Application.Security.DTOs.UpsertSecurityPageRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.Security.DTOs.SecurityPageDto>($"{Base}/catalog/pages", request, cancellationToken);

    public Task<SchoolManagement.Application.Security.DTOs.SecurityPageDto> UpdatePageAsync(
        Guid id,
        SchoolManagement.Application.Security.DTOs.UpsertSecurityPageRequest request,
        CancellationToken cancellationToken = default) =>
        PutAsync<SchoolManagement.Application.Security.DTOs.SecurityPageDto>($"{Base}/catalog/pages/{id}", request, cancellationToken);

    public Task<SchoolManagement.Application.Security.DTOs.SecurityActionDto> CreateActionAsync(
        SchoolManagement.Application.Security.DTOs.UpsertSecurityActionRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.Security.DTOs.SecurityActionDto>($"{Base}/catalog/actions", request, cancellationToken);

    public Task<SchoolManagement.Application.Security.DTOs.SecurityActionDto> UpdateActionAsync(
        Guid id,
        SchoolManagement.Application.Security.DTOs.UpsertSecurityActionRequest request,
        CancellationToken cancellationToken = default) =>
        PutAsync<SchoolManagement.Application.Security.DTOs.SecurityActionDto>($"{Base}/catalog/actions/{id}", request, cancellationToken);

    public Task<IReadOnlyList<SchoolManagement.Application.Security.DTOs.SecurityPermissionAdminDto>> GetPermissionsAsync(
        CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<SchoolManagement.Application.Security.DTOs.SecurityPermissionAdminDto>>($"{Base}/catalog/permissions", cancellationToken);

    public Task<SchoolManagement.Application.Security.DTOs.SecurityPermissionAdminDto> CreatePermissionAsync(
        SchoolManagement.Application.Security.DTOs.UpsertSecurityPermissionRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.Security.DTOs.SecurityPermissionAdminDto>($"{Base}/catalog/permissions", request, cancellationToken);

    public Task<SchoolManagement.Application.Security.DTOs.SecurityPermissionAdminDto> UpdatePermissionAsync(
        Guid id,
        SchoolManagement.Application.Security.DTOs.UpsertSecurityPermissionRequest request,
        CancellationToken cancellationToken = default) =>
        PutAsync<SchoolManagement.Application.Security.DTOs.SecurityPermissionAdminDto>($"{Base}/catalog/permissions/{id}", request, cancellationToken);

    public Task<IReadOnlyList<SchoolManagement.Application.Security.DTOs.PermissionDependencyDto>> GetDependenciesAsync(
        CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<SchoolManagement.Application.Security.DTOs.PermissionDependencyDto>>($"{Base}/catalog/dependencies", cancellationToken);

    public Task<SchoolManagement.Application.Security.DTOs.PermissionDependencyDto> AddDependencyAsync(
        SchoolManagement.Application.Security.DTOs.CreatePermissionDependencyRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.Security.DTOs.PermissionDependencyDto>($"{Base}/catalog/dependencies", request, cancellationToken);

    public Task RemoveDependencyAsync(Guid dependencyId, CancellationToken cancellationToken = default) =>
        DeleteAsync($"{Base}/catalog/dependencies/{dependencyId}", cancellationToken);

    public Task<IReadOnlyList<SchoolManagement.Application.Security.DTOs.SecurityAuditLogDto>> QueryPlatformAuditAsync(
        SchoolManagement.Application.Security.DTOs.SecurityAuditQuery query,
        CancellationToken cancellationToken = default)
    {
        var parts = new List<string>();
        if (query.SchoolId is not null) parts.Add($"schoolId={query.SchoolId}");
        if (query.FromUtc is not null) parts.Add($"fromUtc={Uri.EscapeDataString(query.FromUtc.Value.ToString("O"))}");
        if (query.ToUtc is not null) parts.Add($"toUtc={Uri.EscapeDataString(query.ToUtc.Value.ToString("O"))}");
        if (!string.IsNullOrWhiteSpace(query.ActionType)) parts.Add($"actionType={Uri.EscapeDataString(query.ActionType)}");
        parts.Add($"skip={query.Skip}");
        parts.Add($"take={query.Take}");
        var qs = parts.Count > 0 ? "?" + string.Join("&", parts) : string.Empty;
        return GetAsync<IReadOnlyList<SchoolManagement.Application.Security.DTOs.SecurityAuditLogDto>>($"{Base}/audit{qs}", cancellationToken);
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

    public Task<IReadOnlyList<SchoolManagement.Application.Admin.DTOs.TeacherAdminDto>> GetTeachersAsync(
        CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<SchoolManagement.Application.Admin.DTOs.TeacherAdminDto>>("api/v1/admin/teachers", cancellationToken);

    public Task<SchoolManagement.Application.Admin.DTOs.TeacherAdminDto> CreateTeacherAsync(
        SchoolManagement.Application.Admin.DTOs.CreateTeacherAdminRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.Admin.DTOs.TeacherAdminDto>("api/v1/admin/teachers", request, cancellationToken);

    public Task<SchoolManagement.Application.Admin.DTOs.TeacherAdminDto> UpdateTeacherAsync(
        Guid teacherId,
        SchoolManagement.Application.Admin.DTOs.UpdateTeacherAdminRequest request,
        CancellationToken cancellationToken = default) =>
        PutAsync<SchoolManagement.Application.Admin.DTOs.TeacherAdminDto>($"api/v1/admin/teachers/{teacherId}", request, cancellationToken);
}

public sealed class UpdateAdminApiService : ApiServiceBase, IUpdateAdminApiService
{
    public UpdateAdminApiService(IHttpClientFactory httpClientFactory) : base(httpClientFactory) { }

    public Task<IReadOnlyList<SchoolManagement.Application.Updates.DTOs.ApplicationVersionAdminDto>> ListVersionsAsync(
        CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<SchoolManagement.Application.Updates.DTOs.ApplicationVersionAdminDto>>(
            "api/v1/update/versions", cancellationToken);

    public Task<SchoolManagement.Application.Updates.DTOs.ApplicationVersionAdminDto> PublishAsync(
        SchoolManagement.Application.Updates.DTOs.PublishApplicationVersionRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.Updates.DTOs.ApplicationVersionAdminDto>(
            "api/v1/update/versions", request, cancellationToken);

    public Task<SchoolManagement.Application.Updates.DTOs.ApplicationVersionAdminDto> SetActiveAsync(
        Guid id,
        bool active,
        bool deactivateOthers = true,
        CancellationToken cancellationToken = default) =>
        PutAsync<SchoolManagement.Application.Updates.DTOs.ApplicationVersionAdminDto>(
            $"api/v1/update/versions/{id}/active?active={active}&deactivateOthers={deactivateOthers}",
            new { },
            cancellationToken);
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
        bool forReinscription = false,
        CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<SchoolManagement.Application.EnrollmentWizard.DTOs.EnrollmentStudentSearchResultDto>>(
            $"api/v1/enrollment-wizard/search-students?search={Uri.EscapeDataString(search)}&forReinscription={(forReinscription ? "true" : "false")}",
            cancellationToken);

    public Task<IReadOnlyList<SchoolManagement.Application.EnrollmentWizard.DTOs.EnrollmentGuardianSearchResultDto>> SearchGuardiansAsync(
        string search,
        CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<SchoolManagement.Application.EnrollmentWizard.DTOs.EnrollmentGuardianSearchResultDto>>(
            $"api/v1/enrollment-wizard/search-guardians?search={Uri.EscapeDataString(search)}",
            cancellationToken);

    public async Task<SchoolManagement.Application.EnrollmentWizard.DTOs.StoredEnrollmentFileDto> StoreEnrollmentFileAsync(
        string lastName,
        string firstName,
        string registrationNumber,
        string academicYearLabel,
        string documentType,
        string filePath,
        CancellationToken cancellationToken = default)
    {
        var client = HttpClientFactory.CreateClient("SchoolApiAuth");
        await using var stream = File.OpenRead(filePath);
        using var content = new MultipartFormDataContent
        {
            { new StringContent(lastName), "lastName" },
            { new StringContent(firstName), "firstName" },
            { new StringContent(registrationNumber), "registrationNumber" },
            { new StringContent(academicYearLabel), "academicYearLabel" },
            { new StringContent(documentType), "documentType" },
            { new StreamContent(stream), "file", Path.GetFileName(filePath) }
        };

        var response = await client.PostAsync("api/v1/enrollment-wizard/store-file", content, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadFromJsonAsync<ApiResponse<object>>(cancellationToken: cancellationToken);
            throw new HttpRequestException(error?.Message ?? $"Erreur API ({(int)response.StatusCode})");
        }

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<SchoolManagement.Application.EnrollmentWizard.DTOs.StoredEnrollmentFileDto>>(
            cancellationToken: cancellationToken);
        return body?.Data ?? throw new InvalidOperationException("Réponse API invalide.");
    }

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
        Guid? pedagogicalClassId = null,
        Guid? academicYearId = null,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string>();
        if (pedagogicalClassId.HasValue)
        {
            query.Add($"pedagogicalClassId={pedagogicalClassId.Value}");
        }

        if (academicYearId.HasValue)
        {
            query.Add($"academicYearId={academicYearId.Value}");
        }

        var suffix = query.Count > 0 ? "?" + string.Join("&", query) : string.Empty;
        return GetAsync<SchoolManagement.Application.EnrollmentWizard.DTOs.EnrollmentFeeSummaryDto>(
            $"api/v1/enrollment-wizard/fees{suffix}",
            cancellationToken);
    }

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

    public Task<SchoolManagement.Application.EnrollmentWizard.DTOs.EnrollmentFormDocumentDto> GetEnrollmentFormAsync(
        Guid enrollmentId,
        CancellationToken cancellationToken = default) =>
        GetAsync<SchoolManagement.Application.EnrollmentWizard.DTOs.EnrollmentFormDocumentDto>(
            $"api/v1/enrollment-wizard/fiche-inscription/{enrollmentId}", cancellationToken);

    public Task<SchoolManagement.Application.EnrollmentWizard.DTOs.StudentDossierEditDto> GetStudentDossierForEditAsync(
        Guid studentId,
        CancellationToken cancellationToken = default) =>
        GetAsync<SchoolManagement.Application.EnrollmentWizard.DTOs.StudentDossierEditDto>(
            $"api/v1/enrollment-wizard/student-dossier/{studentId}", cancellationToken);

    public Task<SchoolManagement.Application.EnrollmentWizard.DTOs.EnrollmentValidationResultDto> ValidateStudentDossierUpdateAsync(
        Guid enrollmentId,
        SchoolManagement.Application.EnrollmentWizard.DTOs.CompleteEnrollmentRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.EnrollmentWizard.DTOs.EnrollmentValidationResultDto>(
            $"api/v1/enrollment-wizard/student-dossier/{enrollmentId}/validate",
            request,
            cancellationToken);

    public Task<SchoolManagement.Application.EnrollmentWizard.DTOs.UpdateStudentDossierResultDto> UpdateStudentDossierAsync(
        Guid enrollmentId,
        SchoolManagement.Application.EnrollmentWizard.DTOs.CompleteEnrollmentRequest request,
        CancellationToken cancellationToken = default) =>
        PutAsync<SchoolManagement.Application.EnrollmentWizard.DTOs.UpdateStudentDossierResultDto>(
            $"api/v1/enrollment-wizard/student-dossier/{enrollmentId}",
            request,
            cancellationToken);
}

public sealed class GeographyApiService : ApiServiceBase, IGeographyApiService
{
    public GeographyApiService(IHttpClientFactory httpClientFactory) : base(httpClientFactory) { }

    public Task<IReadOnlyList<SchoolManagement.Application.Geography.DTOs.GeographyItemDto>> GetCountriesAsync(
        CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<SchoolManagement.Application.Geography.DTOs.GeographyItemDto>>(
            "api/v1/geography/countries", cancellationToken);

    public Task<IReadOnlyList<SchoolManagement.Application.Geography.DTOs.GeographyItemDto>> GetProvincesAsync(
        Guid countryId,
        CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<SchoolManagement.Application.Geography.DTOs.GeographyItemDto>>(
            $"api/v1/geography/provinces?countryId={countryId}", cancellationToken);

    public Task<IReadOnlyList<SchoolManagement.Application.Geography.DTOs.GeographyItemDto>> GetCitiesAsync(
        Guid provinceId,
        CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<SchoolManagement.Application.Geography.DTOs.GeographyItemDto>>(
            $"api/v1/geography/cities?provinceId={provinceId}", cancellationToken);

    public Task<IReadOnlyList<SchoolManagement.Application.Geography.DTOs.GeographyItemDto>> GetCommunesAsync(
        Guid cityId,
        CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<SchoolManagement.Application.Geography.DTOs.GeographyItemDto>>(
            $"api/v1/geography/communes?cityId={cityId}", cancellationToken);

    public async Task<SchoolManagement.Application.Geography.DTOs.AddressDto?> GetAddressAsync(
        Guid addressId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await GetAsync<SchoolManagement.Application.Geography.DTOs.AddressDto>(
                $"api/v1/geography/addresses/{addressId}", cancellationToken);
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }
}

public sealed class GeographyAdminApiService : ApiServiceBase, IGeographyAdminApiService
{
    public GeographyAdminApiService(IHttpClientFactory httpClientFactory) : base(httpClientFactory) { }

    public Task<IReadOnlyList<SchoolManagement.Application.Geography.DTOs.GeographyItemDto>> GetCountriesAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<SchoolManagement.Application.Geography.DTOs.GeographyItemDto>>(
            $"api/v1/geography/admin/countries?includeInactive={includeInactive.ToString().ToLowerInvariant()}",
            cancellationToken);

    public Task<IReadOnlyList<SchoolManagement.Application.Geography.DTOs.GeographyItemDto>> GetProvincesAsync(
        Guid countryId,
        bool includeInactive = false,
        CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<SchoolManagement.Application.Geography.DTOs.GeographyItemDto>>(
            $"api/v1/geography/admin/provinces?countryId={countryId}&includeInactive={includeInactive.ToString().ToLowerInvariant()}",
            cancellationToken);

    public Task<IReadOnlyList<SchoolManagement.Application.Geography.DTOs.GeographyItemDto>> GetCitiesAsync(
        Guid provinceId,
        bool includeInactive = false,
        CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<SchoolManagement.Application.Geography.DTOs.GeographyItemDto>>(
            $"api/v1/geography/admin/cities?provinceId={provinceId}&includeInactive={includeInactive.ToString().ToLowerInvariant()}",
            cancellationToken);

    public Task<IReadOnlyList<SchoolManagement.Application.Geography.DTOs.GeographyItemDto>> GetCommunesAsync(
        Guid cityId,
        bool includeInactive = false,
        CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<SchoolManagement.Application.Geography.DTOs.GeographyItemDto>>(
            $"api/v1/geography/admin/communes?cityId={cityId}&includeInactive={includeInactive.ToString().ToLowerInvariant()}",
            cancellationToken);

    public Task<SchoolManagement.Application.Geography.DTOs.GeographyItemDto> SaveCountryAsync(
        SchoolManagement.Application.Geography.DTOs.UpsertGeographyItemRequest request,
        Guid? id = null,
        CancellationToken cancellationToken = default) =>
        id.HasValue
            ? PutAsync<SchoolManagement.Application.Geography.DTOs.GeographyItemDto>(
                $"api/v1/geography/admin/countries/{id.Value}", request, cancellationToken)
            : PostAsync<SchoolManagement.Application.Geography.DTOs.GeographyItemDto>(
                "api/v1/geography/admin/countries", request, cancellationToken);

    public Task<SchoolManagement.Application.Geography.DTOs.GeographyItemDto> SaveProvinceAsync(
        SchoolManagement.Application.Geography.DTOs.CreateProvinceRequest request,
        Guid? id = null,
        CancellationToken cancellationToken = default) =>
        id.HasValue
            ? PutAsync<SchoolManagement.Application.Geography.DTOs.GeographyItemDto>(
                $"api/v1/geography/admin/provinces/{id.Value}", request, cancellationToken)
            : PostAsync<SchoolManagement.Application.Geography.DTOs.GeographyItemDto>(
                "api/v1/geography/admin/provinces", request, cancellationToken);

    public Task<SchoolManagement.Application.Geography.DTOs.GeographyItemDto> SaveCityAsync(
        SchoolManagement.Application.Geography.DTOs.CreateCityRequest request,
        Guid? id = null,
        CancellationToken cancellationToken = default) =>
        id.HasValue
            ? PutAsync<SchoolManagement.Application.Geography.DTOs.GeographyItemDto>(
                $"api/v1/geography/admin/cities/{id.Value}", request, cancellationToken)
            : PostAsync<SchoolManagement.Application.Geography.DTOs.GeographyItemDto>(
                "api/v1/geography/admin/cities", request, cancellationToken);

    public Task<SchoolManagement.Application.Geography.DTOs.GeographyItemDto> SaveCommuneAsync(
        SchoolManagement.Application.Geography.DTOs.CreateCommuneRequest request,
        Guid? id = null,
        CancellationToken cancellationToken = default) =>
        id.HasValue
            ? PutAsync<SchoolManagement.Application.Geography.DTOs.GeographyItemDto>(
                $"api/v1/geography/admin/communes/{id.Value}", request, cancellationToken)
            : PostAsync<SchoolManagement.Application.Geography.DTOs.GeographyItemDto>(
                "api/v1/geography/admin/communes", request, cancellationToken);

    public Task DeactivateCountryAsync(Guid id, CancellationToken cancellationToken = default) =>
        DeleteAsync($"api/v1/geography/admin/countries/{id}", cancellationToken);

    public Task DeactivateProvinceAsync(Guid id, CancellationToken cancellationToken = default) =>
        DeleteAsync($"api/v1/geography/admin/provinces/{id}", cancellationToken);

    public Task DeactivateCityAsync(Guid id, CancellationToken cancellationToken = default) =>
        DeleteAsync($"api/v1/geography/admin/cities/{id}", cancellationToken);

    public Task DeactivateCommuneAsync(Guid id, CancellationToken cancellationToken = default) =>
        DeleteAsync($"api/v1/geography/admin/communes/{id}", cancellationToken);

    public async Task<byte[]> DownloadImportTemplateAsync(CancellationToken cancellationToken = default)
    {
        var client = HttpClientFactory.CreateClient("SchoolApiAuth");
        var response = await client.GetAsync("api/v1/geography/admin/import/template", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadFromJsonAsync<ApiResponse<object>>(cancellationToken: cancellationToken);
            throw new HttpRequestException(error?.Message ?? $"Erreur API ({(int)response.StatusCode})");
        }

        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    public async Task<SchoolManagement.Application.Geography.DTOs.GeographyImportResultDto> ImportExcelAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        var client = HttpClientFactory.CreateClient("SchoolApiAuth");
        await using var stream = File.OpenRead(filePath);
        using var content = new MultipartFormDataContent
        {
            { new StreamContent(stream), "file", Path.GetFileName(filePath) }
        };

        var response = await client.PostAsync("api/v1/geography/admin/import", content, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadFromJsonAsync<ApiResponse<object>>(cancellationToken: cancellationToken);
            throw new HttpRequestException(error?.Message ?? $"Erreur API ({(int)response.StatusCode})");
        }

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<SchoolManagement.Application.Geography.DTOs.GeographyImportResultDto>>(
            cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Réponse API invalide.");
        return body.Data ?? throw new InvalidOperationException(body.Message ?? "Données absentes.");
    }
}

public sealed class DocumentBrandingApiService : ApiServiceBase, IDocumentBrandingApiService
{
    public DocumentBrandingApiService(IHttpClientFactory httpClientFactory) : base(httpClientFactory) { }

    public Task<SchoolManagement.Application.DocumentBranding.DTOs.DocumentBrandingConfigurationDto> GetConfigurationAsync(
        CancellationToken cancellationToken = default) =>
        GetAsync<SchoolManagement.Application.DocumentBranding.DTOs.DocumentBrandingConfigurationDto>(
            "api/v1/document-branding/configuration", cancellationToken);

    public Task<SchoolManagement.Application.DocumentBranding.DTOs.DocumentBrandingLookupDto> GetLookupsAsync(
        CancellationToken cancellationToken = default) =>
        GetAsync<SchoolManagement.Application.DocumentBranding.DTOs.DocumentBrandingLookupDto>(
            "api/v1/document-branding/lookups", cancellationToken);

    public Task<SchoolManagement.Application.DocumentBranding.DTOs.SchoolLogoDto> CreateLogoAsync(
        SchoolManagement.Application.DocumentBranding.DTOs.SaveSchoolLogoRequest request,
        string imagePath,
        CancellationToken cancellationToken = default) =>
        PostMultipartAsync<SchoolManagement.Application.DocumentBranding.DTOs.SchoolLogoDto>(
            "api/v1/document-branding/logos",
            BuildLogoForm(request, imagePath),
            cancellationToken);

    public Task<SchoolManagement.Application.DocumentBranding.DTOs.SchoolLogoDto> UpdateLogoAsync(
        Guid logoId,
        SchoolManagement.Application.DocumentBranding.DTOs.SaveSchoolLogoRequest request,
        string? imagePath,
        CancellationToken cancellationToken = default) =>
        PutMultipartAsync<SchoolManagement.Application.DocumentBranding.DTOs.SchoolLogoDto>(
            $"api/v1/document-branding/logos/{logoId}",
            BuildLogoForm(request, imagePath),
            cancellationToken);

    public Task DeleteLogoAsync(Guid logoId, CancellationToken cancellationToken = default) =>
        DeleteAsync($"api/v1/document-branding/logos/{logoId}", cancellationToken);

    public Task SetPrimaryLogoAsync(Guid logoId, CancellationToken cancellationToken = default) =>
        PostAsync<object>($"api/v1/document-branding/logos/{logoId}/set-primary", new { }, cancellationToken);

    public Task<SchoolManagement.Application.DocumentBranding.DTOs.SchoolDocumentHeaderDto> CreateHeaderAsync(
        SchoolManagement.Application.DocumentBranding.DTOs.SaveSchoolDocumentHeaderRequest request,
        string? imagePath,
        CancellationToken cancellationToken = default) =>
        PostMultipartAsync<SchoolManagement.Application.DocumentBranding.DTOs.SchoolDocumentHeaderDto>(
            "api/v1/document-branding/headers",
            BuildHeaderForm(request, imagePath),
            cancellationToken);

    public Task<SchoolManagement.Application.DocumentBranding.DTOs.SchoolDocumentHeaderDto> UpdateHeaderAsync(
        Guid headerId,
        SchoolManagement.Application.DocumentBranding.DTOs.SaveSchoolDocumentHeaderRequest request,
        string? imagePath,
        CancellationToken cancellationToken = default) =>
        PutMultipartAsync<SchoolManagement.Application.DocumentBranding.DTOs.SchoolDocumentHeaderDto>(
            $"api/v1/document-branding/headers/{headerId}",
            BuildHeaderForm(request, imagePath),
            cancellationToken);

    public Task DeleteHeaderAsync(Guid headerId, CancellationToken cancellationToken = default) =>
        DeleteAsync($"api/v1/document-branding/headers/{headerId}", cancellationToken);

    public Task<SchoolManagement.Application.DocumentBranding.DTOs.SchoolSignatureDto> CreateSignatureAsync(
        SchoolManagement.Application.DocumentBranding.DTOs.SaveSchoolSignatureRequest request,
        string imagePath,
        CancellationToken cancellationToken = default) =>
        PostMultipartAsync<SchoolManagement.Application.DocumentBranding.DTOs.SchoolSignatureDto>(
            "api/v1/document-branding/signatures",
            BuildSignatureForm(request, imagePath),
            cancellationToken);

    public Task<SchoolManagement.Application.DocumentBranding.DTOs.SchoolSignatureDto> UpdateSignatureAsync(
        Guid signatureId,
        SchoolManagement.Application.DocumentBranding.DTOs.SaveSchoolSignatureRequest request,
        string? imagePath,
        CancellationToken cancellationToken = default) =>
        PutMultipartAsync<SchoolManagement.Application.DocumentBranding.DTOs.SchoolSignatureDto>(
            $"api/v1/document-branding/signatures/{signatureId}",
            BuildSignatureForm(request, imagePath),
            cancellationToken);

    public Task DeleteSignatureAsync(Guid signatureId, CancellationToken cancellationToken = default) =>
        DeleteAsync($"api/v1/document-branding/signatures/{signatureId}", cancellationToken);

    public Task<SchoolManagement.Application.DocumentBranding.DTOs.SchoolStampDto> CreateStampAsync(
        SchoolManagement.Application.DocumentBranding.DTOs.SaveSchoolStampRequest request,
        string imagePath,
        CancellationToken cancellationToken = default) =>
        PostMultipartAsync<SchoolManagement.Application.DocumentBranding.DTOs.SchoolStampDto>(
            "api/v1/document-branding/stamps",
            BuildStampForm(request, imagePath),
            cancellationToken);

    public Task<SchoolManagement.Application.DocumentBranding.DTOs.SchoolStampDto> UpdateStampAsync(
        Guid stampId,
        SchoolManagement.Application.DocumentBranding.DTOs.SaveSchoolStampRequest request,
        string? imagePath,
        CancellationToken cancellationToken = default) =>
        PutMultipartAsync<SchoolManagement.Application.DocumentBranding.DTOs.SchoolStampDto>(
            $"api/v1/document-branding/stamps/{stampId}",
            BuildStampForm(request, imagePath),
            cancellationToken);

    public Task DeleteStampAsync(Guid stampId, CancellationToken cancellationToken = default) =>
        DeleteAsync($"api/v1/document-branding/stamps/{stampId}", cancellationToken);

    public Task<SchoolManagement.Application.DocumentBranding.DTOs.SchoolDocumentFooterDto> SaveFooterAsync(
        SchoolManagement.Application.DocumentBranding.DTOs.SaveSchoolDocumentFooterRequest request,
        CancellationToken cancellationToken = default) =>
        PutAsync<SchoolManagement.Application.DocumentBranding.DTOs.SchoolDocumentFooterDto>(
            "api/v1/document-branding/footer", request, cancellationToken);

    private static MultipartFormDataContent BuildLogoForm(
        SchoolManagement.Application.DocumentBranding.DTOs.SaveSchoolLogoRequest request,
        string? imagePath)
    {
        var content = new MultipartFormDataContent
        {
            { new StringContent(request.Name), "Name" },
            { new StringContent(request.IsPrimary.ToString()), "IsPrimary" },
            { new StringContent(request.IsActive.ToString()), "IsActive" }
        };
        AddImage(content, imagePath);
        return content;
    }

    private static MultipartFormDataContent BuildHeaderForm(
        SchoolManagement.Application.DocumentBranding.DTOs.SaveSchoolDocumentHeaderRequest request,
        string? imagePath)
    {
        var content = new MultipartFormDataContent
        {
            { new StringContent(request.Name), "Name" },
            { new StringContent(((int)request.DocumentType).ToString()), "DocumentType" },
            { new StringContent(((int)request.PrintMode).ToString()), "PrintMode" },
            { new StringContent(request.IsActive.ToString()), "IsActive" }
        };
        if (request.WidthPx.HasValue) content.Add(new StringContent(request.WidthPx.Value.ToString()), "WidthPx");
        if (request.HeightPx.HasValue) content.Add(new StringContent(request.HeightPx.Value.ToString()), "HeightPx");
        if (request.ResolutionDpi.HasValue) content.Add(new StringContent(request.ResolutionDpi.Value.ToString()), "ResolutionDpi");
        content.Add(new StringContent(request.MarginLeftMm.ToString(System.Globalization.CultureInfo.InvariantCulture)), "MarginLeftMm");
        content.Add(new StringContent(request.MarginRightMm.ToString(System.Globalization.CultureInfo.InvariantCulture)), "MarginRightMm");
        if (request.MaxHeightMm.HasValue)
        {
            content.Add(new StringContent(request.MaxHeightMm.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)), "MaxHeightMm");
        }
        if (!string.IsNullOrWhiteSpace(request.ApplicableDocumentTypes))
        {
            content.Add(new StringContent(request.ApplicableDocumentTypes), "ApplicableDocumentTypes");
        }
        AddImage(content, imagePath);
        return content;
    }

    private static MultipartFormDataContent BuildSignatureForm(
        SchoolManagement.Application.DocumentBranding.DTOs.SaveSchoolSignatureRequest request,
        string? imagePath)
    {
        var content = new MultipartFormDataContent
        {
            { new StringContent(request.SignatoryName), "SignatoryName" },
            { new StringContent(request.Function), "Function" },
            { new StringContent(((int)request.DocumentType).ToString()), "DocumentType" },
            { new StringContent(request.IsActive.ToString()), "IsActive" }
        };
        if (!string.IsNullOrWhiteSpace(request.ApplicableDocumentTypes))
        {
            content.Add(new StringContent(request.ApplicableDocumentTypes), "ApplicableDocumentTypes");
        }
        AddImage(content, imagePath);
        return content;
    }

    private static MultipartFormDataContent BuildStampForm(
        SchoolManagement.Application.DocumentBranding.DTOs.SaveSchoolStampRequest request,
        string? imagePath)
    {
        var content = new MultipartFormDataContent
        {
            { new StringContent(request.Name), "Name" },
            { new StringContent(request.IsActive.ToString()), "IsActive" }
        };
        AddImage(content, imagePath);
        return content;
    }

    private static void AddImage(MultipartFormDataContent content, string? imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
        {
            return;
        }

        var stream = File.OpenRead(imagePath);
        content.Add(new StreamContent(stream), "image", Path.GetFileName(imagePath));
    }

    private async Task<T> PostMultipartAsync<T>(string url, MultipartFormDataContent content, CancellationToken cancellationToken)
    {
        var client = HttpClientFactory.CreateClient("SchoolApiAuth");
        var response = await client.PostAsync(url, content, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadFromJsonAsync<ApiResponse<object>>(cancellationToken: cancellationToken);
            throw new HttpRequestException(error?.Message ?? $"Erreur API ({(int)response.StatusCode})");
        }

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<T>>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Réponse API invalide.");
        return body.Data ?? throw new InvalidOperationException(body.Message ?? "Données absentes.");
    }

    private async Task<T> PutMultipartAsync<T>(string url, MultipartFormDataContent content, CancellationToken cancellationToken)
    {
        var client = HttpClientFactory.CreateClient("SchoolApiAuth");
        var response = await client.PutAsync(url, content, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadFromJsonAsync<ApiResponse<object>>(cancellationToken: cancellationToken);
            throw new HttpRequestException(error?.Message ?? $"Erreur API ({(int)response.StatusCode})");
        }

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<T>>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Réponse API invalide.");
        return body.Data ?? throw new InvalidOperationException(body.Message ?? "Données absentes.");
    }
}

public sealed class SchoolFeeApiService : ApiServiceBase, ISchoolFeeApiService
{
    public SchoolFeeApiService(IHttpClientFactory httpClientFactory) : base(httpClientFactory) { }

    public Task<SchoolManagement.Application.SchoolFees.DTOs.SchoolFeeCatalogDto> GetCatalogAsync(
        CancellationToken cancellationToken = default) =>
        GetAsync<SchoolManagement.Application.SchoolFees.DTOs.SchoolFeeCatalogDto>(
            "api/v1/school-fees/catalog", cancellationToken);

    public Task<SchoolManagement.Application.SchoolFees.DTOs.FeeTypeDto> CreateFeeTypeAsync(
        SchoolManagement.Application.SchoolFees.DTOs.CreateFeeTypeRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.SchoolFees.DTOs.FeeTypeDto>(
            "api/v1/school-fees/fee-types", request, cancellationToken);

    public Task<SchoolManagement.Application.SchoolFees.DTOs.FeeTypeDto> UpdateFeeTypeAsync(
        Guid feeTypeId,
        SchoolManagement.Application.SchoolFees.DTOs.UpdateFeeTypeRequest request,
        CancellationToken cancellationToken = default) =>
        PutAsync<SchoolManagement.Application.SchoolFees.DTOs.FeeTypeDto>(
            $"api/v1/school-fees/fee-types/{feeTypeId}", request, cancellationToken);

    public Task DeleteFeeTypeAsync(Guid feeTypeId, CancellationToken cancellationToken = default) =>
        DeleteAsync($"api/v1/school-fees/fee-types/{feeTypeId}", cancellationToken);

    public Task<SchoolManagement.Application.SchoolFees.DTOs.FeePricingCategoryDto> CreatePricingCategoryAsync(
        SchoolManagement.Application.SchoolFees.DTOs.CreateFeePricingCategoryRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.SchoolFees.DTOs.FeePricingCategoryDto>(
            "api/v1/school-fees/pricing-categories", request, cancellationToken);

    public Task<SchoolManagement.Application.SchoolFees.DTOs.FeePricingCategoryDto> UpdatePricingCategoryAsync(
        Guid categoryId,
        SchoolManagement.Application.SchoolFees.DTOs.UpdateFeePricingCategoryRequest request,
        CancellationToken cancellationToken = default) =>
        PutAsync<SchoolManagement.Application.SchoolFees.DTOs.FeePricingCategoryDto>(
            $"api/v1/school-fees/pricing-categories/{categoryId}", request, cancellationToken);

    public Task DeletePricingCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default) =>
        DeleteAsync($"api/v1/school-fees/pricing-categories/{categoryId}", cancellationToken);

    public Task<SchoolManagement.Application.SchoolFees.DTOs.FeeInstallmentDto> CreateInstallmentAsync(
        SchoolManagement.Application.SchoolFees.DTOs.SaveFeeInstallmentRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.SchoolFees.DTOs.FeeInstallmentDto>(
            "api/v1/school-fees/installments", request, cancellationToken);

    public Task<SchoolManagement.Application.SchoolFees.DTOs.FeeInstallmentDto> UpdateInstallmentAsync(
        Guid installmentId,
        SchoolManagement.Application.SchoolFees.DTOs.SaveFeeInstallmentRequest request,
        CancellationToken cancellationToken = default) =>
        PutAsync<SchoolManagement.Application.SchoolFees.DTOs.FeeInstallmentDto>(
            $"api/v1/school-fees/installments/{installmentId}", request, cancellationToken);

    public Task DeleteInstallmentAsync(Guid installmentId, CancellationToken cancellationToken = default) =>
        DeleteAsync($"api/v1/school-fees/installments/{installmentId}", cancellationToken);

    public Task<IReadOnlyList<SchoolManagement.Application.SchoolFees.DTOs.FeeTypeInstallmentDto>> GetFeeTypeInstallmentsAsync(
        Guid feeTypeId,
        CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<SchoolManagement.Application.SchoolFees.DTOs.FeeTypeInstallmentDto>>(
            $"api/v1/school-fees/fee-types/{feeTypeId}/installments", cancellationToken);

    public Task<IReadOnlyList<SchoolManagement.Application.SchoolFees.DTOs.FeeTypeInstallmentDto>> SaveFeeTypeInstallmentsAsync(
        Guid feeTypeId,
        SchoolManagement.Application.SchoolFees.DTOs.SaveFeeTypeInstallmentsRequest request,
        CancellationToken cancellationToken = default) =>
        PutAsync<IReadOnlyList<SchoolManagement.Application.SchoolFees.DTOs.FeeTypeInstallmentDto>>(
            $"api/v1/school-fees/fee-types/{feeTypeId}/installments", request, cancellationToken);

    public Task<SchoolManagement.Application.SchoolFees.DTOs.ClassFeeScheduleDto> GetScheduleAsync(
        Guid academicYearId,
        Guid pedagogicalClassId,
        Guid feePricingCategoryId,
        Guid feeTypeId,
        CancellationToken cancellationToken = default) =>
        GetAsync<SchoolManagement.Application.SchoolFees.DTOs.ClassFeeScheduleDto>(
            $"api/v1/school-fees/schedule?academicYearId={academicYearId}&pedagogicalClassId={pedagogicalClassId}&feePricingCategoryId={feePricingCategoryId}&feeTypeId={feeTypeId}",
            cancellationToken);

    public Task<IReadOnlyList<SchoolManagement.Application.SchoolFees.DTOs.ClassFeeScheduleSignatureDto>> GetScheduleSignaturesAsync(
        Guid academicYearId,
        Guid feePricingCategoryId,
        Guid feeTypeId,
        CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<SchoolManagement.Application.SchoolFees.DTOs.ClassFeeScheduleSignatureDto>>(
            $"api/v1/school-fees/schedule/signatures?academicYearId={academicYearId}&feePricingCategoryId={feePricingCategoryId}&feeTypeId={feeTypeId}",
            cancellationToken);

    public Task<SchoolManagement.Application.SchoolFees.DTOs.ClassFeeScheduleDto> SaveScheduleAsync(
        SchoolManagement.Application.SchoolFees.DTOs.SaveClassFeeScheduleRequest request,
        CancellationToken cancellationToken = default) =>
        PutAsync<SchoolManagement.Application.SchoolFees.DTOs.ClassFeeScheduleDto>(
            "api/v1/school-fees/schedule", request, cancellationToken);

    public Task<SchoolManagement.Application.SchoolFees.DTOs.SaveClassFeeScheduleBulkResult> SaveScheduleBulkAsync(
        SchoolManagement.Application.SchoolFees.DTOs.SaveClassFeeScheduleBulkRequest request,
        CancellationToken cancellationToken = default) =>
        PutAsync<SchoolManagement.Application.SchoolFees.DTOs.SaveClassFeeScheduleBulkResult>(
            "api/v1/school-fees/schedule/bulk", request, cancellationToken);

    public Task<SchoolManagement.Application.SchoolFees.DTOs.CopyClassFeeScheduleResult> CopyScheduleFromPreviousAsync(
        SchoolManagement.Application.SchoolFees.DTOs.CopyClassFeeScheduleRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.SchoolFees.DTOs.CopyClassFeeScheduleResult>(
            "api/v1/school-fees/schedule/copy-from-previous", request, cancellationToken);

    public Task<SchoolManagement.Application.SchoolFees.DTOs.CopyClassFeeScheduleBulkResult> CopyScheduleFromPreviousBulkAsync(
        SchoolManagement.Application.SchoolFees.DTOs.CopyClassFeeScheduleBulkRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.SchoolFees.DTOs.CopyClassFeeScheduleBulkResult>(
            "api/v1/school-fees/schedule/copy-from-previous/bulk", request, cancellationToken);
}

public sealed class AccountingApiService : ApiServiceBase, IAccountingApiService
{
    public AccountingApiService(IHttpClientFactory httpClientFactory) : base(httpClientFactory) { }

    public Task<SchoolManagement.Application.Accounting.DTOs.ExpenseRequestSearchResultDto> SearchExpenseRequestsAsync(
        SchoolManagement.Application.Accounting.DTOs.ExpenseSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var parts = new List<string> { $"page={request.Page}", $"pageSize={request.PageSize}" };
        if (request.AcademicYearId.HasValue) parts.Add($"academicYearId={request.AcademicYearId}");
        if (request.FromDate.HasValue) parts.Add($"fromDate={request.FromDate:yyyy-MM-dd}");
        if (request.ToDate.HasValue) parts.Add($"toDate={request.ToDate:yyyy-MM-dd}");
        if (request.DestinationId.HasValue) parts.Add($"destinationId={request.DestinationId}");
        if (request.Status.HasValue) parts.Add($"status={(int)request.Status.Value}");
        return GetAsync<SchoolManagement.Application.Accounting.DTOs.ExpenseRequestSearchResultDto>(
            $"api/v1/accounting/expense-requests?{string.Join("&", parts)}", cancellationToken);
    }

    public Task<SchoolManagement.Application.Accounting.DTOs.ExpensePaymentSearchResultDto> SearchExpensePaymentsAsync(
        SchoolManagement.Application.Accounting.DTOs.ExpenseSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var parts = new List<string> { $"page={request.Page}", $"pageSize={request.PageSize}" };
        if (request.AcademicYearId.HasValue) parts.Add($"academicYearId={request.AcademicYearId}");
        if (request.FromDate.HasValue) parts.Add($"fromDate={request.FromDate:yyyy-MM-dd}");
        if (request.ToDate.HasValue) parts.Add($"toDate={request.ToDate:yyyy-MM-dd}");
        if (request.DestinationId.HasValue) parts.Add($"destinationId={request.DestinationId}");
        return GetAsync<SchoolManagement.Application.Accounting.DTOs.ExpensePaymentSearchResultDto>(
            $"api/v1/accounting/expense-payments?{string.Join("&", parts)}", cancellationToken);
    }

    public Task<SchoolManagement.Application.Accounting.DTOs.ExpenseRequestDto> CreateExpenseRequestAsync(
        SchoolManagement.Application.Accounting.DTOs.CreateExpenseRequestRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.Accounting.DTOs.ExpenseRequestDto>(
            "api/v1/accounting/expense-requests", request, cancellationToken);

    public Task<SchoolManagement.Application.Accounting.DTOs.ExpenseRequestDto> SubmitExpenseRequestAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.Accounting.DTOs.ExpenseRequestDto>(
            $"api/v1/accounting/expense-requests/{id}/submit", new { }, cancellationToken);

    public Task<SchoolManagement.Application.Accounting.DTOs.ExpenseRequestDto> ApproveExpenseRequestAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.Accounting.DTOs.ExpenseRequestDto>(
            $"api/v1/accounting/expense-requests/{id}/approve", new { }, cancellationToken);

    public Task<SchoolManagement.Application.Accounting.DTOs.ExpensePaymentDto> CreateExpensePaymentAsync(
        SchoolManagement.Application.Accounting.DTOs.CreateExpensePaymentRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.Accounting.DTOs.ExpensePaymentDto>(
            "api/v1/accounting/expense-payments", request, cancellationToken);

    public Task<SchoolManagement.Application.Accounting.DTOs.ExpensePaymentDto> GetExpensePaymentByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        GetAsync<SchoolManagement.Application.Accounting.DTOs.ExpensePaymentDto>(
            $"api/v1/accounting/expense-payments/{id}", cancellationToken);

    public Task<IReadOnlyList<SchoolManagement.Application.Accounting.DTOs.ExpenseDestinationBalanceDto>> GetExpenseBalancesAsync(
        Guid academicYearId,
        CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<SchoolManagement.Application.Accounting.DTOs.ExpenseDestinationBalanceDto>>(
            $"api/v1/accounting/expense-balances?academicYearId={academicYearId}", cancellationToken);
}

public sealed class CloudSyncApiService : ApiServiceBase, ICloudSyncApiService
{
    public CloudSyncApiService(IHttpClientFactory httpClientFactory) : base(httpClientFactory)
    {
    }

    public Task<SchoolManagement.Application.CloudSync.DTOs.CloudSyncStatusDto> GetStatusAsync(
        CancellationToken cancellationToken = default) =>
        GetAsync<SchoolManagement.Application.CloudSync.DTOs.CloudSyncStatusDto>(
            "api/v1/cloud-sync/status", cancellationToken);

    public Task<SchoolManagement.Application.CloudSync.DTOs.CloudSyncRunResultDto> SynchronizeNowAsync(
        bool criticalOnly = false,
        CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.CloudSync.DTOs.CloudSyncRunResultDto>(
            $"api/v1/cloud-sync/synchronize?criticalOnly={criticalOnly}&requeueDeadLetters=true",
            new { },
            cancellationToken);
}

public sealed class ParentActivationApiService : ApiServiceBase, IParentActivationApiService
{
    public ParentActivationApiService(IHttpClientFactory httpClientFactory) : base(httpClientFactory)
    {
    }

    public Task<SchoolManagement.Application.ParentActivation.IssueParentActivationTokenResponse> IssueTokenAsync(
        SchoolManagement.Application.ParentActivation.IssueParentActivationTokenRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.ParentActivation.IssueParentActivationTokenResponse>(
            "api/v1/parent/activation/issue",
            request,
            cancellationToken);
}

public sealed class CourseConfigurationApiService : ApiServiceBase, ICourseConfigurationApiService
{
    public CourseConfigurationApiService(IHttpClientFactory httpClientFactory) : base(httpClientFactory) { }

    public Task<IReadOnlyList<SchoolManagement.Application.CourseConfiguration.DTOs.BranchOptionDto>> GetBranchesAsync(
        CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<SchoolManagement.Application.CourseConfiguration.DTOs.BranchOptionDto>>(
            "api/v1/course-configuration/branches", cancellationToken);

    public Task<IReadOnlyList<SchoolManagement.Application.CourseConfiguration.DTOs.AvailableCourseBranchGroupDto>> GetAvailableCoursesAsync(
        Guid pedagogicalClassId,
        CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<SchoolManagement.Application.CourseConfiguration.DTOs.AvailableCourseBranchGroupDto>>(
            $"api/v1/course-configuration/available-courses?pedagogicalClassId={pedagogicalClassId}",
            cancellationToken);

    public Task<SchoolManagement.Application.CourseConfiguration.DTOs.CourseConfigurationDto> GetConfigurationAsync(
        Guid academicYearId,
        Guid pedagogicalClassId,
        Guid classRoomId,
        CancellationToken cancellationToken = default) =>
        GetAsync<SchoolManagement.Application.CourseConfiguration.DTOs.CourseConfigurationDto>(
            $"api/v1/course-configuration?academicYearId={academicYearId}&pedagogicalClassId={pedagogicalClassId}&classRoomId={classRoomId}",
            cancellationToken);

    public Task<SchoolManagement.Application.CourseConfiguration.DTOs.CourseConfigurationDto> SaveConfigurationAsync(
        SchoolManagement.Application.CourseConfiguration.DTOs.SaveCourseConfigurationRequest request,
        CancellationToken cancellationToken = default) =>
        PutAsync<SchoolManagement.Application.CourseConfiguration.DTOs.CourseConfigurationDto>(
            "api/v1/course-configuration", request, cancellationToken);

    public Task<SchoolManagement.Application.CourseConfiguration.DTOs.CreateCatalogCourseResultDto> CreateCatalogCourseAsync(
        SchoolManagement.Application.CourseConfiguration.DTOs.CreateCatalogCourseRequest request,
        CancellationToken cancellationToken = default) =>
        PostAsync<SchoolManagement.Application.CourseConfiguration.DTOs.CreateCatalogCourseResultDto>(
            "api/v1/course-configuration/courses", request, cancellationToken);
}
