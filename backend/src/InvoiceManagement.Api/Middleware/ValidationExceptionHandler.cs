using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace InvoiceManagement.Api.Middleware;

/// <summary>
/// Handles FluentValidation exceptions by returning 400 Bad Request with validation errors
/// in ProblemDetails format (RFC 7807). Registered before GlobalExceptionHandler so it
/// takes priority over the generic 500 handler.
/// </summary>
public sealed class ValidationExceptionHandler : IExceptionHandler
{
    private readonly ILogger<ValidationExceptionHandler> _logger;

    public ValidationExceptionHandler(ILogger<ValidationExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken ct)
    {
        if (exception is not ValidationException validationException)
            return false;

        _logger.LogWarning(
            "Validation failed for {Path}: {ErrorCount} error(s)",
            httpContext.Request.Path,
            validationException.Errors.Count());

        var errors = validationException.Errors
            .Select(e => e.ErrorMessage)
            .ToList();

        var problemDetails = new ProblemDetails
        {
            Title = "Validation failed",
            Status = StatusCodes.Status400BadRequest,
            Detail = errors.FirstOrDefault() ?? "One or more validation errors occurred.",
            Instance = httpContext.Request.Path
        };

        problemDetails.Extensions["errors"] = errors;

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        httpContext.Response.ContentType = "application/problem+json";

        await httpContext.Response.WriteAsJsonAsync(problemDetails, ct);

        return true;
    }
}
