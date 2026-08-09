using System.Net;
using System.Text.Json;
using SchoolManagement.Application.Common;
using SchoolManagement.Domain.Exceptions;
using SchoolManagement.Shared.Models;

namespace SchoolManagement.API.Middleware;

public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur non gérée : {Message}", ex.Message);
            await WriteErrorResponseAsync(context, ex);
        }
    }

    private static async Task WriteErrorResponseAsync(HttpContext context, Exception exception)
    {
        var statusCode = exception switch
        {
            // Ressource d'un autre établissement : répondre comme si elle n'existait pas.
            SchoolTenancyAccessDeniedException => HttpStatusCode.NotFound,
            UnauthorizedAccessException => HttpStatusCode.Unauthorized,
            KeyNotFoundException => HttpStatusCode.NotFound,
            ArgumentException => HttpStatusCode.BadRequest,
            DomainException => HttpStatusCode.Conflict,
            _ => HttpStatusCode.InternalServerError
        };

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var response = ApiResponse<object>.Fail(ResolveClientMessage(exception, statusCode));

        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }

    private static string ResolveClientMessage(Exception exception, HttpStatusCode statusCode)
    {
        if (statusCode != HttpStatusCode.InternalServerError)
        {
            return exception.Message;
        }

        return exception switch
        {
            Microsoft.EntityFrameworkCore.DbUpdateException dbUpdate when dbUpdate.InnerException?.Message is { Length: > 0 } sqlMessage
                => $"Erreur base de données : {sqlMessage}",
            _ => "Une erreur interne est survenue."
        };
    }
}
