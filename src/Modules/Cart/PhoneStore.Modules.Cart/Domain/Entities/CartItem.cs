using PhoneStore.BuildingBlocks.Domain;
using PhoneStore.BuildingBlocks.Exceptions;

namespace PhoneStore.Modules.Cart.Domain.Entities;

public class CartItem : BaseEntity<long>
{
    public const int MaxQuantityPerItem = 10;

    public long CartId { get; private set; }
    public long ProductVariantId { get; private set; }
    public string ProductName { get; private set; } = string.Empty;
    public string Sku { get; private set; } = string.Empty;
    public long UnitPrice { get; private set; }
    public int Quantity { get; private set; }

    public long Subtotal => UnitPrice * Quantity;

    protected CartItem() { }

    public static CartItem Create(long cartId, long productVariantId, string productName, string sku, long unitPrice, int quantity)
    {
        if (productVariantId <= 0)
        {
            throw new ArgumentException("Id phiên bản sản phẩm không hợp lệ.", nameof(productVariantId));
        }

        if (unitPrice < 0)
        {
            throw new ArgumentException("Đơn giá không được âm.", nameof(unitPrice));
        }

        var cartItem = new CartItem
        {
            CartId = cartId,
            ProductVariantId = productVariantId,
            ProductName = productName.Trim(),
            Sku = sku.Trim(),
            UnitPrice = unitPrice,
            Quantity = 0,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        cartItem.SetQuantity(quantity);
        return cartItem;
    }

    public void SetQuantity(int quantity)
    {
        if (quantity <= 0)
        {
            throw new ValidationException(
                "quantity",
                "Số lượng phải lớn hơn 0.");
        }

        if (quantity > MaxQuantityPerItem)
        {
            throw new ValidationException(
                "quantity",
                $"Mỗi sản phẩm chỉ được thêm tối đa {MaxQuantityPerItem} đơn vị.");
        }

        Quantity = quantity;
        Touch();
    }

    public void IncreaseQuantity(int quantity)
    {
        if (quantity <= 0)
        {
            throw new ValidationException(
                "quantity",
                "Số lượng tăng thêm phải lớn hơn 0.");
        }

        SetQuantity(Quantity + quantity);
    }
}
