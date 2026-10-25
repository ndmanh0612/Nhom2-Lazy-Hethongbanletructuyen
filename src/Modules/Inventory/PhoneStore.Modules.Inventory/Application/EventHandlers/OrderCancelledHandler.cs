using MediatR;
using Microsoft.Extensions.Logging;
using PhoneStore.BuildingBlocks.Contracts.Inventory;
using PhoneStore.BuildingBlocks.Contracts.Order;

namespace PhoneStore.Modules.Inventory.Application.EventHandlers;

/// <summary>
/// Nhận event OrderCancelled từ module Order và hoàn trả tồn kho (FR-09).
/// </summary>
public class OrderCancelledHandler : INotificationHandler<OrderCancelled>
{
    private readonly IInventoryContract _inventoryContract;
    private readonly ILogger<OrderCancelledHandler> _logger;

    public OrderCancelledHandler(IInventoryContract inventoryContract, ILogger<OrderCancelledHandler> logger)
    {
        _inventoryContract = inventoryContract;
        _logger = logger;
    }

    public async Task Handle(OrderCancelled notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Hoàn tồn kho cho đơn hàng {OrderNumber} (OrderId={OrderId})",
            notification.OrderNumber,
            notification.OrderId);

        var items = notification.Items
            .Select(item => new StockChangeItem(item.ProductVariantId, item.Quantity))
            .ToList();

        await _inventoryContract.RestoreStockAsync(items, cancellationToken);
    }
}
