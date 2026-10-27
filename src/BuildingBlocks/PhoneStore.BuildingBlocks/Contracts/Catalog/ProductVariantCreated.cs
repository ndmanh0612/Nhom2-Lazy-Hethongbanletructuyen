using MediatR;
using PhoneStore.BuildingBlocks.Domain;

namespace PhoneStore.BuildingBlocks.Contracts.Catalog;

/// <summary>
/// N2LHTBLTT-23: Event phát ra khi Catalog tạo một phiên bản sản phẩm mới.
///
/// Module Inventory nhận event này để khởi tạo bản ghi tồn kho tương ứng trong
/// schema <c>inventory</c>. Nếu thiếu bước này, sản phẩm mới tạo từ API quản trị
/// sẽ không có tồn kho → checkout luôn bị từ chối với mã <c>INSUFFICIENT_STOCK</c>.
///
/// Đây là kênh giao tiếp duy nhất giữa Catalog và Inventory (không tham chiếu
/// trực tiếp DbContext của nhau), đúng nguyên tắc kiến trúc Modular Monolith.
/// </summary>
public record ProductVariantCreated(
    long ProductVariantId,
    long ProductId,
    string Sku,
    int InitialStockQuantity) : IDomainEvent, INotification
{
    public DateTimeOffset OccurredOn { get; init; } = DateTimeOffset.UtcNow;
}
