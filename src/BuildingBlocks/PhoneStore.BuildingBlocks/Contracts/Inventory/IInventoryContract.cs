namespace PhoneStore.BuildingBlocks.Contracts.Inventory;

/// <summary>
/// Một dòng yêu cầu thay đổi tồn kho (đặt giữ hoặc hoàn trả).
/// </summary>
public record StockChangeItem(long ProductVariantId, int Quantity);

/// <summary>
/// Thông tin tồn kho thấp của một phiên bản sản phẩm phục vụ báo cáo FR-16 / N2LHTBLTT-32.
/// </summary>
public record LowStockItemDto(long ProductVariantId, int StockQuantity);

/// <summary>
/// Contract để module khác (Order, Reports) làm việc với tồn kho mà không reference InventoryDbContext.
/// </summary>
public interface IInventoryContract
{
    /// <summary>
    /// Đặt giữ tồn kho cho danh sách sản phẩm. Trả về false nếu tồn kho không đủ.
    /// </summary>
    Task<bool> TryReserveStockAsync(
        IReadOnlyCollection<StockChangeItem> items,
        long? orderId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Hoàn trả tồn kho đã đặt giữ (dùng khi hủy đơn).
    /// </summary>
    Task RestoreStockAsync(
        IReadOnlyCollection<StockChangeItem> items,
        long? orderId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// FR-16: Lấy danh sách phiên bản có tồn kho thấp hơn hoặc bằng ngưỡng quy định.
    /// </summary>
    Task<IReadOnlyList<LowStockItemDto>> GetLowStockAsync(
        int threshold = 10,
        CancellationToken cancellationToken = default);
}
