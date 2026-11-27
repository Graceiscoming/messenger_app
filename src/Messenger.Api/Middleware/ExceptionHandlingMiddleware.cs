using System.Text.Json;
using Messenger.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Messenger.Api.Middleware;

/// <summary>Maps domain exceptions (and a few framework ones) to RFC 7807 ProblemDetails. Unknown errors become a bare 500.</summary>
public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // client went away; nothing to write
        }
        catch (Exception ex) when (!context.Response.HasStarted)
        {
            var problem = Map(ex);
            if (problem.Status >= 500) logger.LogError(ex, "Unhandled exception");
            problem.Extensions["traceId"] = context.TraceIdentifier;

            context.Response.Clear();
            context.Response.StatusCode = problem.Status!.Value;
            context.Response.ContentType = "application/problem+json";
            await JsonSerializer.SerializeAsync(context.Response.Body, problem, Json, context.RequestAborted);
        }
    }

    private static ProblemDetails Map(Exception ex)
    {
        switch (ex)
        {
            case ValidationException v:
                var vp = Make(400, "Validation failed", v.Message);
                if (v.Errors.Count > 0) vp.Extensions["errors"] = v.Errors;
                return vp;
            case NotFoundException: return Make(404, "Not found", ex.Message);
            case ForbiddenException: return Make(403, "Forbidden", ex.Message);
            case ConflictException: return Make(409, "Conflict", ex.Message);
            case UnauthorizedException: return Make(401, "Unauthorized", ex.Message);
            case PayloadTooLargeException: return Make(413, "Payload too large", ex.Message);
            case BadHttpRequestException b:
                return Make(b.StatusCode, b.StatusCode == 413 ? "Payload too large" : "Bad request", "The request could not be processed.");
            case InvalidDataException d:
                return d.Message.Contains("limit", StringComparison.OrdinalIgnoreCase)
                    ? Make(413, "Payload too large", "The request body is too large.")
                    : Make(400, "Bad request", "The request body is malformed.");
            default:
                return Make(500, "Internal server error", "An unexpected error occurred.");
        }
    }

    private static ProblemDetails Make(int status, string title, string detail) => new()
    {
        Status = status,
        Title = title,
        Detail = detail,
        Type = $"https://httpstatuses.io/{status}",
    };
}
