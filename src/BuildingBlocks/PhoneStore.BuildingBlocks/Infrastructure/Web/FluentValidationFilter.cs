using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PhoneStore.BuildingBlocks.Common;

namespace PhoneStore.BuildingBlocks.Infrastructure.Web;

/// <summary>
/// Tự động chạy FluentValidation cho mọi request có body/query được bind vào
/// tham số có validator tương ứng trong DI.
///
/// Lỗi trả về đúng định dạng ErrorResponse (giống AppExceptionMiddleware) để client
/// chỉ cần xử lý một cấu trúc lỗi duy nhất.
/// </summary>
public class FluentValidationFilter : IAsyncActionFilter
{
    /// <summary>
    /// Chạy trước ModelStateInvalidFilter của [ApiController] (Order = -2000)
    /// để FluentValidation là nguồn validation chính, trả về message tiếng Việt.
    /// </summary>
    public int Order => -3000;

    private readonly IServiceProvider _serviceProvider;

    public FluentValidationFilter(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
            {
                continue;
            }

            var argumentType = argument.GetType();
            var validatorServiceType = typeof(IValidator<>).MakeGenericType(argumentType);

            // Lấy validator từ DI: nếu không có validator cho kiểu này thì bỏ qua.
            var validator = _serviceProvider.GetService(validatorServiceType) as IValidator;
            if (validator is null)
            {
                continue;
            }

            var validationResult = await validator.ValidateAsync(
                new ValidationContext<object>(argument),
                context.HttpContext.RequestAborted);

            if (validationResult.IsValid)
            {
                continue;
            }

            var fieldErrors = validationResult.Errors
                .GroupBy(failure => failure.PropertyName, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(failure => failure.ErrorMessage).ToArray(),
                    StringComparer.Ordinal);

            var errorResponse = new ErrorResponse
            {
                Code = "VALIDATION_ERROR",
                Message = "Dữ liệu yêu cầu không hợp lệ.",
                FieldErrors = fieldErrors,
                RequestId = context.HttpContext.TraceIdentifier,
                Timestamp = DateTimeOffset.UtcNow
            };

            context.Result = new ObjectResult(errorResponse) { StatusCode = StatusCodes.Status400BadRequest };
            return;
        }

        await next();
    }
}
