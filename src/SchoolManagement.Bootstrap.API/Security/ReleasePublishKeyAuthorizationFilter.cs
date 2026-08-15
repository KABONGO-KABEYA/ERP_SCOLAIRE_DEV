using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using SchoolManagement.Bootstrap.API.Options;

namespace SchoolManagement.Bootstrap.API.Security;

/// <summary>
/// Auth publication catalogue : header <c>X-Bootstrap-Release-Key</c> = <c>Bootstrap:ReleasePublishApiKey</c>.
/// Distinct de <c>Bootstrap:RelayApiKey</c>. La valeur de la clé n'est jamais journalisée.
/// </summary>
public sealed class ReleasePublishKeyAuthorizationFilter : IAsyncActionFilter
{
    public const string HeaderName = "X-Bootstrap-Release-Key";

    private readonly BootstrapOptions _options;

    public ReleasePublishKeyAuthorizationFilter(IOptions<BootstrapOptions> options)
    {
        _options = options.Value;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (string.IsNullOrWhiteSpace(_options.ReleasePublishApiKey))
        {
            context.Result = new ObjectResult(new { error = "Publication catalogue non configurée (Bootstrap:ReleasePublishApiKey)." })
            {
                StatusCode = StatusCodes.Status503ServiceUnavailable,
            };
            return;
        }

        if (!TryGetHeader(context.HttpContext.Request.Headers, HeaderName, out var provided)
            || string.IsNullOrWhiteSpace(provided))
        {
            context.Result = new UnauthorizedObjectResult(new { error = "Clé de publication catalogue manquante." });
            return;
        }

        if (!FixedEquals(provided, _options.ReleasePublishApiKey))
        {
            context.Result = new UnauthorizedObjectResult(new { error = "Clé de publication catalogue invalide." });
            return;
        }

        await next();
    }

    public static bool TryGetHeader(IHeaderDictionary headers, string headerName, out string? value)
    {
        value = null;
        if (!headers.TryGetValue(headerName, out var raw))
        {
            return false;
        }

        value = raw.ToString();
        return !string.IsNullOrWhiteSpace(value);
    }

    public static bool FixedEquals(string provided, string expected)
    {
        var left = Encoding.UTF8.GetBytes(provided);
        var right = Encoding.UTF8.GetBytes(expected);
        if (left.Length != right.Length)
        {
            // Compare against a dummy buffer so timing does not leak length; still reject.
            CryptographicOperations.FixedTimeEquals(left, left);
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(left, right);
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireReleasePublishKeyAttribute : TypeFilterAttribute
{
    public RequireReleasePublishKeyAttribute()
        : base(typeof(ReleasePublishKeyAuthorizationFilter))
    {
    }
}
