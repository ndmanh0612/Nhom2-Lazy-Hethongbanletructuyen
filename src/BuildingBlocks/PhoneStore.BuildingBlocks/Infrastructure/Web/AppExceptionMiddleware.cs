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
        catch (Exception exception)
        {
            // N2LHTBLTT-14: Mọi lỗi không lường trước đều trả về đúng định dạng ErrorResponse
            // thay vì để ASP.NET trả về HTML/500 trần, để client xử lý thống nhất.
            _logger.LogError(exception, "Unhandled exception: {RequestId}", context.TraceIdentifier);

            var response = new ErrorResponse
            {
                Code = "INTERNAL_SERVER_ERROR",
                Message = "Đã xảy ra lỗi không mong muốn. Vui lòng thử lại sau.",
                RequestId = context.TraceIdentifier,
                Timestamp = DateTimeOffset.UtcNow
            };

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json; charset=utf-8";
            await context.Response.WriteAsJsonAsync(response);
        }
    }
}
