using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using SchoolManagement.Application.ParentActivation.BootstrapRelay;
using SchoolManagement.Bootstrap.API.Options;

namespace SchoolManagement.Bootstrap.API.Filters;

/// <summary>
/// Auth service école → Bootstrap registre (<c>X-Bootstrap-Relay-Key</c> = <c>Bootstrap:RelayApiKey</c>).
/// </summary>
public sealed class BootstrapRelayKeyAuthorizationFilter : IAsyncActionFilter
{
    public const string HeaderName = BootstrapRelayAuthConstants.LegacySharedKeyHeaderName;

    private readonly BootstrapOptions _options;

    public BootstrapRelayKeyAuthorizationFilter(IOptions<BootstrapOptions> options)
    {
        _options = options.Value;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (string.IsNullOrWhiteSpace(_options.RelayApiKey))
        {
            context.Result = new ObjectResult(new { error = "Bootstrap relay non configuré (Bootstrap:RelayApiKey)." })
            {
                StatusCode = StatusCodes.Status503ServiceUnavailable,
            };
            return;
        }

        if (!TryGetHeader(context.HttpContext.Request.Headers, HeaderName, out var provided))
        {
            context.Result = new UnauthorizedObjectResult(new { error = "Clé relay Bootstrap manquante." });
            return;
        }

        if (!string.Equals(provided, _options.RelayApiKey, StringComparison.Ordinal))
        {
            context.Result = new UnauthorizedObjectResult(new { error = "Clé relay Bootstrap invalide." });
            return;
        }

        await next();
    }

    private static bool TryGetHeader(IHeaderDictionary headers, string headerName, out string? value)
    {
        value = null;
        if (!headers.TryGetValue(headerName, out var raw))
        {
            return false;
        }

        value = raw.ToString();
        return !string.IsNullOrWhiteSpace(value);
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireBootstrapRelayKeyAttribute : TypeFilterAttribute
{
    public RequireBootstrapRelayKeyAttribute()
        : base(typeof(BootstrapRelayKeyAuthorizationFilter))
    {
    }
}
