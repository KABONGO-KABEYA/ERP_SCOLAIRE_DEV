using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using SchoolManagement.Bootstrap.API.Options;

namespace SchoolManagement.Bootstrap.API.Security;

/// <summary>
/// Auth provisionnement credentials agent : header <c>X-Bootstrap-Agent-Provision-Key</c>
/// = <c>Bootstrap:AgentProvisionApiKey</c>. Distincte de Relay et ReleasePublish.
/// La valeur n'est jamais journalisée.
/// </summary>
public sealed class AgentProvisionKeyAuthorizationFilter : IAsyncActionFilter
{
    public const string HeaderName = "X-Bootstrap-Agent-Provision-Key";

    private readonly BootstrapOptions _options;

    public AgentProvisionKeyAuthorizationFilter(IOptions<BootstrapOptions> options)
    {
        _options = options.Value;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (string.IsNullOrWhiteSpace(_options.AgentProvisionApiKey))
        {
            context.Result = new ObjectResult(new { error = "Provisionnement agent non configuré (Bootstrap:AgentProvisionApiKey)." })
            {
                StatusCode = StatusCodes.Status503ServiceUnavailable,
            };
            return;
        }

        if (!TryGetHeader(context.HttpContext.Request.Headers, HeaderName, out var provided)
            || string.IsNullOrWhiteSpace(provided))
        {
            context.Result = new UnauthorizedObjectResult(new { error = "Clé de provisionnement agent manquante." });
            return;
        }

        if (!FixedEquals(provided, _options.AgentProvisionApiKey))
        {
            context.Result = new UnauthorizedObjectResult(new { error = "Clé de provisionnement agent invalide." });
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
            CryptographicOperations.FixedTimeEquals(left, left);
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(left, right);
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireAgentProvisionKeyAttribute : TypeFilterAttribute
{
    public RequireAgentProvisionKeyAttribute()
        : base(typeof(AgentProvisionKeyAuthorizationFilter))
    {
    }
}
