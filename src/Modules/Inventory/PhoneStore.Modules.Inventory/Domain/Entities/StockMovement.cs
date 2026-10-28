using PhoneStore.BuildingBlocks.Domain;

namespace PhoneStore.Modules.Inventory.Domain.Entities;

/// <summary>
/// Lịch sử biến động kho hàng.
/// Mỗi lần nhập/xuất/điều chỉnh kho sẽ tạo 1 bản ghi StockMovement.
/// </summary>
public class StockMovement : BaseEntity<long>
{
    /// <summary>ID bản ghi tồn kho liên quan</summary>
    public long StockId { get; private set; }

    /// <summary>Loại biến động (INBOUND, OUTBOUND, ADJUSTMENT_IN, ADJUSTMENT_OUT, RETURN)</summary>
    public MovementType Type { get; private set; }

    /// <summary>Số lượng thay đổi (dương = nhập, âm = xuất)</summary>
    public int Quantity { get; private set; }

    /// <summary>Số lượng tồn kho sau khi thay đổi (snapshot)</summary>
    public int QuantityAfter { get; private set; }

    /// <summary>Lý do / ghi chú biến động</summary>
    public string? Reason { get; private set; }

    protected StockMovement() { }

    public static StockMovement Create(long stockId, MovementType type, int quantity, int quantityAfter, string? reason = null)
    {
        return new StockMovement
        {
            StockId = stockId,
            Type = type,
            Quantity = quantity,
            QuantityAfter = quantityAfter,
            Reason = reason?.Trim(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }
}
