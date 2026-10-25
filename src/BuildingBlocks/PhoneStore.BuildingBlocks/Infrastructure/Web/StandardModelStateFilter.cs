using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PhoneStore.BuildingBlocks.Common;

namespace PhoneStore.BuildingBlocks.Infrastructure.Web;

/// <summary>
/// Chuẩn hóa lỗi validation của ASP.NET (DataAnnotations) về đúng định dạng
/// ErrorResponse mà AppExceptionMiddleware sử dụng, để client chỉ cần xử lý
/// một cấu trúc lỗi duy nhất.
/// </summary>
public class StandardModelStateFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (context.ModelState.IsValid)
        {
            return;
        }

        var fieldErrors = context.ModelState
            .Where(entry => entry.Value?.Errors.Count > 0)
            .ToDictionary(
                entry => entry.Key,
                entry => entry.Value!.Errors
                    .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage)
                        ? "Giá trị không hợp lệ."
                        : error.ErrorMessage)
                    .ToArray(),
                StringComparer.Ordinal);

        var response = new ErrorResponse
        {
            Code = "VALIDATION_ERROR",
            Message = "Dữ liệu yêu cầu không hợp lệ.",
            FieldErrors = fieldErrors,
            RequestId = context.HttpContext.TraceIdentifier,
            Timestamp = DateTimeOffset.UtcNow
        };

        context.Result = new ObjectResult(response) { StatusCode = StatusCodes.Status400BadRequest };
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
    }
}

/// <summary>
/// Ghi nhận lỗi model state khi model binding thất bại (ví dụ JSON không đúng kiểu),
/// trả về ErrorResponse thay vì format mặc định của ASP.NET.
/// </summary>
public class InvalidModelStateResponseFactory
{
    public static IActionResult Create(ActionContext context)
    {
        var fieldErrors = context.ModelState
            .Where(entry => entry.Value?.Errors.Count > 0)
            .ToDictionary(
                entry => entry.Key,
                entry => entry.Value!.Errors
                    .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage)
                        ? "Giá trị không hợp lệ."
                        : error.ErrorMessage)
                    .ToArray(),
                StringComparer.Ordinal);

        var response = new ErrorResponse
        {
            Code = "VALIDATION_ERROR",
            Message = "Dữ liệu yêu cầu không hợp lệ.",
            FieldErrors = fieldErrors,
            RequestId = context.HttpContext.TraceIdentifier,
            Timestamp = DateTimeOffset.UtcNow
        };

        return new ObjectResult(response) { StatusCode = StatusCodes.Status400BadRequest };
    }
}
