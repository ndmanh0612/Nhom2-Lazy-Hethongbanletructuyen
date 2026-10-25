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

[ApiController]
[Route("api/v1/orders")]
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
    /// <param name="status">Lọc theo trạng thái đơn (PENDING, CONFIRMED, SHIPPING, DELIVERED, DELIVERY_FAILED, CANCELLED).</param>
    /// <param name="fromDate">Chỉ lấy đơn tạo từ thời điểm này trở đi (ISO-8601).</param>
    /// <param name="toDate">Chỉ lấy đơn tạo trước thời điểm này (ISO-8601).</param>
    /// <param name="page">Số trang, bắt đầu từ 1.</param>
    /// <param name="pageSize">Số bản ghi mỗi trang (tối đa 100).</param>
    [HttpGet]
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
    public async Task<IActionResult> GetOrder(long orderId, CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var result = await _orderService.GetOrderAsync(orderId, userId, cancellationToken);
        return Ok(ApiResponse<OrderDetailDto>.Ok(result.Value));
    }

    /// <summary>
    /// FR-07: Checkout COD — chuyển giỏ hàng thành đơn hàng.
    /// </summary>
    [HttpPost("checkout")]
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
    public async Task<IActionResult> CancelOrder(
        long orderId,
        [FromBody] CancelOrderRequest request,
        CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var result = await _orderService.CancelOrderAsync(orderId, userId, request, cancellationToken);
        return Ok(ApiResponse<OrderDetailDto>.Ok(result.Value, "Đã hủy đơn hàng."));
    }

    private long RequireUserId()
    {
        return _currentUser.UserId
            ?? throw new UnauthorizedException("Vui lòng đăng nhập để sử dụng chức năng này.");
    }
}
