using FluentValidation;
using PhoneStore.BuildingBlocks.Validation;
using PhoneStore.Modules.Order.Application.DTOs;
using PhoneStore.Modules.Order.Domain.Enums;

namespace PhoneStore.Modules.Order.Application.Validators;

/// <summary>
/// FR-07 / FR-08 / FR-09: Validation cho các request của module Order.
/// </summary>
public class CheckoutRequestValidator : AppValidator<CheckoutRequest>
{
    public CheckoutRequestValidator()
    {
        RuleFor(request => request.ReceiverName)
            .NotEmpty()
            .MaximumLength(100)
            .WithMessage("Tên người nhận không được để trống và tối đa 100 ký tự.");

        RuleFor(request => request.ReceiverPhone)
            .NotEmpty()
            .MaximumLength(20)
            .Matches(@"^(0|\+84)\d{8,11}$")
            .WithMessage("Số điện thoại không hợp lệ. Ví dụ hợp lệ: 0912345678.");

        RuleFor(request => request.ShippingAddress)
            .NotEmpty()
            .MaximumLength(500)
            .WithMessage("Địa chỉ giao hàng không được để trống và tối đa 500 ký tự.");

        RuleFor(request => request.Note)
            .MaximumLength(500)
            .When(request => request.Note is not null)
            .WithMessage("Ghi chú tối đa 500 ký tự.");
    }
}

public class CancelOrderRequestValidator : AppValidator<CancelOrderRequest>
{
    public CancelOrderRequestValidator()
    {
        RuleFor(request => request.Reason)
            .NotEmpty()
            .MaximumLength(500)
            .WithMessage("Lý do hủy đơn không được để trống và tối đa 500 ký tự.");
    }
}

/// <summary>
/// FR-08: Validation cho tham số lọc và phân trang danh sách đơn hàng.
/// </summary>
public class OrderFilterQueryValidator : AppValidator<OrderFilterQuery>
{
    public OrderFilterQueryValidator()
    {
        RuleFor(query => query.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Số trang phải bắt đầu từ 1.");

        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("Số bản ghi mỗi trang phải từ 1 đến 100.");

        RuleFor(query => query.FromDate)
            .LessThanOrEqualTo(query => query.ToDate)
            .When(query => query.FromDate.HasValue && query.ToDate.HasValue)
            .WithMessage("Ngày bắt đầu phải nhỏ hơn hoặc bằng ngày kết thúc.");

        RuleFor(query => query.Status)
            .IsInEnum()
            .When(query => query.Status.HasValue)
            .WithMessage("Trạng thái đơn hàng không hợp lệ.");
    }
}

// ---------------------------------------------------------------------------
// FR-10 (N2LHTBLTT-27) / FR-11 (N2LHTBLTT-28): Validation cho nghiệp vụ vận chuyển.
// ---------------------------------------------------------------------------

public class ConfirmOrderRequestValidator : AppValidator<ConfirmOrderRequest>
{
    public ConfirmOrderRequestValidator()
    {
        RuleFor(request => request.Note)
            .MaximumLength(500)
            .When(request => request.Note is not null)
            .WithMessage("Ghi chú tối đa 500 ký tự.");
    }
}

public class ShipOrderRequestValidator : AppValidator<ShipOrderRequest>
{
    public ShipOrderRequestValidator()
    {
        RuleFor(request => request.Carrier)
            .MaximumLength(100)
            .When(request => request.Carrier is not null)
            .WithMessage("Tên đơn vị vận chuyển tối đa 100 ký tự.");

        RuleFor(request => request.TrackingNumber)
            .MaximumLength(64)
            .When(request => request.TrackingNumber is not null)
            .WithMessage("Mã vận đơn tối đa 64 ký tự.");

        RuleFor(request => request.Note)
            .MaximumLength(500)
            .When(request => request.Note is not null)
            .WithMessage("Ghi chú tối đa 500 ký tự.");
    }
}

public class DeliverOrderRequestValidator : AppValidator<DeliverOrderRequest>
{
    public DeliverOrderRequestValidator()
    {
        RuleFor(request => request.CollectedAmount)
            .GreaterThan(0)
            .WithMessage("Số tiền COD thu được phải lớn hơn 0.");

        RuleFor(request => request.Note)
            .MaximumLength(500)
            .When(request => request.Note is not null)
            .WithMessage("Ghi chú tối đa 500 ký tự.");
    }
}

public class DeliveryFailedRequestValidator : AppValidator<DeliveryFailedRequest>
{
    public DeliveryFailedRequestValidator()
    {
        RuleFor(request => request.Reason)
            .NotEmpty()
            .MaximumLength(500)
            .WithMessage("Lý do giao thất bại không được để trống và tối đa 500 ký tự.");
    }
}
