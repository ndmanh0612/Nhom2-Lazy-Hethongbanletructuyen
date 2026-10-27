namespace PhoneStore.BuildingBlocks.Contracts.Inventory;

/// <summary>
/// Một dòng yêu cầu thay đổi tồn kho (đặt giữ hoặc hoàn trả).
/// </summary>
public record StockChangeItem(long ProductVariantId, int Quantity);

/// <summary>
/// Contract để module khác (Order) điều chỉnh tồn kho mà không reference InventoryDbContext.
/// </summary>
public interface IInventoryContract
{
    /// <summary>
    /// Đặt giữ tồn kho cho danh sách sản phẩm. Trả về false nếu tồn kho không đủ.
    /// </summary>
    Task<bool> TryReserveStockAsync(
        IReadOnlyCollection<StockChangeItem> items,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Hoàn trả tồn kho đã đặt giữ (dùng khi hủy đơn).
    /// </summary>
    Task RestoreStockAsync(
        IReadOnlyCollection<StockChangeItem> items,
        CancellationToken cancellationToken = default);
}
