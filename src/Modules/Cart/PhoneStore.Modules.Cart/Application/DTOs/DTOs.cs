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

/// <summary>
/// Thêm sản phẩm vào giỏ.
/// Giá, tên sản phẩm và SKU KHÔNG nhận từ client — server tự lấy từ Catalog
/// qua IProductPricingContract để tránh client tự đặt giá (F-01).
/// Validation được thực hiện bằng FluentValidation (AddToCartRequestValidator).
/// </summary>
public record AddToCartRequest(
    long ProductVariantId,
    int Quantity = 1);

public record UpdateCartItemRequest(
    int Quantity);
