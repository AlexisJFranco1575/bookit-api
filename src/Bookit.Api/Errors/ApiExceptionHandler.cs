using Bookit.Application.Auth;
using Bookit.Application.Common;
using Bookit.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Bookit.Api.Errors;

/// <summary>
/// Turns exceptions into standard problem+json responses. Business errors keep their message (it is written
/// for the user); anything unexpected is logged and answered with a generic 500 so no internals leak.
/// </summary>
public sealed class ApiExceptionHandler(
    IProblemDetailsService problemDetails,
    ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // The client went away; there is nobody to answer.
        if (exception is OperationCanceledException)
        {
            return false;
        }

        var (status, title, detail) = exception switch
        {
            DomainException e => (StatusCodes.Status422UnprocessableEntity, "The request breaks a business rule.", e.Message),
            NotFoundException e => (StatusCodes.Status404NotFound, "Not found.", e.Message),
            ConflictException e => (StatusCodes.Status409Conflict, "Conflict.", e.Message),
            InvalidCredentialsException e => (StatusCodes.Status401Unauthorized, "Invalid credentials.", e.Message),
            BadHttpRequestException => (
                StatusCodes.Status400BadRequest,
                "Bad request.",
                "The request is malformed or contains invalid values."),
            _ => (StatusCodes.Status500InternalServerError, "Unexpected error.", "Something went wrong on our side.")
        };

        if (status == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception for {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
        }

        httpContext.Response.StatusCode = status;

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = detail
            }
        });
    }
}