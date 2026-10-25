using FluentAssertions;
using PhoneStore.BuildingBlocks.Contracts.Order;
using PhoneStore.BuildingBlocks.Exceptions;
using PhoneStore.Modules.Order.Domain.Entities;
using PhoneStore.Modules.Order.Domain.Enums;
using Xunit;

namespace PhoneStore.UnitTests;

public class OrderTests
{
    private static readonly OrderItemDraft[] SingleItemDraft =
    [
        new OrderItemDraft(100, "iPhone 17 Pro", "IP17P-256-BLK", 25_000_000, 2)
    ];

    private static readonly OrderItemDraft[] SmallItemDraft =
    [
        new OrderItemDraft(200, "Ốp lưng silicon", "CASE-SIL-BLK", 3_000_000, 1)
    ];

    [Fact]
    public void Create_ShouldInitializePendingOrderWithCalculatedTotals()
    {
        var order = Order.Create("ORD-20261025-0001", 42, "Mai Thanh Toàn", "0912345678", "Hà Nội", null, SingleItemDraft);

        order.OrderNumber.Should().Be("ORD-20261025-0001");
        order.CustomerId.Should().Be(42);
        order.Status.Should().Be(OrderStatus.PENDING);
        order.PaymentMethod.Should().Be("COD");
        order.Subtotal.Should().Be(50_000_000);
        order.TotalAmount.Should().Be(50_000_000);
        order.Items.Should().ContainSingle();
        order.StatusHistories.Should().ContainSingle();
    }

    [Fact]
    public void Create_ShouldRejectEmptyItemList()
    {
        var action = () => Order.Create("ORD-20261025-0001", 42, "Mai Thanh Toàn", "0912345678", "Hà Nội", null, []);

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_BelowFreeShippingThreshold_ShouldAddShippingFee()
    {
        var order = Order.Create("ORD-20261025-0001", 42, "Mai Thanh Toàn", "0912345678", "Hà Nội", null, SmallItemDraft);

        order.ShippingFee.Should().Be(Order.StandardShippingFee);
        order.TotalAmount.Should().Be(order.Subtotal + order.ShippingFee);
    }

    [Fact]
    public void Create_AboveFreeShippingThreshold_ShouldNotAddShippingFee()
    {
        var order = Order.Create("ORD-20261025-0001", 42, "Mai Thanh Toàn", "0912345678", "Hà Nội", null, SingleItemDraft);

        order.ShippingFee.Should().Be(0);
        order.TotalAmount.Should().Be(order.Subtotal);
    }

    [Fact]
    public void ChangeStatus_ShouldRecordStatusHistory()
    {
        var order = Order.Create("ORD-20261025-0001", 42, "Mai Thanh Toàn", "0912345678", "Hà Nội", null, SingleItemDraft);

        order.ChangeStatus(OrderStatus.CONFIRMED, "Đã xác nhận đơn");

        order.Status.Should().Be(OrderStatus.CONFIRMED);
        order.StatusHistories.Should().HaveCount(2);
        order.StatusHistories.Last().FromStatus.Should().Be(OrderStatus.PENDING);
        order.StatusHistories.Last().ToStatus.Should().Be(OrderStatus.CONFIRMED);
    }

    [Fact]
    public void ChangeStatus_ToDelivered_ShouldSetDeliveredAt()
    {
        var order = Order.Create("ORD-20261025-0001", 42, "Mai Thanh Toàn", "0912345678", "Hà Nội", null, SingleItemDraft);

        order.ChangeStatus(OrderStatus.CONFIRMED);
        order.ChangeStatus(OrderStatus.SHIPPING);
        order.ChangeStatus(OrderStatus.DELIVERED);

        order.Status.Should().Be(OrderStatus.DELIVERED);
        order.DeliveredAt.Should().NotBeNull();
    }

    [Fact]
    public void ChangeStatus_FromDelivered_ShouldBeRejected()
    {
        var order = Order.Create("ORD-20261025-0001", 42, "Mai Thanh Toàn", "0912345678", "Hà Nội", null, SingleItemDraft);
        order.ChangeStatus(OrderStatus.CONFIRMED);
        order.ChangeStatus(OrderStatus.SHIPPING);
        order.ChangeStatus(OrderStatus.DELIVERED);

        var action = () => order.ChangeStatus(OrderStatus.CANCELLED);

        action.Should().Throw<ConflictException>()
            .WithMessage("*DELIVERED*")
            .Which.Code.Should().Be("INVALID_ORDER_STATUS");
    }

    [Fact]
    public void Cancel_ShouldSetStatusReasonAndTimestamp()
    {
        var order = Order.Create("ORD-20261025-0001", 42, "Mai Thanh Toàn", "0912345678", "Hà Nội", null, SingleItemDraft);

        order.Cancel("Khách đổi ý định mua");

        order.Status.Should().Be(OrderStatus.CANCELLED);
        order.CancelReason.Should().Be("Khách đổi ý định mua");
        order.CancelledAt.Should().NotBeNull();
        order.StatusHistories.Last().ToStatus.Should().Be(OrderStatus.CANCELLED);
    }

    [Fact]
    public void Cancel_WhenShipping_ShouldBeRejected()
    {
        var order = Order.Create("ORD-20261025-0001", 42, "Mai Thanh Toàn", "0912345678", "Hà Nội", null, SingleItemDraft);
        order.ChangeStatus(OrderStatus.CONFIRMED);
        order.ChangeStatus(OrderStatus.SHIPPING);

        var action = () => order.Cancel("Khách đổi ý định mua");

        action.Should().Throw<ConflictException>()
            .WithMessage("*SHIPPING*")
            .Which.Code.Should().Be("INVALID_ORDER_STATUS");
    }

    [Fact]
    public void Cancel_ShouldRaiseOrderCancelledDomainEvent()
    {
        var order = Order.Create("ORD-20261025-0001", 42, "Mai Thanh Toàn", "0912345678", "Hà Nội", null, SingleItemDraft);

        order.Cancel("Khách đổi ý định mua");

        var domainEvent = order.DomainEvents.Should().ContainSingle().Subject;
        domainEvent.Should().BeOfType<OrderCancelled>();
        var orderCancelled = (OrderCancelled)domainEvent;
        orderCancelled.OrderId.Should().Be(order.Id);
        orderCancelled.CustomerId.Should().Be(42);
        orderCancelled.OrderNumber.Should().Be("ORD-20261025-0001");
        orderCancelled.Items.Should().ContainSingle();
        orderCancelled.Items[0].ProductVariantId.Should().Be(100);
        orderCancelled.Items[0].Quantity.Should().Be(2);
    }

    [Fact]
    public void ClearDomainEvents_ShouldRemoveRaisedEvents()
    {
        var order = Order.Create("ORD-20261025-0001", 42, "Mai Thanh Toàn", "0912345678", "Hà Nội", null, SingleItemDraft);
        order.Cancel("Khách đổi ý định mua");

        order.ClearDomainEvents();

        order.DomainEvents.Should().BeEmpty();
    }
}
