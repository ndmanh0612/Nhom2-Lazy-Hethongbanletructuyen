using PhoneStore.BuildingBlocks.Contracts.Order;
using PhoneStore.BuildingBlocks.Domain;
using PhoneStore.BuildingBlocks.Exceptions;
using PhoneStore.Modules.Order.Domain.Enums;

namespace PhoneStore.Modules.Order.Domain.Entities;

public class Order : AggregateRoot<long>
{
    public const long FreeShippingThreshold = 10_000_000;
    public const long StandardShippingFee = 30_000;

    public string OrderNumber { get; private set; } = string.Empty;
    public long CustomerId { get; private set; }
    public OrderStatus Status { get; private set; } = OrderStatus.PENDING;
    public string PaymentMethod { get; private set; } = "COD";
    public long Subtotal { get; private set; }
    public long ShippingFee { get; private set; }
    public long TotalAmount { get; private set; }
    public string ReceiverName { get; private set; } = string.Empty;
    public string ReceiverPhone { get; private set; } = string.Empty;
    public string ShippingAddress { get; private set; } = string.Empty;
    public string? Note { get; private set; }
    public string? CancelReason { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }
    public DateTimeOffset? DeliveredAt { get; private set; }

    private readonly List<OrderItem> _items = [];
    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

    private readonly List<OrderStatusHistory> _statusHistories = [];
    public IReadOnlyCollection<OrderStatusHistory> StatusHistories => _statusHistories.AsReadOnly();

    protected Order() { }

    public static Order Create(
        string orderNumber,
        long customerId,
        string receiverName,
        string receiverPhone,
        string shippingAddress,
        string? note,
        IReadOnlyList<OrderItemDraft> itemDrafts)
    {
        if (customerId <= 0)
        {
            throw new ArgumentException("Id khách hàng không hợp lệ.", nameof(customerId));
        }

        if (itemDrafts.Count == 0)
        {
            throw new ArgumentException("Đơn hàng phải có ít nhất một sản phẩm.", nameof(itemDrafts));
        }

        var order = new Order
        {
            OrderNumber = orderNumber.Trim(),
            CustomerId = customerId,
            Status = OrderStatus.PENDING,
            PaymentMethod = "COD",
            ReceiverName = receiverName.Trim(),
            ReceiverPhone = receiverPhone.Trim(),
            ShippingAddress = shippingAddress.Trim(),
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        foreach (var itemDraft in itemDrafts)
        {
            order._items.Add(OrderItem.Create(order, itemDraft.ProductVariantId, itemDraft.ProductName, itemDraft.Sku, itemDraft.UnitPrice, itemDraft.Quantity));
        }

        order.RecalculateTotals();
        order._statusHistories.Add(OrderStatusHistory.Create(order, null, OrderStatus.PENDING, "Đơn hàng được tạo từ giỏ hàng."));
        return order;
    }

    public void ChangeStatus(OrderStatus newStatus, string? note = null)
    {
        if (Status == newStatus)
        {
            return;
        }

        if (Status == OrderStatus.DELIVERED || Status == OrderStatus.CANCELLED)
        {
            throw new ConflictException($"Không thể chuyển trạng thái từ {Status} sang {newStatus}.", "INVALID_ORDER_STATUS");
        }

        var previousStatus = Status;
        Status = newStatus;
        _statusHistories.Add(OrderStatusHistory.Create(this, previousStatus, newStatus, note));

        if (newStatus == OrderStatus.DELIVERED)
        {
            DeliveredAt = DateTimeOffset.UtcNow;
        }

        Touch();
    }

    public void Cancel(string reason)
    {
        if (Status != OrderStatus.PENDING && Status != OrderStatus.CONFIRMED)
        {
            throw new ConflictException($"Đơn hàng đang ở trạng thái {Status} không thể hủy.", "INVALID_ORDER_STATUS");
        }

        var previousStatus = Status;
        Status = OrderStatus.CANCELLED;
        CancelReason = reason.Trim();
        CancelledAt = DateTimeOffset.UtcNow;
        _statusHistories.Add(OrderStatusHistory.Create(this, previousStatus, OrderStatus.CANCELLED, CancelReason));
        Touch();

        // Phát event để Inventory hoàn trả tồn kho (FR-09).
        // Event được dispatch sau khi SaveChanges bởi OrderService.
        AddDomainEvent(new OrderCancelled(
            Id,
            CustomerId,
            OrderNumber,
            _items.Select(item => new OrderCancelledItem(item.ProductVariantId, item.Quantity)).ToList()));
    }

    public void RecalculateTotals()
    {
        Subtotal = _items.Sum(item => item.Subtotal);
        ShippingFee = Subtotal >= FreeShippingThreshold ? 0 : StandardShippingFee;
        TotalAmount = Subtotal + ShippingFee;
    }
}
