using PhoneStore.BuildingBlocks.Domain;

namespace PhoneStore.Modules.Inventory.Domain.Entities;

/// <summary>
/// Tồn kho của một phiên bản sản phẩm, thuộc sở hữu của module Inventory.
/// </summary>
public class ProductStock : BaseEntity<long>
{
    public long ProductVariantId { get; private set; }
    public int StockQuantity { get; private set; }

    protected ProductStock() { }

    public static ProductStock Create(long productVariantId, int stockQuantity = 0)
    {
        if (productVariantId <= 0)
        {
            throw new ArgumentException("Id phiên bản sản phẩm không hợp lệ.", nameof(productVariantId));
        }

        if (stockQuantity < 0)
        {
            throw new ArgumentException("Số lượng tồn kho không được âm.", nameof(stockQuantity));
        }

        return new ProductStock
        {
            ProductVariantId = productVariantId,
            StockQuantity = stockQuantity,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    public void Reserve(int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentException("Số lượng đặt giữ phải lớn hơn 0.", nameof(quantity));
        }

        if (StockQuantity < quantity)
        {
            throw new InvalidOperationException($"Tồn kho không đủ. Hiện có {StockQuantity}, yêu cầu giảm {quantity}.");
        }

        StockQuantity -= quantity;
        Touch();
    }

    public void Restore(int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentException("Số lượng hoàn trả phải lớn hơn 0.", nameof(quantity));
        }

        StockQuantity += quantity;
        Touch();
    }
}
