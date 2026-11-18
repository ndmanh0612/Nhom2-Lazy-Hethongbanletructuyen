using Microsoft.EntityFrameworkCore;
using PhoneStore.BuildingBlocks.Common;
using PhoneStore.BuildingBlocks.Contracts.Inventory;
using PhoneStore.BuildingBlocks.Domain;
using PhoneStore.Modules.Order.Application.DTOs;
using PhoneStore.Modules.Order.Domain.Enums;
using PhoneStore.Modules.Order.Infrastructure.Persistence;

namespace PhoneStore.Modules.Order.Application.Services;

/// <summary>
/// FR-16 (N2LHTBLTT-32): Dịch vụ báo cáo thống kê doanh thu, trạng thái đơn và tồn kho thấp cho Quản trị.
/// 
/// Quy tắc nghiệp vụ (TC-18):
/// - Đơn đang xử lý (PENDING, CONFIRMED, SHIPPING) hoặc CANCELLED KHÔNG được tính vào doanh thu.
/// - Chỉ đơn hàng DELIVERED đã thu tiền (COD) mới được tính doanh thu.
/// - Doanh thu tiền hàng (Subtotal) và Phí vận chuyển (ShippingFee) được thống kê riêng biệt.
/// </summary>
public class ReportingService : IReportingService
{
    private readonly OrderDbContext _dbContext;
    private readonly IInventoryContract _inventoryContract;

    public ReportingService(OrderDbContext dbContext, IInventoryContract inventoryContract)
    {
        _dbContext = dbContext;
        _inventoryContract = inventoryContract;
    }

    public async Task<Result<DashboardReportDto>> GetDashboardReportAsync(
        DateTimeOffset? fromDate,
        DateTimeOffset? toDate,
        int lowStockThreshold = 10,
        CancellationToken cancellationToken = default)
    {
        var statusReportResult = await GetOrderStatusReportAsync(cancellationToken);
        var revenueReportResult = await GetRevenueReportAsync(fromDate, toDate, cancellationToken);
        var lowStockItems = await _inventoryContract.GetLowStockAsync(lowStockThreshold, cancellationToken);

        var dashboard = new DashboardReportDto(
            statusReportResult.Value.StatusCounts,
            statusReportResult.Value.TotalOrders,
            revenueReportResult.Value,
            lowStockItems);

        return Result.Success(dashboard);
    }

    public async Task<Result<RevenueReportDto>> GetRevenueReportAsync(
        DateTimeOffset? fromDate,
        DateTimeOffset? toDate,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Orders
            .AsNoTracking()
            .Where(o => o.Status == OrderStatus.DELIVERED);

        if (fromDate.HasValue)
        {
            query = query.Where(o => (o.CollectedAt ?? o.DeliveredAt ?? o.CreatedAt) >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(o => (o.CollectedAt ?? o.DeliveredAt ?? o.CreatedAt) <= toDate.Value);
        }

        var deliveredOrders = await query
            .Select(o => new { o.Subtotal, o.ShippingFee, o.TotalAmount })
            .ToListAsync(cancellationToken);

        long totalRevenue = deliveredOrders.Sum(o => o.Subtotal);
        long totalShippingFee = deliveredOrders.Sum(o => o.ShippingFee);
        int deliveredOrderCount = deliveredOrders.Count;

        return Result.Success(new RevenueReportDto(
            totalRevenue,
            totalShippingFee,
            deliveredOrderCount,
            fromDate,
            toDate));
    }

    public async Task<Result<OrderStatusReportDto>> GetOrderStatusReportAsync(
        CancellationToken cancellationToken = default)
    {
        var rawCounts = await _dbContext.Orders
            .AsNoTracking()
            .GroupBy(o => o.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var statusMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            [OrderStatus.PENDING.ToString()] = 0,
            [OrderStatus.CONFIRMED.ToString()] = 0,
            [OrderStatus.SHIPPING.ToString()] = 0,
            [OrderStatus.DELIVERED.ToString()] = 0,
            [OrderStatus.CANCELLED.ToString()] = 0
        };

        foreach (var item in rawCounts)
        {
            statusMap[item.Status.ToString()] = item.Count;
        }

        int totalOrders = statusMap.Values.Sum();

        return Result.Success(new OrderStatusReportDto(statusMap, totalOrders));
    }
}
