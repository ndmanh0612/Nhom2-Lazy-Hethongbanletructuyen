namespace PhoneStore.BuildingBlocks.Contracts.Cart;

/// <summary>
/// Snapshot của một dòng sản phẩm trong giỏ hàng, dùng bởi module Order khi checkout.
/// </summary>
public record CartSnapshotItem(
    long CartItemId,
    long ProductVariantId,
    string ProductName,
    string Sku,
    long UnitPrice,
    int Quantity);

/// <summary>
/// Snapshot của giỏ hàng đang hoạt động của khách hàng.
/// </summary>
public record CartSnapshot(
    long CartId,
    long UserId,
    IReadOnlyList<CartSnapshotItem> Items)
{
    public bool IsEmpty => Items.Count == 0;
    public int TotalQuantity => Items.Sum(item => item.Quantity);
}

/// <summary>
/// Contract để module Order lấy giỏ hàng mà không reference trực tiếp CartDbContext.
/// Cart module là nơi implement contract này.
/// </summary>
public interface ICartContract
{
    Task<CartSnapshot?> GetActiveCartAsync(long userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Đánh dấu giỏ hàng đã chuyển thành đơn hàng sau khi checkout thành công.
    /// </summary>
    Task MarkAsConvertedAsync(long cartId, CancellationToken cancellationToken = default);
}
