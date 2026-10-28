using Microsoft.AspNetCore.Mvc;
using PhoneStore.Modules.Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace PhoneStore.Modules.Inventory.Endpoints;

[ApiController]
[Route("api/v1/admin/inventory")]
public class InventoryController : ControllerBase
{
    private readonly InventoryDbContext _dbContext;

    public InventoryController(InventoryDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Lấy danh sách tồn kho
    /// </summary>
    [HttpGet("stocks")]
    public async Task<IActionResult> GetStocks()
    {
        var stocks = await _dbContext.Stocks
            .OrderBy(s => s.Sku)
            .Select(s => new
            {
                s.Id,
                s.ProductVariantId,
                s.Sku,
                s.QuantityOnHand,
                s.QuantityReserved,
                Available = s.QuantityOnHand - s.QuantityReserved,
                s.CreatedAt,
                s.UpdatedAt
            })
            .ToListAsync();

        return Ok(stocks);
    }

    /// <summary>
    /// Lấy lịch sử biến động kho
    /// </summary>
    [HttpGet("movements")]
    public async Task<IActionResult> GetMovements()
    {
        var movements = await _dbContext.StockMovements
            .OrderByDescending(m => m.CreatedAt)
            .Take(50)
            .Select(m => new
            {
                m.Id,
                m.StockId,
                Type = m.Type.ToString(),
                m.Quantity,
                m.QuantityAfter,
                m.Reason,
                m.CreatedAt
            })
            .ToListAsync();

        return Ok(movements);
    }
}
