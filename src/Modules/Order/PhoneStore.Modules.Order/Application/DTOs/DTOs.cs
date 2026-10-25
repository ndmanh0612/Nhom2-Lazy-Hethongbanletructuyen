using System.ComponentModel.DataAnnotations;
using PhoneStore.Modules.Order.Domain.Enums;
using OrderEntity = PhoneStore.Modules.Order.Domain.Entities.Order;

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

public record CheckoutRequest(
    [Required, MaxLength(100)] string ReceiverName,
    [Required, Phone, MaxLength(20)] string ReceiverPhone,
    [Required, MaxLength(500)] string ShippingAddress,
    [MaxLength(500)] string? Note);

public record CancelOrderRequest(
    [Required, MaxLength(500)] string Reason);

public record OrderFilterQuery(
    OrderStatus? Status,
    DateTimeOffset? FromDate,
    DateTimeOffset? ToDate,
    int Page = 1,
    int PageSize = 20);
