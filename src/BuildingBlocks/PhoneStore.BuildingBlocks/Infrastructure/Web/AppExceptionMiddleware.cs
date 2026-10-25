using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using PhoneStore.BuildingBlocks.Common;
using PhoneStore.BuildingBlocks.Exceptions;

namespace PhoneStore.BuildingBlocks.Infrastructure.Web;

/// <summary>
/// Chuyển AppException thành response chuẩn của API (ErrorResponse).
/// </summary>
public class AppExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AppExceptionMiddleware> _logger;

    public AppExceptionMiddleware(RequestDelegate next, ILogger<AppExceptionMiddleware> logger)
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
        catch (AppException exception)
        {
            _logger.LogWarning(exception, "AppException: {Code}", exception.Code);

            var response = new ErrorResponse
            {
                Code = exception.Code,
                Message = exception.Message,
                RequestId = context.TraceIdentifier,
                Timestamp = DateTimeOffset.UtcNow
            };

            if (exception is ValidationException validationException)
            {
                response.FieldErrors = validationException.Errors;
            }

            context.Response.StatusCode = (int)exception.StatusCode;
            context.Response.ContentType = "application/json; charset=utf-8";
            await context.Response.WriteAsJsonAsync(response);
        }
    }
}
