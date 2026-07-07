namespace SchoolManagement.Infrastructure.Services;

using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SchoolManagement.Application.Common.Interfaces;
using SchoolManagement.Shared.Constants;

public sealed class HttpContextCurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextCurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? UserId
    {
        get
        {
            var sub = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Name)
                ?? _httpContextAccessor.HttpContext?.User?.FindFirstValue("sub");
            return Guid.TryParse(sub, out var id) ? id : null;
        }
    }

    public Guid? SchoolId
    {
        get
        {
            var schoolId = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypesCustom.SchoolId);
            return Guid.TryParse(schoolId, out var id) ? id : null;
        }
    }

    public string? UserName =>
        _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Name)
        ?? _httpContextAccessor.HttpContext?.User?.Identity?.Name;

    public IReadOnlyList<string> Permissions =>
        _httpContextAccessor.HttpContext?.User?
            .FindAll(ClaimTypesCustom.Permissions)
            .Select(c => c.Value)
            .ToList() ?? [];

    public bool HasPermission(string permission) =>
        Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase)
        || Permissions.Contains(Shared.Constants.Permissions.AdminFull, StringComparer.OrdinalIgnoreCase);
}
