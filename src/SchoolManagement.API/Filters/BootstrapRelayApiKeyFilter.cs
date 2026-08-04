using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SchoolManagement.Application.ParentActivation.BootstrapRelay;

namespace SchoolManagement.API.Filters;

/// <summary>Endpoints d'activation école — relay Bootstrap uniquement (<see cref="IBootstrapRelayRequestValidator"/>).</summary>
public sealed class BootstrapRelayApiKeyFilter : IAsyncActionFilter
{
    public const string HeaderName = BootstrapRelayAuthConstants.LegacySharedKeyHeaderName;

    private readonly IBootstrapRelayRequestValidator _validator;

    public BootstrapRelayApiKeyFilter(IBootstrapRelayRequestValidator validator)
    {
        _validator = validator;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var headers = context.HttpContext.Request.Headers
            .ToDictionary(h => h.Key, h => (string?)h.Value.ToString(), StringComparer.OrdinalIgnoreCase);

        var result = await _validator.ValidateAsync(headers, context.HttpContext.RequestAborted);
        if (!result.IsSuccess)
        {
            var status = result.HttpStatusCode ?? StatusCodes.Status401Unauthorized;
            context.Result = status switch
            {
                StatusCodes.Status503ServiceUnavailable => new ObjectResult(new { error = result.ErrorMessage })
                {
                    StatusCode = status
                },
                _ => new UnauthorizedObjectResult(new { error = result.ErrorMessage })
            };
            return;
        }

        await next();
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class BootstrapRelayOnlyAttribute : TypeFilterAttribute
{
    public BootstrapRelayOnlyAttribute()
        : base(typeof(BootstrapRelayApiKeyFilter))
    {
    }
}
