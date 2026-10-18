using ComputerLending.Core.DTOs.Common;

namespace ComputerLending.API.Middleware;

public class ExceptionHandlingMiddleware
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

            if (context.Response.StatusCode == StatusCodes.Status404NotFound && !context.Response.HasStarted)
                await WriteAsync(context, StatusCodes.Status404NotFound, "הנתיב לא נמצא.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");

            if (!context.Response.HasStarted)
                await WriteAsync(context, StatusCodes.Status500InternalServerError, "אירעה שגיאה לא צפויה.");
        }
    }

    private static async Task WriteAsync(HttpContext context, int status, string message)
    {
        var correlationId = context.Items[CorrelationIdMiddleware.ItemKey] as string
            ?? context.Request.Headers[CorrelationIdMiddleware.HeaderName].FirstOrDefault()
            ?? string.Empty;

        context.Response.Clear();
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";
        context.Response.Headers[CorrelationIdMiddleware.HeaderName] = correlationId;

        await context.Response.WriteAsJsonAsync(new ErrorResponse
        {
            Status = status,
            Message = message,
            CorrelationId = correlationId
        });
    }
}
