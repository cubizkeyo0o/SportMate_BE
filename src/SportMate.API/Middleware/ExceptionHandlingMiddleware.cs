using Microsoft.AspNetCore.Mvc;
using SportMate.Application.Common;
using SportMate.Domain.Common;

public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            var problem = ToProblemDetails(exception);
            if (problem.Status == StatusCodes.Status500InternalServerError)
            {
                logger.LogError(exception, "An unhandled exception occurred while processing the request.");
            }

            if (context.Response.HasStarted)
            {
                throw;
            }

            problem.Instance = context.Request.Path;
            context.Response.Clear();
            context.Response.StatusCode = problem.Status!.Value;
            await context.Response.WriteAsJsonAsync(problem, problem.GetType());
        }
    }

    // Messages of the mapped exceptions are written for clients and contain no secrets.
    private static ProblemDetails ToProblemDetails(Exception exception) => exception switch
    {
        ValidationException validation => new ValidationProblemDetails(
            validation.Errors.ToDictionary(e => e.Key, e => e.Value))
        {
            Status = StatusCodes.Status400BadRequest,
            Title = validation.Message
        },
        AuthenticationFailedException => Problem(StatusCodes.Status401Unauthorized, exception.Message),
        ForbiddenException => Problem(StatusCodes.Status403Forbidden, exception.Message),
        ConflictException => Problem(StatusCodes.Status409Conflict, exception.Message),
        DomainException => Problem(StatusCodes.Status422UnprocessableEntity, exception.Message),
        _ => Problem(StatusCodes.Status500InternalServerError, "An unexpected error occurred.")
    };

    private static ProblemDetails Problem(int status, string title) => new() { Status = status, Title = title };
}
