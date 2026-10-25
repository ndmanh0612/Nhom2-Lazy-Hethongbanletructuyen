using FluentValidation;
using PhoneStore.BuildingBlocks.Validation;
using PhoneStore.Modules.Cart.Application.DTOs;

namespace PhoneStore.Modules.Cart.Application.Validators;

/// <summary>
/// FR-06: Validation cho các request thao tác trên giỏ hàng.
/// </summary>
public class AddToCartRequestValidator : AppValidator<AddToCartRequest>
{
    public AddToCartRequestValidator()
    {
        RuleFor(request => request.ProductVariantId)
            .GreaterThan(0)
            .WithMessage("ProductVariantId phải lớn hơn 0.");

        RuleFor(request => request.Quantity)
            .InclusiveBetween(1, 10)
            .WithMessage("Số lượng phải từ 1 đến 10.");
    }
}

public class UpdateCartItemRequestValidator : AppValidator<UpdateCartItemRequest>
{
    public UpdateCartItemRequestValidator()
    {
        RuleFor(request => request.Quantity)
            .InclusiveBetween(1, 10)
            .WithMessage("Số lượng phải từ 1 đến 10.");
    }
}
