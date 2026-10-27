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

    /// <summary>
    /// N2LHTBLTT-24: Id giỏ hàng nguồn của đơn. Dùng để chống tạo trùng đơn khi
    /// client retry request checkout.
    /// </summary>
    public long? CartId { get; private set; }

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

    // FR-10 (N2LHTBLTT-27): Thông tin vận chuyển.
    public string? Carrier { get; private set; }
    public string? TrackingNumber { get; private set; }

    // FR-11 (N2LHTBLTT-28): Tiền COD đã thu.
    public long? CollectedAmount { get; private set; }
    public DateTimeOffset? CollectedAt { get; private set; }

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
        IReadOnlyList<OrderItemDraft> itemDrafts,
        long? cartId = null)
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
            CartId = cartId,
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

    /// <summary>
    /// FR-10 (N2LHTBLTT-27): Xác nhận đơn — chuyển PENDING → CONFIRMED.
    /// </summary>
    public void Confirm(string? note)
    {
        if (Status != OrderStatus.PENDING)
        {
            throw new ConflictException(
                $"Chỉ đơn hàng ở trạng thái PENDING mới có thể xác nhận. Hiện tại: {Status}.",
                "INVALID_ORDER_STATUS");
        }

        ChangeStatus(OrderStatus.CONFIRMED, note ?? "Đơn hàng đã được xác nhận.");
    }

    /// <summary>
    /// FR-10 (N2LHTBLTT-27): Bàn giao vận chuyển — chuyển CONFIRMED → SHIPPING.
    /// </summary>
    public void Ship(string? carrier, string? trackingNumber, string? note)
    {
        if (Status != OrderStatus.CONFIRMED)
        {
            throw new ConflictException(
                $"Chỉ đơn hàng ở trạng thái CONFIRMED mới có thể giao hàng. Hiện tại: {Status}.",
                "INVALID_ORDER_STATUS");
        }

        Carrier = string.IsNullOrWhiteSpace(carrier) ? null : carrier.Trim();
        TrackingNumber = string.IsNullOrWhiteSpace(trackingNumber) ? null : trackingNumber.Trim();

        ChangeStatus(OrderStatus.SHIPPING, note ?? "Đơn hàng đã được bàn giao cho đơn vị vận chuyển.");
    }

    /// <summary>
    /// FR-11 (N2LHTBLTT-28): Giao hàng thành công và thu tiền COD.
    /// Chuyển SHIPPING → DELIVERED.
    /// </summary>
    public void Deliver(long collectedAmount, string? note)
    {
        if (Status != OrderStatus.SHIPPING)
        {
            throw new ConflictException(
                $"Chỉ đơn hàng ở trạng thái SHIPPING mới có thể giao thành công. Hiện tại: {Status}.",
                "INVALID_ORDER_STATUS");
        }

        if (collectedAmount < TotalAmount)
        {
            throw new ValidationException(
                "collectedAmount",
                $"Số tiền COD thu được ({collectedAmount:N0}) phải bằng hoặc lớn hơn tổng tiền đơn hàng ({TotalAmount:N0}).");
        }

        CollectedAmount = collectedAmount;
        CollectedAt = DateTimeOffset.UtcNow;

        ChangeStatus(OrderStatus.DELIVERED, note ?? "Giao hàng thành công, đã thu tiền COD.");
    }

    /// <summary>
    /// FR-11 (N2LHTBLTT-28): Giao hàng thất bại.
    /// Đơn chuyển sang DELIVERY_FAILED, hoặc bị huỷ nếu không giao lại được.
    /// </summary>
    public void MarkDeliveryFailed(string reason, bool cancelOrder)
    {
        if (Status != OrderStatus.SHIPPING)
        {
            throw new ConflictException(
                $"Chỉ đơn hàng ở trạng thái SHIPPING mới đánh dấu giao thất bại. Hiện tại: {Status}.",
                "INVALID_ORDER_STATUS");
        }

        if (cancelOrder)
        {
            // Hủy đơn sau khi giao thất bại: phải hoàn tồn kho.
            var previousStatus = Status;
            Status = OrderStatus.CANCELLED;
            CancelReason = reason.Trim();
            CancelledAt = DateTimeOffset.UtcNow;
            _statusHistories.Add(OrderStatusHistory.Create(this, previousStatus, OrderStatus.CANCELLED, CancelReason));
            Touch();

            AddDomainEvent(new OrderCancelled(
                Id,
                CustomerId,
                OrderNumber,
                _items.Select(item => new OrderCancelledItem(item.ProductVariantId, item.Quantity)).ToList()));

            return;
        }

        ChangeStatus(OrderStatus.DELIVERY_FAILED, reason.Trim());
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
