using Microsoft.EntityFrameworkCore;
using PhoneStore.BuildingBlocks.Contracts.Inventory;
using PhoneStore.Modules.Inventory.Domain.Entities;
using PhoneStore.Modules.Inventory.Infrastructure.Persistence;

namespace PhoneStore.Modules.Inventory.Application.Services;

/// <summary>
/// Inventory là module sở hữu dữ liệu tồn kho, vì vậy nó là nơi implement IInventoryContract.
/// </summary>
public class InventoryContract : IInventoryContract
{
    private readonly InventoryDbContext _dbContext;

    public InventoryContract(InventoryDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> TryReserveStockAsync(
        IReadOnlyCollection<StockChangeItem> items,
        CancellationToken cancellationToken = default)
    {
        if (items.Count == 0)
        {
            return true;
        }

        var variantIds = items.Select(item => item.ProductVariantId).Distinct().ToList();
        var stocks = await _dbContext.ProductStocks
            .Where(stock => variantIds.Contains(stock.ProductVariantId))
            .ToListAsync(cancellationToken);

        // Phiên bản chưa có bản ghi tồn kho tức là chưa được thiết lập tồn kho.
        if (stocks.Count != variantIds.Count)
        {
            return false;
        }

        var requested = items.ToDictionary(item => item.ProductVariantId, item => item.Quantity);
        foreach (var stock in stocks)
        {
            if (stock.StockQuantity < requested[stock.ProductVariantId])
            {
                return false;
            }
        }

        foreach (var stock in stocks)
        {
            stock.Reserve(requested[stock.ProductVariantId]);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task RestoreStockAsync(
        IReadOnlyCollection<StockChangeItem> items,
        CancellationToken cancellationToken = default)
    {
        if (items.Count == 0)
        {
            return;
        }

        var variantIds = items.Select(item => item.ProductVariantId).Distinct().ToList();
        var stocks = await _dbContext.ProductStocks
            .Where(stock => variantIds.Contains(stock.ProductVariantId))
            .ToListAsync(cancellationToken);

        var quantities = items.ToDictionary(item => item.ProductVariantId, item => item.Quantity);
        foreach (var stock in stocks)
        {
            stock.Restore(quantities[stock.ProductVariantId]);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
