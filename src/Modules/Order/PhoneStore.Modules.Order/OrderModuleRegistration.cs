using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PhoneStore.BuildingBlocks.Application;
using PhoneStore.BuildingBlocks.Common;
using PhoneStore.BuildingBlocks.Exceptions;
using PhoneStore.Modules.Order.Application.DTOs;
using PhoneStore.Modules.Order.Application.Services;
using PhoneStore.Modules.Order.Infrastructure.Persistence;
using PhoneStore.Modules.Order.Infrastructure.Repositories;

namespace PhoneStore.Modules.Order;

public static class OrderModuleRegistration
{
    public static IServiceCollection AddOrderModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=phonestore_db;Username=postgres;Password=postgres";

        services.AddDbContext<OrderDbContext>(options =>
            options.UseNpgsql(connectionString, x => x.MigrationsHistoryTable("__EFMigrationsHistory", OrderDbContext.Schema)));

        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IOrderService, OrderService>();

        return services;
    }
}

/// <summary>
/// FR-07 / FR-08 / FR-09: Nghiệp vụ đặt hàng của khách hàng.
/// FR-10 / FR-11: Nghiệp vụ xác nhận, giao hàng và thu COD của nhân viên.
/// </summary>
[ApiController]
[Route("api/v1/orders")]
[ApiExplorerSettings(IgnoreApi = true)] // Tạm ẩn trên Swagger cho Sprint 1 (bật lại ở Sprint 2)
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;
    private readonly ICurrentUser _currentUser;

    public OrdersController(IOrderService orderService, ICurrentUser currentUser)
    {
        _orderService = orderService;
        _currentUser = currentUser;
    }

    /// <summary>
    /// FR-08: Danh sách đơn hàng của khách hàng đang đăng nhập.
    /// Hỗ trợ lọc theo trạng thái, khoảng thời gian tạo đơn và phân trang.
    /// </summary>
    /// <param name="query">Bộ lọc và tham số phân trang: status, fromDate, toDate, page, pageSize.</param>
    /// <param name="cancellationToken">Token hủy thao tác.</param>
    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.RequireAuthenticated)]
    public async Task<IActionResult> GetOrders(
        [FromQuery] OrderFilterQuery query,
        CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var result = await _orderService.GetOrdersAsync(userId, query, cancellationToken);
        return Ok(ApiResponse<PagedResult<OrderDto>>.Ok(result.Value));
    }

    /// <summary>
    /// FR-08: Chi tiết một đơn hàng.
    /// </summary>
    [HttpGet("{orderId:long}")]
    [Authorize(Policy = AuthorizationPolicies.RequireAuthenticated)]
    public async Task<IActionResult> GetOrder(long orderId, CancellationToken cancellationToken)
    {
        var userId = RequireUserId();

        // N2LHTBLTT-27: nhan vien / quan tri duoc xem don hang cua khach hang.
        var isStaff = _currentUser.IsInRole(AppRoles.Staff) || _currentUser.IsInRole(AppRoles.Admin);

        var result = await _orderService.GetOrderAsync(orderId, userId, isStaff, cancellationToken);
        return Ok(ApiResponse<OrderDetailDto>.Ok(result.Value));
    }

    /// <summary>
    /// FR-07: Checkout COD — chuyển giỏ hàng thành đơn hàng.
    /// </summary>
    [HttpPost("checkout")]
    [Authorize(Policy = AuthorizationPolicies.RequireAuthenticated)]
    public async Task<IActionResult> Checkout([FromBody] CheckoutRequest request, CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var result = await _orderService.CheckoutAsync(userId, request, cancellationToken);
        return StatusCode(201, ApiResponse<OrderDto>.Ok(result.Value, "Đặt hàng thành công."));
    }

    /// <summary>
    /// FR-09: Hủy đơn hàng — phát event hoàn trả tồn kho.
    /// </summary>
    [HttpPost("{orderId:long}/cancel")]
    [Authorize(Policy = AuthorizationPolicies.RequireAuthenticated)]
    public async Task<IActionResult> CancelOrder(
        long orderId,
        [FromBody] CancelOrderRequest request,
        CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var result = await _orderService.CancelOrderAsync(orderId, userId, request, cancellationToken);
        return Ok(ApiResponse<OrderDetailDto>.Ok(result.Value, "Đã hủy đơn hàng."));
    }

    // -----------------------------------------------------------------------
    // FR-10 (N2LHTBLTT-27) / FR-11 (N2LHTBLTT-28): nghiệp vụ nhân viên.
    // -----------------------------------------------------------------------

    /// <summary>
    /// FR-10: Xác nhận đơn hàng (PENDING → CONFIRMED).
    /// </summary>
    [HttpPost("{orderId:long}/confirm")]
    [Authorize(Policy = AuthorizationPolicies.RequireStaff)]
    public async Task<IActionResult> ConfirmOrder(
        long orderId,
        [FromBody] ConfirmOrderRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _orderService.ConfirmOrderAsync(orderId, request, cancellationToken);
        return Ok(ApiResponse<OrderDetailDto>.Ok(result.Value, "Đã xác nhận đơn hàng."));
    }

    /// <summary>
    /// FR-10: Bàn giao vận chuyển (CONFIRMED → SHIPPING).
    /// </summary>
    [HttpPost("{orderId:long}/ship")]
    [Authorize(Policy = AuthorizationPolicies.RequireStaff)]
    public async Task<IActionResult> ShipOrder(
        long orderId,
        [FromBody] ShipOrderRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _orderService.ShipOrderAsync(orderId, request, cancellationToken);
        return Ok(ApiResponse<OrderDetailDto>.Ok(result.Value, "Đã bàn giao đơn hàng cho vận chuyển."));
    }

    /// <summary>
    /// FR-11: Giao hàng thành công và thu tiền COD (SHIPPING → DELIVERED).
    /// </summary>
    [HttpPost("{orderId:long}/deliver")]
    [Authorize(Policy = AuthorizationPolicies.RequireStaff)]
    public async Task<IActionResult> DeliverOrder(
        long orderId,
        [FromBody] DeliverOrderRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _orderService.DeliverOrderAsync(orderId, request, cancellationToken);
        return Ok(ApiResponse<OrderDetailDto>.Ok(result.Value, "Giao hàng thành công, đã thu tiền COD."));
    }

    /// <summary>
    /// FR-11: Giao hàng thất bại — giao lại hoặc huỷ đơn (hoàn tồn kho nếu huỷ).
    /// </summary>
    [HttpPost("{orderId:long}/delivery-failed")]
    [Authorize(Policy = AuthorizationPolicies.RequireStaff)]
    public async Task<IActionResult> FailDelivery(
        long orderId,
        [FromBody] DeliveryFailedRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _orderService.FailDeliveryAsync(orderId, request, cancellationToken);
        return Ok(ApiResponse<OrderDetailDto>.Ok(result.Value, "Đã ghi nhận giao hàng thất bại."));
    }

    /// <summary>
    /// FR-11: Danh sách đơn hàng đã giao thành công và đã thu tiền COD.
    /// </summary>
    [HttpGet("/api/v1/admin/orders/cod-collections")]
    [Authorize(Policy = AuthorizationPolicies.RequireStaff)]
    public async Task<IActionResult> GetCodCollections(
        [FromQuery] OrderFilterQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _orderService.GetCodCollectionsAsync(query, cancellationToken);
        return Ok(ApiResponse<PagedResult<CodCollectionDto>>.Ok(result.Value));
    }

    private long RequireUserId()
    {
        return _currentUser.UserId
            ?? throw new UnauthorizedException("Vui lòng đăng nhập để sử dụng chức năng này.");
    }
}
