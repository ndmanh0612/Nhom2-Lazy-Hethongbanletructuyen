namespace PhoneStore.Modules.Inventory.Application.DTOs;

/// <summary>
/// N2LHTBLTT-21 (FR-14): Yêu cầu nhập kho cho một phiên bản sản phẩm.
/// </summary>
public record ImportStockRequest(
    long ProductVariantId,
    int Quantity,
    string? Note);

/// <summary>
/// N2LHTBLTT-21 (FR-14): Yêu cầu điều chỉnh tồn kho thủ công.
/// Delta dương = tăng, delta âm = giảm.
/// </summary>
public record AdjustStockRequest(
    long ProductVariantId,
    int Delta,
    string? Note);

/// <summary>
/// N2LHTBLTT-21 (FR-14): Nhập kho hàng loạt — một lần tạo nhiều bản ghi.
/// </summary>
public record ImportStockBatchRequest(
    IReadOnlyList<ImportStockRequest> Items,
    string? Note);

/// <summary>N2LHTBLTT-21 (FR-14): Tồn kho hiện tại của một phiên bản sản phẩm.</summary>
public record StockDto(
    long ProductVariantId,
    int StockQuantity,
    DateTimeOffset UpdatedAt);

/// <summary>N2LHTBLTT-21 (FR-14): Một dòng nhật ký tồn kho.</summary>
public record StockMovementDto(
    long Id,
    long ProductVariantId,
    int Quantity,
    int QuantityBefore,
    int QuantityAfter,
    string Type,
    long? OrderId,
    string? Note,
    long? PerformedBy,
    DateTimeOffset ChangedAt);

/// <summary>N2LHTBLTT-21 (FR-14): Bộ lọc nhật ký tồn kho.</summary>
public record StockMovementFilterQuery(
    long? ProductVariantId,
    string? Type,
    long? OrderId,
    DateTimeOffset? FromDate,
    DateTimeOffset? ToDate,
    int Page = 1,
    int PageSize = 20);
