using Microsoft.EntityFrameworkCore;
using PhoneStore.BuildingBlocks.Contracts.Inventory;
using PhoneStore.Modules.Inventory.Domain.Entities;
using PhoneStore.Modules.Inventory.Infrastructure.Persistence;

namespace PhoneStore.Modules.Inventory.Application.Services;

/// <summary>
/// Inventory là module sở hữu dữ liệu tồn kho, vì vậy nó là nơi implement IInventoryContract.
///
/// N2LHTBLTT-22: đặt giữ tồn kho dùng khóa dòng <c>FOR UPDATE</c> để tránh
/// race condition khi hai khách hàng cùng checkout một phiên bản sắp hết hàng —
/// chỉ một request được trừ tồn, request còn lại nhận false và được từ chối.
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
        long? orderId = null,
        CancellationToken cancellationToken = default)
    {
        if (items.Count == 0)
        {
            return true;
        }

        var variantIds = items.Select(item => item.ProductVariantId).Distinct().ToList();
        var requested = items.ToDictionary(item => item.ProductVariantId, item => item.Quantity);

        // Khóa dòng theo thứ tự id tăng dần để tránh deadlock khi hai giao
        // dịch chạy song song với tập phiên bản khác nhau.
        //
        // Lưu ý: tên cột trong database do EF tạo là PascalCase ("ProductVariantId"),
        // nên raw SQL phải dùng đúng tên cột — không dùng naming convention snake_case.
        var stocks = await _dbContext.ProductStocks
            .FromSqlInterpolated($"SELECT * FROM inventory.product_stocks WHERE \"ProductVariantId\" = ANY ({variantIds.ToArray()}) ORDER BY \"ProductVariantId\" FOR UPDATE")
            .ToListAsync(cancellationToken);

        // Phiên bản chưa có bản ghi tồn kho tức là chưa được thiết lập tồn kho.
        if (stocks.Count != variantIds.Count)
        {
            return false;
        }

        foreach (var stock in stocks)
        {
            if (stock.StockQuantity < requested[stock.ProductVariantId])
            {
                return false;
            }
        }

        foreach (var stock in stocks)
        {
            var before = stock.StockQuantity;
            var quantity = requested[stock.ProductVariantId];
            stock.Reserve(quantity);

            _dbContext.StockMovements.Add(StockMovement.Create(
                stock.ProductVariantId,
                -quantity,
                before,
                stock.StockQuantity,
                StockMovementType.RESERVATION,
                orderId: orderId));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task RestoreStockAsync(
        IReadOnlyCollection<StockChangeItem> items,
        long? orderId = null,
        CancellationToken cancellationToken = default)
    {
        if (items.Count == 0)
        {
            return;
        }

        var variantIds = items.Select(item => item.ProductVariantId).Distinct().ToList();
        var quantities = items.ToDictionary(item => item.ProductVariantId, item => item.Quantity);

        var stocks = await _dbContext.ProductStocks
            .Where(stock => variantIds.Contains(stock.ProductVariantId))
            .ToListAsync(cancellationToken);

        foreach (var stock in stocks)
        {
            var before = stock.StockQuantity;
            var quantity = quantities[stock.ProductVariantId];
            stock.Restore(quantity);

            _dbContext.StockMovements.Add(StockMovement.Create(
                stock.ProductVariantId,
                quantity,
                before,
                stock.StockQuantity,
                StockMovementType.RESTORE,
                orderId: orderId));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
