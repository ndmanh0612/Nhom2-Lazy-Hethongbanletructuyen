using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhoneStore.BuildingBlocks.Application;
using PhoneStore.BuildingBlocks.Common;
using PhoneStore.Modules.Order.Application.DTOs;
using PhoneStore.Modules.Order.Application.Services;

namespace PhoneStore.Modules.Order.Endpoints;

/// <summary>
/// FR-16 (N2LHTBLTT-32): API thống kê báo cáo cho Quản trị viên (Admin / Staff).
/// Bao gồm thống kê doanh thu, trạng thái đơn hàng và cảnh báo tồn kho thấp.
/// </summary>
[ApiController]
[Route("api/v1/admin/reports")]
[Authorize(Policy = AuthorizationPolicies.RequireStaff)]
public class ReportsController : ControllerBase
{
    private readonly IReportingService _reportingService;

    public ReportsController(IReportingService reportingService)
    {
        _reportingService = reportingService;
    }

    /// <summary>
    /// FR-16 (N2LHTBLTT-32): Báo cáo tổng quan dashboard (doanh thu, đơn theo trạng thái, tồn kho thấp).
    /// </summary>
    /// <param name="fromDate">Từ ngày tính doanh thu (tùy chọn).</param>
    /// <param name="toDate">Đến ngày tính doanh thu (tùy chọn).</param>
    /// <param name="lowStockThreshold">Ngưỡng cảnh báo tồn kho thấp (mặc định 10).</param>
    /// <param name="cancellationToken">CancellationToken.</param>
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(
        [FromQuery] DateTimeOffset? fromDate,
        [FromQuery] DateTimeOffset? toDate,
        [FromQuery] int lowStockThreshold = 10,
        CancellationToken cancellationToken = default)
    {
        var result = await _reportingService.GetDashboardReportAsync(fromDate, toDate, lowStockThreshold, cancellationToken);
        return Ok(ApiResponse<DashboardReportDto>.Ok(result.Value));
    }

    /// <summary>
    /// FR-16: Báo cáo chi tiết doanh thu đơn DELIVERED đã thu tiền (COD).
    /// Phí giao hàng được thống kê riêng biệt.
    /// </summary>
    [HttpGet("revenue")]
    public async Task<IActionResult> GetRevenue(
        [FromQuery] DateTimeOffset? fromDate,
        [FromQuery] DateTimeOffset? toDate,
        CancellationToken cancellationToken = default)
    {
        var result = await _reportingService.GetRevenueReportAsync(fromDate, toDate, cancellationToken);
        return Ok(ApiResponse<RevenueReportDto>.Ok(result.Value));
    }

    /// <summary>
    /// FR-16: Báo cáo số lượng đơn hàng theo từng trạng thái (PENDING, CONFIRMED, SHIPPING, DELIVERED, CANCELLED).
    /// </summary>
    [HttpGet("order-status")]
    public async Task<IActionResult> GetOrderStatus(CancellationToken cancellationToken = default)
    {
        var result = await _reportingService.GetOrderStatusReportAsync(cancellationToken);
        return Ok(ApiResponse<OrderStatusReportDto>.Ok(result.Value));
    }
}
