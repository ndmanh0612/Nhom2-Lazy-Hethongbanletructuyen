using PhoneStore.BuildingBlocks.Domain;

namespace PhoneStore.Modules.Order.Domain.Entities;

public class OrderItem : BaseEntity<long>
{
    public long OrderId { get; private set; }
    public Order Order { get; private set; } = null!;
    public long ProductVariantId { get; private set; }
    public string ProductName { get; private set; } = string.Empty;
    public string Sku { get; private set; } = string.Empty;
    public long UnitPrice { get; private set; }
    public int Quantity { get; private set; }

    public long Subtotal => UnitPrice * Quantity;

    protected OrderItem() { }

    public static OrderItem Create(Order order, long productVariantId, string productName, string sku, long unitPrice, int quantity)
    {
        if (productVariantId <= 0)
        {
            throw new ArgumentException("Id phiên bản sản phẩm không hợp lệ.", nameof(productVariantId));
        }

        if (unitPrice < 0)
        {
            throw new ArgumentException("Đơn giá không được âm.", nameof(unitPrice));
        }

        if (quantity <= 0)
        {
            throw new ArgumentException("Số lượng phải lớn hơn 0.", nameof(quantity));
        }

        return new OrderItem
        {
            Order = order,
            ProductVariantId = productVariantId,
            ProductName = productName.Trim(),
            Sku = sku.Trim(),
            UnitPrice = unitPrice,
            Quantity = quantity,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }
}
