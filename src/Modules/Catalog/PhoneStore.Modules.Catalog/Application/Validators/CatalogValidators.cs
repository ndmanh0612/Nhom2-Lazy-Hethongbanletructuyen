using FluentValidation;
using PhoneStore.BuildingBlocks.Validation;
using PhoneStore.Modules.Catalog.Application.DTOs;

namespace PhoneStore.Modules.Catalog.Application.Validators;

/// <summary>
/// FR-03 / FR-04: Validation cho các request quản trị danh mục, thương hiệu
/// và tham số lọc/phân trang danh sách sản phẩm.
/// </summary>
public class CreateCategoryRequestValidator : AppValidator<CreateCategoryRequest>
{
    public CreateCategoryRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty()
            .MaximumLength(100)
            .WithMessage("Tên danh mục không được để trống và tối đa 100 ký tự.");
    }
}

public class CreateBrandRequestValidator : AppValidator<CreateBrandRequest>
{
    public CreateBrandRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty()
            .MaximumLength(100)
            .WithMessage("Tên thương hiệu không được để trống và tối đa 100 ký tự.");
    }
}

public class ProductFilterQueryValidator : AppValidator<ProductFilterQuery>
{
    public ProductFilterQueryValidator()
    {
        RuleFor(query => query.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Số trang phải bắt đầu từ 1.");

        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, 50)
            .WithMessage("Số bản ghi mỗi trang phải từ 1 đến 50.");

        RuleFor(query => query.CategoryId)
            .GreaterThan(0)
            .When(query => query.CategoryId.HasValue)
            .WithMessage("CategoryId phải lớn hơn 0.");

        RuleFor(query => query.BrandId)
            .GreaterThan(0)
            .When(query => query.BrandId.HasValue)
            .WithMessage("BrandId phải lớn hơn 0.");

        RuleFor(query => query.MinPrice)
            .GreaterThanOrEqualTo(0)
            .When(query => query.MinPrice.HasValue)
            .WithMessage("Giá tối thiểu không được âm.");

        RuleFor(query => query.MaxPrice)
            .GreaterThanOrEqualTo(query => query.MinPrice ?? 0)
            .When(query => query.MaxPrice.HasValue)
            .WithMessage("Giá tối đa phải lớn hơn hoặc bằng giá tối thiểu.");

        RuleFor(query => query.SortBy)
            .Must(value => value is null
                || value is "price_asc" or "price_desc" or "newest")
            .WithMessage("SortBy chỉ nhận: price_asc, price_desc hoặc newest.");
    }
}
