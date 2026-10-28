using PhoneStore.BuildingBlocks.Domain;

namespace PhoneStore.Modules.Inventory.Domain.Entities;

/// <summary>
/// Aggregate Root: Tồn kho theo từng ProductVariant (SKU).
/// Mỗi bản ghi Stock đại diện cho số lượng tồn kho hiện tại của 1 phiên bản sản phẩm.
/// </summary>
public class Stock : AggregateRoot<long>
{
    /// <summary>ID phiên bản sản phẩm (tham chiếu sang Catalog module)</summary>
    public long ProductVariantId { get; private set; }

    /// <summary>Mã SKU (lưu denormalize để truy vấn nhanh, không cần join sang Catalog)</summary>
    public string Sku { get; private set; } = string.Empty;

    /// <summary>Số lượng tồn kho hiện tại (available)</summary>
    public int QuantityOnHand { get; private set; }

    /// <summary>Số lượng đã đặt hàng nhưng chưa xuất (reserved)</summary>
    public int QuantityReserved { get; private set; }

    // Navigation: danh sách lịch sử biến động
    private readonly List<StockMovement> _movements = [];
    public IReadOnlyCollection<StockMovement> Movements => _movements.AsReadOnly();

    protected Stock() { }

    /// <summary>
    /// Tạo bản ghi tồn kho mới cho 1 ProductVariant
    /// </summary>
    public static Stock Create(long productVariantId, string sku, int initialQuantity = 0)
    {
        if (initialQuantity < 0)
            throw new ArgumentException("Số lượng tồn kho ban đầu không được âm.", nameof(initialQuantity));

        return new Stock
        {
            ProductVariantId = productVariantId,
            Sku = sku.Trim().ToUpperInvariant(),
            QuantityOnHand = initialQuantity,
            QuantityReserved = 0,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Nhập hàng vào kho
    /// </summary>
    public void AddStock(int quantity, string? reason = null)
    {
        if (quantity <= 0)
            throw new ArgumentException("Số lượng nhập phải lớn hơn 0.", nameof(quantity));

        QuantityOnHand += quantity;
        _movements.Add(StockMovement.Create(Id, MovementType.INBOUND, quantity, QuantityOnHand, reason));
        Touch();
    }

    /// <summary>
    /// Xuất hàng khỏi kho (khi đơn hàng được xác nhận giao)
    /// </summary>
    public void RemoveStock(int quantity, string? reason = null)
    {
        if (quantity <= 0)
            throw new ArgumentException("Số lượng xuất phải lớn hơn 0.", nameof(quantity));

        if (QuantityOnHand < quantity)
            throw new InvalidOperationException(
                $"Tồn kho không đủ. Hiện có {QuantityOnHand}, yêu cầu xuất {quantity}.");

        QuantityOnHand -= quantity;
        _movements.Add(StockMovement.Create(Id, MovementType.OUTBOUND, -quantity, QuantityOnHand, reason));
        Touch();
    }

    /// <summary>
    /// Đặt trước hàng (khi khách đặt đơn nhưng chưa giao)
    /// </summary>
    public void Reserve(int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentException("Số lượng đặt trước phải lớn hơn 0.", nameof(quantity));

        var available = QuantityOnHand - QuantityReserved;
        if (available < quantity)
            throw new InvalidOperationException(
                $"Không đủ hàng để đặt trước. Có sẵn {available}, yêu cầu {quantity}.");

        QuantityReserved += quantity;
        Touch();
    }

    /// <summary>
    /// Hủy đặt trước (khi khách hủy đơn)
    /// </summary>
    public void ReleaseReservation(int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentException("Số lượng hủy đặt trước phải lớn hơn 0.", nameof(quantity));

        if (QuantityReserved < quantity)
            throw new InvalidOperationException(
                $"Số lượng đặt trước hiện tại ({QuantityReserved}) nhỏ hơn số cần hủy ({quantity}).");

        QuantityReserved -= quantity;
        Touch();
    }

    /// <summary>
    /// Điều chỉnh tồn kho (kiểm kê, hàng hỏng, ...)
    /// </summary>
    public void Adjust(int delta, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Phải có lý do khi điều chỉnh tồn kho.", nameof(reason));

        var newQuantity = QuantityOnHand + delta;
        if (newQuantity < 0)
            throw new InvalidOperationException(
                $"Sau điều chỉnh tồn kho sẽ âm ({newQuantity}). Hiện có {QuantityOnHand}, delta = {delta}.");

        var movementType = delta >= 0 ? MovementType.ADJUSTMENT_IN : MovementType.ADJUSTMENT_OUT;
        QuantityOnHand = newQuantity;
        _movements.Add(StockMovement.Create(Id, movementType, delta, QuantityOnHand, reason));
        Touch();
    }

    /// <summary>
    /// Số lượng có thể bán (chưa bị reserve)
    /// </summary>
    public int AvailableQuantity => QuantityOnHand - QuantityReserved;
}
