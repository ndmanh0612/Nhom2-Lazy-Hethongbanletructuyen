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

    /// <summary>
    /// N2LHTBLTT-21 (FR-14): Đặt giữ tồn kho (khi đặt hàng).
    /// </summary>
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

    /// <summary>
    /// N2LHTBLTT-21 (FR-14): Hoàn trả tồn kho đã đặt giữ (khi hủy đơn).
    /// </summary>
    public void Restore(int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentException("Số lượng hoàn trả phải lớn hơn 0.", nameof(quantity));
        }

        StockQuantity += quantity;
        Touch();
    }

    /// <summary>
    /// N2LHTBLTT-21 (FR-14): Nhập kho — tăng tồn theo số lượng nhập.
    /// </summary>
    public int Import(int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentException("Số lượng nhập kho phải lớn hơn 0.", nameof(quantity));
        }

        StockQuantity += quantity;
        Touch();
        return quantity;
    }

    /// <summary>
    /// N2LHTBLTT-21 (FR-14): Điều chỉnh tồn kho thủ công.
    /// Delta dương = tăng, delta âm = giảm. Tồn kho không được âm.
    /// Trả về delta thực tế đã áp dụng.
    /// </summary>
    public int Adjust(int delta)
    {
        if (delta == 0)
        {
            throw new ArgumentException("Số lượng điều chỉnh không được bằng 0.", nameof(delta));
        }

        var newQuantity = StockQuantity + delta;
        if (newQuantity < 0)
        {
            throw new InvalidOperationException(
                $"Tồn kho không đủ để điều chỉnh. Hiện có {StockQuantity}, yêu cầu giảm {-delta}.");
        }

        StockQuantity = newQuantity;
        Touch();
        return delta;
    }
}
