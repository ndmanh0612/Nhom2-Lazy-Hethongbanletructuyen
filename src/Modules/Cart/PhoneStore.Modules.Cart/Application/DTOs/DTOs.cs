using System.ComponentModel.DataAnnotations;
using PhoneStore.Modules.Cart.Domain.Enums;

namespace PhoneStore.Modules.Cart.Application.DTOs;

public record CartItemDto(
    long Id,
    long ProductVariantId,
    string ProductName,
    string Sku,
    long UnitPrice,
    int Quantity,
    long Subtotal);

public record CartDto(
    long Id,
    long UserId,
    string Status,
    int TotalQuantity,
    long Subtotal,
    IReadOnlyList<CartItemDto> Items,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public record AddToCartRequest(
    [Required] long ProductVariantId,
    [Required, MaxLength(200)] string ProductName,
    [Required, MaxLength(64)] string Sku,
    [Range(0, long.MaxValue)] long UnitPrice,
    [Range(1, 10)] int Quantity = 1);

public record UpdateCartItemRequest(
    [Range(1, 10)] int Quantity);
