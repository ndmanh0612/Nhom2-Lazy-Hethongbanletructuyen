using FluentValidation;
using PhoneStore.BuildingBlocks.Exceptions;
using AppValidationException = PhoneStore.BuildingBlocks.Exceptions.ValidationException;

namespace PhoneStore.BuildingBlocks.Validation;

/// <summary>
/// Validator dùng chung cho các module. Đăng ký vào DI qua
/// <see cref="ValidationRegistration.AddBuildingBlocksValidation"/>.
/// </summary>
public abstract class AppValidator<T> : AbstractValidator<T>
{
    protected AppValidator()
    {
        ClassLevelCascadeMode = CascadeMode.Stop;
        RuleLevelCascadeMode = CascadeMode.Stop;
    }

    /// <summary>
    /// Chuyển lỗi FluentValidation sang định dạng FieldErrors của ErrorResponse.
    /// </summary>
    protected static IReadOnlyDictionary<string, string[]> ToFieldErrors(
        IEnumerable<FluentValidation.Results.ValidationFailure> failures)
    {
        return failures
            .GroupBy(failure => failure.PropertyName, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(failure => failure.ErrorMessage).ToArray(),
                StringComparer.Ordinal);
    }
}

/// <summary>
/// Extension để validator tự chuyển kết quả validate thành ValidationException của BuildingBlocks.
/// </summary>
public static class ValidatorExtensions
{
    public static async Task<T> ValidateAndThrowAsync<T>(
        this IValidator<T> validator,
        T instance,
        CancellationToken cancellationToken = default)
    {
        var result = await validator.ValidateAsync(instance, cancellationToken);

        if (!result.IsValid)
        {
            var errors = result.Errors
                .GroupBy(failure => failure.PropertyName, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(failure => failure.ErrorMessage).ToArray(),
                    StringComparer.Ordinal);

            throw new AppValidationException(errors);
        }

        return instance;
    }
}
