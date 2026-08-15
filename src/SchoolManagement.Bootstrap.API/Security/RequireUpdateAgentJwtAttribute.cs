using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SchoolManagement.Bootstrap.API.Contracts;
using SchoolManagement.Bootstrap.API.Options;
using SchoolManagement.Bootstrap.API.Services;

namespace SchoolManagement.Bootstrap.API.Security;

/// <summary>
/// Bearer JWT <c>update_agent</c> : signature, aud, token_type, exp ;
/// credential Active (lookup par <c>sub</c> = ClientId) ; école IsActive.
/// Le <c>jti</c> est un identifiant d'émission, pas l'id du credential.
/// </summary>
public sealed class UpdateAgentJwtAuthorizationFilter : IAsyncActionFilter
{
    private readonly IUpdateAgentCredentialService _agents;
    private readonly BootstrapOptions _options;

    public UpdateAgentJwtAuthorizationFilter(
        IUpdateAgentCredentialService agents,
        IOptions<BootstrapOptions> options)
    {
        _agents = agents;
        _options = options.Value;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!UpdateAgentJwt.TryValidateSigningKey(_options.AgentJwtSigningKey, out var keyError))
        {
            context.Result = new ObjectResult(new { error = keyError })
            {
                StatusCode = StatusCodes.Status503ServiceUnavailable,
            };
            return;
        }

        var header = context.HttpContext.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(header)
            || !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            context.Result = new UnauthorizedObjectResult(new { error = "Jeton agent manquant." });
            return;
        }

        var token = header["Bearer ".Length..].Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            context.Result = new UnauthorizedObjectResult(new { error = "Jeton agent manquant." });
            return;
        }

        try
        {
            var auth = await _agents.AuthenticateBearerAsync(token, context.HttpContext.RequestAborted);
            context.HttpContext.Items[UpdateAgentAuthContext.HttpContextItemKey] = auth;
        }
        catch (AgentException ex)
        {
            context.Result = new ObjectResult(new { error = ex.Message })
            {
                StatusCode = ex.StatusCode,
            };
            return;
        }
        catch (SecurityTokenExpiredException)
        {
            context.Result = new UnauthorizedObjectResult(new { error = "Jeton agent expiré." });
            return;
        }
        catch (SecurityTokenException)
        {
            context.Result = new UnauthorizedObjectResult(new { error = "Jeton agent invalide." });
            return;
        }

        await next();
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireUpdateAgentJwtAttribute : TypeFilterAttribute
{
    public RequireUpdateAgentJwtAttribute()
        : base(typeof(UpdateAgentJwtAuthorizationFilter))
    {
    }
}
