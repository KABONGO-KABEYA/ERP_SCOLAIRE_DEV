using System.Text.Json;
using Microsoft.Extensions.Options;
using SchoolManagement.API.Options;
using SchoolManagement.Shared.Models;

namespace SchoolManagement.API.Middleware;

/// <summary>
/// Sur l'instance Cloud : refuse les écritures (la base locale reste la Source of Truth).
/// Exceptions : auth, health, et saisie de notes enseignants (/api/v1/grades/entries).
/// </summary>
public sealed class CloudReadOnlyMiddleware
{
    private static readonly HashSet<string> AllowedWritePrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/v1/auth",
        "/api/v1/health",
        "/api/health",
        "/api/v1/grades/entries",
        // Publication des versions applicatives (admin) — doit aussi fonctionner sur le Cloud.
        "/api/v1/update/versions"
    };

    private readonly RequestDelegate _next;
    private readonly DeploymentOptions _options;

    public CloudReadOnlyMiddleware(RequestDelegate next, IOptions<DeploymentOptions> options)
    {
        _next = next;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_options.IsCloudReadOnly || IsSafeMethod(context.Request.Method))
        {
            await _next(context);
            return;
        }

        var path = context.Request.Path.Value ?? string.Empty;
        if (IsAllowedWrite(path))
        {
            await _next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/json";
        var payload = ApiResponse<object>.Fail(
            "API Cloud en lecture seule. Les modifications doivent être faites sur le serveur local de l'établissement.");
        await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }

    private static bool IsSafeMethod(string method) =>
        HttpMethods.IsGet(method)
        || HttpMethods.IsHead(method)
        || HttpMethods.IsOptions(method);

    private static bool IsAllowedWrite(string path) =>
        AllowedWritePrefixes.Any(prefix =>
            path.Equals(prefix, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase));
}
