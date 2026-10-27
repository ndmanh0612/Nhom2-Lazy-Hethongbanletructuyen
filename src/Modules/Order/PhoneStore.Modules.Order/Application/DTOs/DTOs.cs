using PhoneStore.Modules.Order.Domain.Enums;

namespace PhoneStore.Modules.Order.Application.DTOs;

public record OrderItemDto(
    long Id,
    long ProductVariantId,
    string ProductName,
    string Sku,
    long UnitPrice,
    int Quantity,
    long Subtotal);

public record OrderStatusHistoryDto(
    long Id,
    string? FromStatus,
    string ToStatus,
    string? Note,
    DateTimeOffset ChangedAt);

public record OrderDto(
    long Id,
    string OrderNumber,
    long CustomerId,
    string Status,
    string PaymentMethod,
    long Subtotal,
    long ShippingFee,
    long TotalAmount,
    string ReceiverName,
    string ReceiverPhone,
    string ShippingAddress,
    string? Note,
    string? CancelReason,
    DateTimeOffset? CancelledAt,
    DateTimeOffset? DeliveredAt,
    IReadOnlyList<OrderItemDto> Items,
    DateTimeOffset CreatedAt);

public record OrderDetailDto(
    long Id,
    string OrderNumber,
    long CustomerId,
    string Status,
    string PaymentMethod,
    long Subtotal,
    long ShippingFee,
    long TotalAmount,
    string ReceiverName,
    string ReceiverPhone,
    string ShippingAddress,
    string? Note,
    string? CancelReason,
    DateTimeOffset? CancelledAt,
    DateTimeOffset? DeliveredAt,
    IReadOnlyList<OrderItemDto> Items,
    IReadOnlyList<OrderStatusHistoryDto> StatusHistories,
    DateTimeOffset CreatedAt);

/// <summary>
/// FR-07: Thông tin người nhận cho đơn hàng COD.
/// Validation được thực hiện bằng FluentValidation (CheckoutRequestValidator).
/// </summary>
public record CheckoutRequest(
    string ReceiverName,
    string ReceiverPhone,
    string ShippingAddress,
    string? Note);

/// <summary>
/// FR-09: Lý do hủy đơn. Validation bằng CancelOrderRequestValidator.
/// </summary>
public record CancelOrderRequest(
    string Reason);

/// <summary>
/// FR-08: Bộ lọc và tham số phân trang danh sách đơn hàng.
/// Validation bằng OrderFilterQueryValidator.
/// </summary>
public record OrderFilterQuery(
    OrderStatus? Status,
    DateTimeOffset? FromDate,
    DateTimeOffset? ToDate,
    int Page = 1,
    int PageSize = 20);

/// <summary>
/// FR-10 (N2LHTBLTT-27): Xác nhận đơn và chuyển sang trạng thái giao hàng.
/// </summary>
public record ConfirmOrderRequest(string? Note);

/// <summary>
/// FR-10 (N2LHTBLTT-27): Bàn giao cho đơn vị vận chuyển — trạng thái SHIPPING.
/// </summary>
public record ShipOrderRequest(string? Carrier, string? TrackingNumber, string? Note);

/// <summary>
/// FR-11 (N2LHTBLTT-28): Giao hàng thành công — thu tiền COD.
/// </summary>
public record DeliverOrderRequest(long CollectedAmount, string? Note);

/// <summary>
/// FR-11 (N2LHTBLTT-28): Giao hàng thất bại — giao lại hoặc huỷ.
/// </summary>
public record DeliveryFailedRequest(string Reason, bool CancelOrder);

/// <summary>
/// FR-11 (N2LHTBLTT-28): Đơn hàng đã thu COD — thống kê tiền mặt đã thu.
/// </summary>
public record CodCollectionDto(
    long OrderId,
    string OrderNumber,
    long TotalAmount,
    long CollectedAmount,
    DateTimeOffset CollectedAt);
