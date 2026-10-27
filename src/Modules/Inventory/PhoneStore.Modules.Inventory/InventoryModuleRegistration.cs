using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PhoneStore.BuildingBlocks.Application;
using PhoneStore.BuildingBlocks.Common;
using PhoneStore.BuildingBlocks.Contracts.Inventory;
using PhoneStore.BuildingBlocks.Exceptions;
using PhoneStore.Modules.Inventory.Application.DTOs;
using PhoneStore.Modules.Inventory.Application.Services;
using PhoneStore.Modules.Inventory.Infrastructure.Persistence;

namespace PhoneStore.Modules.Inventory;

public static class InventoryModuleRegistration
{
    public static IServiceCollection AddInventoryModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=phonestore_db;Username=postgres;Password=postgres";

        services.AddDbContext<InventoryDbContext>(options =>
            options.UseNpgsql(connectionString, x => x.MigrationsHistoryTable("__EFMigrationsHistory", InventoryDbContext.Schema)));

        services.AddScoped<IInventoryContract, InventoryContract>();
        services.AddScoped<IInventoryService, InventoryService>();

        return services;
    }
}

/// <summary>
/// FR-14 (N2LHTBLTT-21): Quản trị tồn kho — nhập kho, điều chỉnh tồn, nhật ký tồn kho.
///
/// Các endpoint quản trị yêu cầu vai trò STAFF hoặc ADMIN.
/// </summary>
[ApiController]
[Route("api/v1/admin/inventory")]
[Authorize(Policy = AuthorizationPolicies.RequireStaff)]
public class InventoryController : ControllerBase
{
    private readonly IInventoryService _inventoryService;
    private readonly ICurrentUser _currentUser;

    public InventoryController(IInventoryService inventoryService, ICurrentUser currentUser)
    {
        _inventoryService = inventoryService;
        _currentUser = currentUser;
    }

    /// <summary>
    /// FR-14: Danh sách tồn kho của toàn bộ phiên bản sản phẩm.
    /// </summary>
    [HttpGet("stocks")]
    public async Task<IActionResult> GetStocks(CancellationToken cancellationToken)
    {
        var result = await _inventoryService.GetStocksAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<StockDto>>.Ok(result.Value));
    }

    /// <summary>
    /// FR-14: Tồn kho của một phiên bản sản phẩm.
    /// </summary>
    [HttpGet("stocks/{productVariantId:long}")]
    public async Task<IActionResult> GetStock(long productVariantId, CancellationToken cancellationToken)
    {
        var result = await _inventoryService.GetStockAsync(productVariantId, cancellationToken);
        return Ok(ApiResponse<StockDto>.Ok(result.Value));
    }

    /// <summary>
    /// FR-14: Nhập kho — tăng tồn kho cho một phiên bản sản phẩm.
    /// </summary>
    [HttpPost("stocks/import")]
    public async Task<IActionResult> ImportStock([FromBody] ImportStockRequest request, CancellationToken cancellationToken)
    {
        var result = await _inventoryService.ImportStockAsync(request, _currentUser.UserId, cancellationToken);
        return Ok(ApiResponse<StockDto>.Ok(result.Value, "Nhập kho thành công."));
    }

    /// <summary>
    /// FR-14: Nhập kho hàng loạt.
    /// </summary>
    [HttpPost("stocks/import/batch")]
    public async Task<IActionResult> ImportStockBatch([FromBody] ImportStockBatchRequest request, CancellationToken cancellationToken)
    {
        var result = await _inventoryService.ImportStockBatchAsync(request, _currentUser.UserId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<StockDto>>.Ok(result.Value, "Nhập kho hàng loạt thành công."));
    }

    /// <summary>
    /// FR-14: Điều chỉnh tồn kho thủ công (tăng hoặc giảm).
    /// </summary>
    [HttpPost("stocks/adjust")]
    public async Task<IActionResult> AdjustStock([FromBody] AdjustStockRequest request, CancellationToken cancellationToken)
    {
        var result = await _inventoryService.AdjustStockAsync(request, _currentUser.UserId, cancellationToken);
        return Ok(ApiResponse<StockDto>.Ok(result.Value, "Điều chỉnh tồn kho thành công."));
    }

    /// <summary>
    /// FR-14: Nhật ký tồn kho — mọi biến động đều được ghi lại.
    /// </summary>
    [HttpGet("movements")]
    public async Task<IActionResult> GetMovements([FromQuery] StockMovementFilterQuery query, CancellationToken cancellationToken)
    {
        var result = await _inventoryService.GetMovementsAsync(query, cancellationToken);
        return Ok(ApiResponse<PagedResult<StockMovementDto>>.Ok(result.Value));
    }

    /// <summary>
    /// FR-14: Danh sách phiên bản sản phẩm có tồn kho thấp (cảnh báo hết hàng).
    /// </summary>
    [HttpGet("low-stock")]
    public async Task<IActionResult> GetLowStock([FromQuery] int threshold = 10, CancellationToken cancellationToken = default)
    {
        var result = await _inventoryService.GetLowStockAsync(threshold, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<StockDto>>.Ok(result.Value));
    }
}
