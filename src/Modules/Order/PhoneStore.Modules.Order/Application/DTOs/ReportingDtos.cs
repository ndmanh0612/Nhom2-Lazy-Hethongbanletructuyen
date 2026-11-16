using PhoneStore.BuildingBlocks.Contracts.Inventory;

namespace PhoneStore.Modules.Order.Application.DTOs;

/// <summary>
/// Báo cáo doanh thu phục vụ FR-16 / N2LHTBLTT-32.
/// Chỉ đơn DELIVERED đã thu tiền (COD) mới được tính vào doanh thu.
/// </summary>
public record RevenueReportDto(
    long TotalRevenue,
    long TotalShippingFee,
    int DeliveredOrderCount,
    DateTimeOffset? FromDate,
    DateTimeOffset? ToDate);

/// <summary>
/// Báo cáo phân bố số lượng đơn hàng theo trạng thái (FR-16 / TC-18).
/// </summary>
public record OrderStatusReportDto(
    IReadOnlyDictionary<string, int> StatusCounts,
    int TotalOrders);

/// <summary>
/// Tổng quan dashboard quản trị FR-16 (doanh thu, trạng thái đơn, tồn kho thấp).
/// </summary>
public record DashboardReportDto(
    IReadOnlyDictionary<string, int> OrderCountsByStatus,
    int TotalOrders,
    RevenueReportDto Revenue,
    IReadOnlyList<LowStockItemDto> LowStockItems);
