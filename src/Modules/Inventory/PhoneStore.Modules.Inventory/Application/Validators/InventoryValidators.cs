using FluentValidation;
using PhoneStore.BuildingBlocks.Validation;
using PhoneStore.Modules.Inventory.Application.DTOs;

namespace PhoneStore.Modules.Inventory.Application.Validators;

/// <summary>
/// FR-14 (N2LHTBLTT-21): Validation cho các thao tác quản trị tồn kho.
/// </summary>
public class ImportStockRequestValidator : AppValidator<ImportStockRequest>
{
    public ImportStockRequestValidator()
    {
        RuleFor(request => request.ProductVariantId)
            .GreaterThan(0)
            .WithMessage("ProductVariantId phải lớn hơn 0.");

        RuleFor(request => request.Quantity)
            .GreaterThan(0)
            .WithMessage("Số lượng nhập kho phải lớn hơn 0.");

        RuleFor(request => request.Note)
            .MaximumLength(500)
            .WithMessage("Ghi chú tối đa 500 ký tự.");
    }
}

public class ImportStockBatchRequestValidator : AppValidator<ImportStockBatchRequest>
{
    public ImportStockBatchRequestValidator()
    {
        RuleFor(request => request.Items)
            .NotNull()
            .WithMessage("Danh sách nhập kho không được để trống.");

        RuleFor(request => request.Items)
            .Must(items => items.Count <= 200)
            .When(request => request.Items is not null)
            .WithMessage("Một lần nhập kho tối đa 200 dòng.");

        RuleForEach(request => request.Items).SetValidator(new ImportStockRequestValidator());

        RuleFor(request => request.Note)
            .MaximumLength(500)
            .WithMessage("Ghi chú tối đa 500 ký tự.");
    }
}

public class AdjustStockRequestValidator : AppValidator<AdjustStockRequest>
{
    public AdjustStockRequestValidator()
    {
        RuleFor(request => request.ProductVariantId)
            .GreaterThan(0)
            .WithMessage("ProductVariantId phải lớn hơn 0.");

        RuleFor(request => request.Delta)
            .NotEqual(0)
            .WithMessage("Số lượng điều chỉnh không được bằng 0.");

        RuleFor(request => request.Note)
            .MaximumLength(500)
            .WithMessage("Ghi chú tối đa 500 ký tự.");
    }
}

public class StockMovementFilterQueryValidator : AppValidator<StockMovementFilterQuery>
{
    public StockMovementFilterQueryValidator()
    {
        RuleFor(query => query.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Số trang phải bắt đầu từ 1.");

        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("Số bản ghi mỗi trang phải từ 1 đến 100.");

        RuleFor(query => query.ProductVariantId)
            .GreaterThan(0)
            .When(query => query.ProductVariantId.HasValue)
            .WithMessage("ProductVariantId phải lớn hơn 0.");

        RuleFor(query => query.Type)
            .Must(type => string.IsNullOrWhiteSpace(type)
                || type is "IMPORT" or "ADJUSTMENT" or "RESERVATION" or "RESTORE")
            .WithMessage("Loại biến động chỉ nhận IMPORT, ADJUSTMENT, RESERVATION hoặc RESTORE.");
    }
}
