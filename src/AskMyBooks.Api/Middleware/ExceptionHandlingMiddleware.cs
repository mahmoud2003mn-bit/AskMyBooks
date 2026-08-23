using AskMyBooks.Application.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace AskMyBooks.Api.Middleware;

/// <summary>
/// Single place that turns exceptions into HTTP responses, so controllers stay
/// free of try/catch and every endpoint gets the same error contract for free.
///
///   NotFoundException     -> 404
///   ArgumentException     -> 400  (domain/DTO validation failures)
///   LlmContractException  -> 400  (the model returned something unusable —
///                             this is what keeps a bad LLM response in
///                             POST /books/extract from ever crashing the API)
///   anything else         -> 500, logged, generic message to the client
/// </summary>
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
            var (statusCode, title, detail) = Map(ex);

            if (statusCode == StatusCodes.Status500InternalServerError)
            {
                _logger.LogError(ex, "Unhandled exception on {Method} {Path}",
                    context.Request.Method, context.Request.Path);
            }
            else if (ex is LlmContractException llmEx)
            {
                // Log the raw model output for debugging, but never send it to the client.
                _logger.LogWarning(ex, "LLM contract violation on {Path}. Raw output: {RawOutput}",
                    context.Request.Path, llmEx.RawModelOutput);
            }
            else
            {
                _logger.LogInformation(ex, "{Title} on {Path}: {Detail}",
                    title, context.Request.Path, detail);
            }

            if (context.Response.HasStarted)
                throw;

            context.Response.ContentType = "application/problem+json";
            context.Response.StatusCode = statusCode;

            var problem = new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = detail,
                Instance = context.Request.Path
            };

            await context.Response.WriteAsJsonAsync(problem);
        }
    }

    private static (int StatusCode, string Title, string Detail) Map(Exception ex) => ex switch
    {
        NotFoundException => (
            StatusCodes.Status404NotFound,
            "Not Found",
            ex.Message),

        LlmContractException => (
            StatusCodes.Status400BadRequest,
            "Bad LLM Response",
            "The model did not return a usable book record from that text. Try rephrasing or adding more detail."),

        ArgumentException => (
            StatusCodes.Status400BadRequest,
            "Invalid Request",
            ex.Message),

        _ => (
            StatusCodes.Status500InternalServerError,
            "Unexpected Error",
            "An unexpected error occurred. Please try again.")
    };
}