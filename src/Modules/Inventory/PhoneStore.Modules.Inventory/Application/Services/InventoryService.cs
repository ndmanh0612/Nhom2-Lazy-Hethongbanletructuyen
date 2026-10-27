using Microsoft.EntityFrameworkCore;
using PhoneStore.BuildingBlocks.Application;
using PhoneStore.BuildingBlocks.Domain;
using PhoneStore.BuildingBlocks.Exceptions;
using PhoneStore.Modules.Inventory.Application.DTOs;
using PhoneStore.Modules.Inventory.Domain.Entities;
using PhoneStore.Modules.Inventory.Infrastructure.Persistence;

namespace PhoneStore.Modules.Inventory.Application.Services;

public interface IInventoryService
{
    Task<Result<IReadOnlyList<StockDto>>> GetStocksAsync(CancellationToken cancellationToken = default);
    Task<Result<StockDto>> GetStockAsync(long productVariantId, CancellationToken cancellationToken = default);
    Task<Result<StockDto>> ImportStockAsync(ImportStockRequest request, long? performedBy, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<StockDto>>> ImportStockBatchAsync(ImportStockBatchRequest request, long? performedBy, CancellationToken cancellationToken = default);
    Task<Result<StockDto>> AdjustStockAsync(AdjustStockRequest request, long? performedBy, CancellationToken cancellationToken = default);
    Task<Result<PagedResult<StockMovementDto>>> GetMovementsAsync(StockMovementFilterQuery query, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<StockDto>>> GetLowStockAsync(int threshold, CancellationToken cancellationToken = default);
}

/// <summary>
/// FR-14 (N2LHTBLTT-21) + N2LHTBLTT-22: Nghiệp vụ tồn kho.
///
/// Mọi thay đổi tồn kho đều ghi một dòng vào <c>inventory.stock_movements</c>
/// để tra cứu / đối soát.
/// </summary>
public class InventoryService : IInventoryService
{
    private readonly InventoryDbContext _dbContext;

    public InventoryService(InventoryDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<IReadOnlyList<StockDto>>> GetStocksAsync(CancellationToken cancellationToken = default)
    {
        var stocks = await _dbContext.ProductStocks
            .AsNoTracking()
            .OrderBy(stock => stock.ProductVariantId)
            .Select(stock => new StockDto(stock.ProductVariantId, stock.StockQuantity, stock.UpdatedAt))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<StockDto>>(stocks);
    }

    public async Task<Result<StockDto>> GetStockAsync(long productVariantId, CancellationToken cancellationToken = default)
    {
        var stock = await _dbContext.ProductStocks
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.ProductVariantId == productVariantId, cancellationToken);

        if (stock is null)
        {
            throw new NotFoundException(
                "Chưa thiết lập tồn kho cho phiên bản sản phẩm này.",
                "STOCK_NOT_FOUND");
        }

        return Result.Success(new StockDto(stock.ProductVariantId, stock.StockQuantity, stock.UpdatedAt));
    }

    public async Task<Result<StockDto>> ImportStockAsync(
        ImportStockRequest request,
        long? performedBy,
        CancellationToken cancellationToken = default)
    {
        if (request.Quantity <= 0)
        {
            throw new ValidationException("quantity", "Số lượng nhập kho phải lớn hơn 0.");
        }

        var stock = await GetOrCreateStockAsync(request.ProductVariantId, cancellationToken);

        var before = stock.StockQuantity;
        stock.Import(request.Quantity);

        _dbContext.StockMovements.Add(StockMovement.Create(
            request.ProductVariantId,
            request.Quantity,
            before,
            stock.StockQuantity,
            StockMovementType.IMPORT,
            orderId: null,
            note: request.Note,
            performedBy: performedBy));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new StockDto(stock.ProductVariantId, stock.StockQuantity, stock.UpdatedAt));
    }

    public async Task<Result<IReadOnlyList<StockDto>>> ImportStockBatchAsync(
        ImportStockBatchRequest request,
        long? performedBy,
        CancellationToken cancellationToken = default)
    {
        if (request.Items is null || request.Items.Count == 0)
        {
            throw new ValidationException("items", "Danh sách nhập kho không được để trống.");
        }

        var results = new List<StockDto>(request.Items.Count);

        foreach (var item in request.Items)
        {
            var result = await ImportStockAsync(
                item with { Note = item.Note ?? request.Note },
                performedBy,
                cancellationToken);

            results.Add(result.Value);
        }

        return Result.Success<IReadOnlyList<StockDto>>(results);
    }

    public async Task<Result<StockDto>> AdjustStockAsync(
        AdjustStockRequest request,
        long? performedBy,
        CancellationToken cancellationToken = default)
    {
        if (request.Delta == 0)
        {
            throw new ValidationException("delta", "Số lượng điều chỉnh không được bằng 0.");
        }

        var stock = await _dbContext.ProductStocks
            .FirstOrDefaultAsync(s => s.ProductVariantId == request.ProductVariantId, cancellationToken);

        if (stock is null)
        {
            throw new NotFoundException(
                "Chưa thiết lập tồn kho cho phiên bản sản phẩm này.",
                "STOCK_NOT_FOUND");
        }

        var before = stock.StockQuantity;
        stock.Adjust(request.Delta);

        _dbContext.StockMovements.Add(StockMovement.Create(
            request.ProductVariantId,
            request.Delta,
            before,
            stock.StockQuantity,
            StockMovementType.ADJUSTMENT,
            orderId: null,
            note: request.Note,
            performedBy: performedBy));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new StockDto(stock.ProductVariantId, stock.StockQuantity, stock.UpdatedAt));
    }

    public async Task<Result<PagedResult<StockMovementDto>>> GetMovementsAsync(
        StockMovementFilterQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 100 ? 20 : query.PageSize;

        var baseQuery = _dbContext.StockMovements.AsNoTracking();

        if (query.ProductVariantId.HasValue)
        {
            baseQuery = baseQuery.Where(m => m.ProductVariantId == query.ProductVariantId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Type))
        {
            if (!Enum.TryParse<StockMovementType>(query.Type, ignoreCase: true, out var type))
            {
                throw new ValidationException(
                    "type",
                    "Loại biến động không hợp lệ. Chỉ nhận IMPORT, ADJUSTMENT, RESERVATION hoặc RESTORE.");
            }

            baseQuery = baseQuery.Where(m => m.Type == type);
        }

        if (query.OrderId.HasValue)
        {
            baseQuery = baseQuery.Where(m => m.OrderId == query.OrderId.Value);
        }

        if (query.FromDate.HasValue)
        {
            var fromDate = query.FromDate.Value;
            baseQuery = baseQuery.Where(m => m.CreatedAt >= fromDate);
        }

        if (query.ToDate.HasValue)
        {
            var toDate = query.ToDate.Value;
            baseQuery = baseQuery.Where(m => m.CreatedAt <= toDate);
        }

        var totalCount = await baseQuery.LongCountAsync(cancellationToken);

        var movements = await baseQuery
            .OrderByDescending(m => m.CreatedAt)
            .ThenByDescending(m => m.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var dtos = movements.Select(m => new StockMovementDto(
            m.Id,
            m.ProductVariantId,
            m.Quantity,
            m.QuantityBefore,
            m.QuantityAfter,
            m.Type.ToString(),
            m.OrderId,
            m.Note,
            m.PerformedBy,
            m.CreatedAt)).ToList();

        return Result.Success(PagedResult<StockMovementDto>.Create(dtos, page, pageSize, totalCount));
    }

    public async Task<Result<IReadOnlyList<StockDto>>> GetLowStockAsync(int threshold, CancellationToken cancellationToken = default)
    {
        var stocks = await _dbContext.ProductStocks
            .AsNoTracking()
            .Where(stock => stock.StockQuantity <= threshold)
            .OrderBy(stock => stock.StockQuantity)
            .Select(stock => new StockDto(stock.ProductVariantId, stock.StockQuantity, stock.UpdatedAt))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<StockDto>>(stocks);
    }

    private async Task<ProductStock> GetOrCreateStockAsync(long productVariantId, CancellationToken cancellationToken)
    {
        var stock = await _dbContext.ProductStocks
            .FirstOrDefaultAsync(s => s.ProductVariantId == productVariantId, cancellationToken);

        if (stock is not null)
        {
            return stock;
        }

        stock = ProductStock.Create(productVariantId, 0);
        _dbContext.ProductStocks.Add(stock);

        return stock;
    }
}
