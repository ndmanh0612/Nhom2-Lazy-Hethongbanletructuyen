using MediatR;
using PhoneStore.BuildingBlocks.Domain;

namespace PhoneStore.BuildingBlocks.Contracts.Order;

/// <summary>
/// Event phát ra khi một đơn hàng bị hủy. Inventory dùng event này để hoàn trả tồn kho.
/// </summary>
public record OrderCancelled(
    long OrderId,
    long CustomerId,
    string OrderNumber,
    IReadOnlyList<OrderCancelledItem> Items) : IDomainEvent, INotification
{
    public DateTimeOffset OccurredOn { get; init; } = DateTimeOffset.UtcNow;
}

public record OrderCancelledItem(long ProductVariantId, int Quantity);
