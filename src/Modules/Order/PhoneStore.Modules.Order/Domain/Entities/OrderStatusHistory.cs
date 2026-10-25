using PhoneStore.BuildingBlocks.Domain;
using PhoneStore.Modules.Order.Domain.Enums;

namespace PhoneStore.Modules.Order.Domain.Entities;

public class OrderStatusHistory : BaseEntity<long>
{
    public long OrderId { get; private set; }
    public Order Order { get; private set; } = null!;
    public OrderStatus? FromStatus { get; private set; }
    public OrderStatus ToStatus { get; private set; }
    public string? Note { get; private set; }
    public DateTimeOffset ChangedAt { get; private set; }

    protected OrderStatusHistory() { }

    public static OrderStatusHistory Create(Order order, OrderStatus? fromStatus, OrderStatus toStatus, string? note)
    {
        return new OrderStatusHistory
        {
            Order = order,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            ChangedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }
}
