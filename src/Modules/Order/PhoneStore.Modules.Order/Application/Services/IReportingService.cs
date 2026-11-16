using PhoneStore.BuildingBlocks.Common;
using PhoneStore.BuildingBlocks.Domain;
using PhoneStore.Modules.Order.Application.DTOs;

namespace PhoneStore.Modules.Order.Application.Services;

/// <summary>
/// FR-16 (N2LHTBLTT-32): Dịch vụ báo cáo thống kê doanh thu, trạng thái đơn và tồn kho thấp cho quản trị.
/// </summary>
public interface IReportingService
{
    Task<Result<DashboardReportDto>> GetDashboardReportAsync(
        DateTimeOffset? fromDate,
        DateTimeOffset? toDate,
        int lowStockThreshold = 10,
        CancellationToken cancellationToken = default);

    Task<Result<RevenueReportDto>> GetRevenueReportAsync(
        DateTimeOffset? fromDate,
        DateTimeOffset? toDate,
        CancellationToken cancellationToken = default);

    Task<Result<OrderStatusReportDto>> GetOrderStatusReportAsync(
        CancellationToken cancellationToken = default);
}
