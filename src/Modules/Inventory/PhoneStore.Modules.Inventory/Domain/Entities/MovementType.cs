namespace PhoneStore.Modules.Inventory.Domain.Entities;

/// <summary>
/// Loại biến động kho hàng
/// </summary>
public enum MovementType
{
    /// <summary>Nhập hàng từ nhà cung cấp</summary>
    INBOUND,

    /// <summary>Xuất hàng (bán cho khách)</summary>
    OUTBOUND,

    /// <summary>Điều chỉnh tăng (kiểm kê thừa)</summary>
    ADJUSTMENT_IN,

    /// <summary>Điều chỉnh giảm (kiểm kê thiếu, hàng hỏng)</summary>
    ADJUSTMENT_OUT,

    /// <summary>Khách trả hàng</summary>
    RETURN
}
