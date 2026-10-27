using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PhoneStore.BuildingBlocks.Contracts.Catalog;
using PhoneStore.Modules.Inventory.Domain.Entities;
using PhoneStore.Modules.Inventory.Infrastructure.Persistence;

namespace PhoneStore.Modules.Inventory.Application.EventHandlers;

/// <summary>
/// N2LHTBLTT-23: Nhận event ProductVariantCreated từ module Catalog và khởi tạo
/// bản ghi tồn kho trong schema <c>inventory</c>.
///
/// Nếu thiếu handler này, sản phẩm tạo mới qua API quản trị sẽ không có tồn kho
/// và mọi lần checkout với SKU đó đều bị từ chối với mã INSUFFICIENT_STOCK.
/// </summary>
public class ProductVariantCreatedHandler : INotificationHandler<ProductVariantCreated>
{
    private readonly InventoryDbContext _dbContext;
    private readonly ILogger<ProductVariantCreatedHandler> _logger;

    public ProductVariantCreatedHandler(
        InventoryDbContext dbContext,
        ILogger<ProductVariantCreatedHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task Handle(ProductVariantCreated notification, CancellationToken cancellationToken)
    {
        var exists = await _dbContext.ProductStocks
            .AnyAsync(stock => stock.ProductVariantId == notification.ProductVariantId, cancellationToken);

        if (exists)
        {
            // Idempotent: event có thể được gửi lại nhiều lần.
            return;
        }

        _dbContext.ProductStocks.Add(ProductStock.Create(
            notification.ProductVariantId,
            notification.InitialStockQuantity));

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Khởi tạo tồn kho {Quantity} cho phiên bản sản phẩm {VariantId} (SKU {Sku})",
            notification.InitialStockQuantity,
            notification.ProductVariantId,
            notification.Sku);
    }
}
