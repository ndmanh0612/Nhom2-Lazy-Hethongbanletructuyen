using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PhoneStore.BuildingBlocks.Application;
using PhoneStore.BuildingBlocks.Common;
using PhoneStore.BuildingBlocks.Contracts.Cart;
using PhoneStore.BuildingBlocks.Exceptions;
using PhoneStore.Modules.Cart.Application.DTOs;
using PhoneStore.Modules.Cart.Application.Services;
using PhoneStore.Modules.Cart.Infrastructure.Persistence;
using PhoneStore.Modules.Cart.Infrastructure.Repositories;

namespace PhoneStore.Modules.Cart;

public static class CartModuleRegistration
{
    public static IServiceCollection AddCartModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=phonestore_db;Username=postgres;Password=postgres";

        services.AddDbContext<CartDbContext>(options =>
            options.UseNpgsql(connectionString, x => x.MigrationsHistoryTable("__EFMigrationsHistory", CartDbContext.Schema)));

        services.AddScoped<ICartRepository, CartRepository>();
        services.AddScoped<ICartService, CartService>();
        services.AddScoped<ICartContract, CartContract>();

        return services;
    }
}

[ApiController]
[Route("api/v1/cart")]
[Authorize(Policy = AuthorizationPolicies.RequireAuthenticated)]
public class CartController : ControllerBase
{
    private readonly ICartService _cartService;
    private readonly ICurrentUser _currentUser;

    public CartController(ICartService cartService, ICurrentUser currentUser)
    {
        _cartService = cartService;
        _currentUser = currentUser;
    }

    /// <summary>
    /// FR-06: Xem giỏ hàng của khách hàng đang đăng nhập.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetCart(CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var result = await _cartService.GetCartAsync(userId, cancellationToken);
        return Ok(ApiResponse<CartDto>.Ok(result.Value));
    }

    /// <summary>
    /// FR-06: Thêm sản phẩm vào giỏ. Giá do server lấy từ Catalog, không nhận từ client.
    /// </summary>
    [HttpPost("items")]
    public async Task<IActionResult> AddItem([FromBody] AddToCartRequest request, CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var result = await _cartService.AddItemAsync(userId, request, cancellationToken);
        return Ok(ApiResponse<CartDto>.Ok(result.Value, "Đã thêm sản phẩm vào giỏ hàng."));
    }

    /// <summary>
    /// FR-06: Cập nhật số lượng của một sản phẩm trong giỏ.
    /// </summary>
    [HttpPut("items/{cartItemId:long}")]
    public async Task<IActionResult> UpdateItem(
        long cartItemId,
        [FromBody] UpdateCartItemRequest request,
        CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var result = await _cartService.UpdateItemAsync(userId, cartItemId, request, cancellationToken);
        return Ok(ApiResponse<CartDto>.Ok(result.Value, "Đã cập nhật giỏ hàng."));
    }

    /// <summary>
    /// FR-06: Xóa một sản phẩm khỏi giỏ.
    /// </summary>
    [HttpDelete("items/{cartItemId:long}")]
    public async Task<IActionResult> RemoveItem(long cartItemId, CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var result = await _cartService.RemoveItemAsync(userId, cartItemId, cancellationToken);
        return Ok(ApiResponse<CartDto>.Ok(result.Value, "Đã xóa sản phẩm khỏi giỏ hàng."));
    }

    private long RequireUserId()
    {
        return _currentUser.UserId
            ?? throw new UnauthorizedException("Vui lòng đăng nhập để sử dụng giỏ hàng.");
    }
}
