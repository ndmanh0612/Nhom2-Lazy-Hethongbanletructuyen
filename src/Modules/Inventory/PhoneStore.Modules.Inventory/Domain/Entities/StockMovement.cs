using PhoneStore.BuildingBlocks.Domain;

namespace PhoneStore.Modules.Inventory.Domain.Entities;

/// <summary>
/// N2LHTBLTT-21 (FR-14): Loại biến động tồn kho.
/// </summary>
public enum StockMovementType
{
    /// <summary>Nhập kho — tăng tồn.</summary>
    IMPORT,

    /// <summary>Điều chỉnh thủ công — có thể tăng hoặc giảm.</summary>
    ADJUSTMENT,

    /// <summary>Đặt giữ khi đặt hàng — giảm tồn.</summary>
    RESERVATION,

    /// <summary>Hoàn trả tồn kho khi hủy đơn — tăng tồn.</summary>
    RESTORE
}

/// <summary>
/// N2LHTBLTT-21 (FR-14): Nhật ký tồn kho.
///
/// Mỗi lần tồn kho thay đổi đều tạo một bản ghi movement, ghi lại số lượng
/// trước / sau để tra cứu và đối soát.
/// </summary>
public class StockMovement : BaseEntity<long>
{
    public long ProductVariantId { get; private set; }

    /// <summary>Số lượng thay đổi (dương = tăng, âm = giảm).</summary>
    public int Quantity { get; private set; }

    /// <summary>Số lượng tồn trước khi thay đổi.</summary>
    public int QuantityBefore { get; private set; }

    /// <summary>Số lượng tồn sau khi thay đổi.</summary>
    public int QuantityAfter { get; private set; }

    public StockMovementType Type { get; private set; }

    /// <summary>Id đơn hàng liên quan (nếu có) — dùng để truy vết.</summary>
    public long? OrderId { get; private set; }

    public string? Note { get; private set; }

    /// <summary>Id người thực hiện thao tác (nhân viên / hệ thống).</summary>
    public long? PerformedBy { get; private set; }

    protected StockMovement() { }

    public static StockMovement Create(
        long productVariantId,
        int quantity,
        int quantityBefore,
        int quantityAfter,
        StockMovementType type,
        long? orderId = null,
        string? note = null,
        long? performedBy = null)
    {
        if (productVariantId <= 0)
        {
            throw new ArgumentException("Id phiên bản sản phẩm không hợp lệ.", nameof(productVariantId));
        }

        if (quantity == 0)
        {
            throw new ArgumentException("Số lượng thay đổi không được bằng 0.", nameof(quantity));
        }

        return new StockMovement
        {
            ProductVariantId = productVariantId,
            Quantity = quantity,
            QuantityBefore = quantityBefore,
            QuantityAfter = quantityAfter,
            Type = type,
            OrderId = orderId,
            Note = note?.Trim(),
            PerformedBy = performedBy,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }
}
