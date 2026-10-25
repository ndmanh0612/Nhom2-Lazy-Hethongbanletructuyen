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
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetOrders(CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var result = await _orderService.GetOrdersAsync(userId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<OrderDto>>.Ok(result.Value));
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
