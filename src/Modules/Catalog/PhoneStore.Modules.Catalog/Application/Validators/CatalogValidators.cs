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

// ---------------------------------------------------------------------------
// FR-12 (N2LHTBLTT-10) / FR-13 (N2LHTBLTT-18): Validation cho quản trị Catalog.
// ---------------------------------------------------------------------------

public class UpdateCategoryRequestValidator : AppValidator<UpdateCategoryRequest>
{
    public UpdateCategoryRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty()
            .MaximumLength(100)
            .WithMessage("Tên danh mục không được để trống và tối đa 100 ký tự.");
    }
}

public class UpdateBrandRequestValidator : AppValidator<UpdateBrandRequest>
{
    public UpdateBrandRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty()
            .MaximumLength(100)
            .WithMessage("Tên thương hiệu không được để trống và tối đa 100 ký tự.");
    }
}

public class CreateProductRequestValidator : AppValidator<CreateProductRequest>
{
    public CreateProductRequestValidator()
    {
        RuleFor(request => request.CategoryId)
            .GreaterThan(0)
            .WithMessage("CategoryId phải lớn hơn 0.");

        RuleFor(request => request.BrandId)
            .GreaterThan(0)
            .WithMessage("BrandId phải lớn hơn 0.");

        RuleFor(request => request.Name)
            .NotEmpty()
            .MaximumLength(200)
            .WithMessage("Tên sản phẩm không được để trống và tối đa 200 ký tự.");

        RuleFor(request => request.Description)
            .MaximumLength(4000)
            .WithMessage("Mô tả tối đa 4000 ký tự.");

        RuleFor(request => request.Variants)
            .Must(variants => variants is null || variants.Count <= 20)
            .WithMessage("Một sản phẩm không được vượt quá 20 phiên bản.");

        RuleForEach(request => request.Variants).SetValidator(new CreateVariantRequestValidator());
    }
}

public class UpdateProductRequestValidator : AppValidator<UpdateProductRequest>
{
    public UpdateProductRequestValidator()
    {
        RuleFor(request => request.CategoryId)
            .GreaterThan(0)
            .WithMessage("CategoryId phải lớn hơn 0.");

        RuleFor(request => request.BrandId)
            .GreaterThan(0)
            .WithMessage("BrandId phải lớn hơn 0.");

        RuleFor(request => request.Name)
            .NotEmpty()
            .MaximumLength(200)
            .WithMessage("Tên sản phẩm không được để trống và tối đa 200 ký tự.");

        RuleFor(request => request.Description)
            .MaximumLength(4000)
            .WithMessage("Mô tả tối đa 4000 ký tự.");
    }
}

public class CreateVariantRequestValidator : AppValidator<CreateVariantRequest>
{
    public CreateVariantRequestValidator()
    {
        RuleFor(request => request.Sku)
            .NotEmpty()
            .MaximumLength(64)
            .WithMessage("SKU không được để trống và tối đa 64 ký tự.");

        RuleFor(request => request.Color)
            .MaximumLength(50)
            .WithMessage("Màu sắc tối đa 50 ký tự.");

        RuleFor(request => request.Capacity)
            .MaximumLength(50)
            .WithMessage("Dung lượng tối đa 50 ký tự.");

        RuleFor(request => request.Price)
            .GreaterThan(0)
            .WithMessage("Giá phải lớn hơn 0.");

        RuleFor(request => request.StockQuantity)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Số lượng tồn kho không được âm.");
    }
}

public class UpdateVariantRequestValidator : AppValidator<UpdateVariantRequest>
{
    public UpdateVariantRequestValidator()
    {
        RuleFor(request => request.Sku)
            .NotEmpty()
            .MaximumLength(64)
            .WithMessage("SKU không được để trống và tối đa 64 ký tự.");

        RuleFor(request => request.Color)
            .MaximumLength(50)
            .WithMessage("Màu sắc tối đa 50 ký tự.");

        RuleFor(request => request.Capacity)
            .MaximumLength(50)
            .WithMessage("Dung lượng tối đa 50 ký tự.");

        RuleFor(request => request.Price)
            .GreaterThan(0)
            .WithMessage("Giá phải lớn hơn 0.");
    }
}

public class CreateProductImageRequestValidator : AppValidator<CreateProductImageRequest>
{
    public CreateProductImageRequestValidator()
    {
        RuleFor(request => request.ImageUrl)
            .NotEmpty()
            .MaximumLength(500)
            .WithMessage("URL ảnh không được để trống và tối đa 500 ký tự.");

        RuleFor(request => request.SortOrder)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Thứ tự ảnh không được âm.");
    }
}

public class VariantFilterQueryValidator : AppValidator<VariantFilterQuery>
{
    public VariantFilterQueryValidator()
    {
        RuleFor(query => query.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Số trang phải bắt đầu từ 1.");

        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("Số bản ghi mỗi trang phải từ 1 đến 100.");

        RuleFor(query => query.ProductId)
            .GreaterThan(0)
            .When(query => query.ProductId.HasValue)
            .WithMessage("ProductId phải lớn hơn 0.");
    }
}
