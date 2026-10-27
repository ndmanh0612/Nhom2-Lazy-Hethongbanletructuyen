using FluentAssertions;
using Xunit;
using PhoneStore.Modules.Order.Domain.Entities;
using PhoneStore.Modules.Order.Domain.Enums;

namespace PhoneStore.UnitTests;

/// <summary>
/// FR-10 (N2LHTBLTT-27) / FR-11 (N2LHTBLTT-28): Kiểm thử chuyển trạng thái đơn hàng.
///
/// Các luồng nghiệp vụ được kiểm thử ở tầng domain vì đây là nơi chứa
/// quy tắc nghiệp vụ — không phụ thuộc database hay HTTP.
/// </summary>
public class OrderStatusTransitionTests
{
    private static Order CreateOrder()
    {
        return Order.Create(
            "ORD-TEST-0001",
            customerId: 1,
            receiverName: "Nguyễn Văn A",
            receiverPhone: "0912345678",
            shippingAddress: "123 Nguyễn Huệ, Hà Nội",
            note: null,
            itemDrafts:
            [
                new OrderItemDraft(10, "iPhone 15", "IP15-BLACK-128", 5_000_000, 1)
            ]);
    }

    [Fact]
    public void New_order_starts_in_pending_status_with_calculated_totals()
    {
        var order = CreateOrder();

        order.Status.Should().Be(OrderStatus.PENDING);
        order.Subtotal.Should().Be(5_000_000);
        order.PaymentMethod.Should().Be("COD");
        order.Items.Should().HaveCount(1);
    }

    [Fact]
    public void Order_under_free_shipping_threshold_includes_shipping_fee()
    {
        var order = CreateOrder();

        order.ShippingFee.Should().Be(Order.StandardShippingFee);
        order.TotalAmount.Should().Be(order.Subtotal + Order.StandardShippingFee);
    }

    [Fact]
    public void Order_at_free_shipping_threshold_has_no_shipping_fee()
    {
        var order = Order.Create(
            "ORD-TEST-0002",
            customerId: 1,
            receiverName: "Nguyễn Văn A",
            receiverPhone: "0912345678",
            shippingAddress: "123 Nguyễn Huệ",
            note: null,
            itemDrafts:
            [
                new OrderItemDraft(10, "iPhone 15", "IP15-BLACK-128", Order.FreeShippingThreshold, 1)
            ]);

        order.ShippingFee.Should().Be(0);
        order.TotalAmount.Should().Be(Order.FreeShippingThreshold);
    }

    [Fact]
    public void Confirm_moves_order_from_pending_to_confirmed_and_records_history()
    {
        var order = CreateOrder();

        order.Confirm("Xác nhận đơn");

        order.Status.Should().Be(OrderStatus.CONFIRMED);
        order.StatusHistories.Should().HaveCount(2);
        order.StatusHistories.Last().ToStatus.Should().Be(OrderStatus.CONFIRMED);
        order.StatusHistories.Last().FromStatus.Should().Be(OrderStatus.PENDING);
    }

    [Fact]
    public void Confirm_on_non_pending_order_throws_conflict()
    {
        var order = CreateOrder();
        order.Confirm(null);

        var action = () => order.Confirm("Xác nhận lại");

        action.Should().Throw<PhoneStore.BuildingBlocks.Exceptions.ConflictException>()
            .WithMessage("*PENDING*");
    }

    [Fact]
    public void Ship_moves_order_from_confirmed_to_shipping_with_tracking_info()
    {
        var order = CreateOrder();
        order.Confirm(null);

        order.Ship("Giao Hàng Nhanh", "GHN123456", "Bàn giao 10h sáng");

        order.Status.Should().Be(OrderStatus.SHIPPING);
        order.Carrier.Should().Be("Giao Hàng Nhanh");
        order.TrackingNumber.Should().Be("GHN123456");
    }

    [Fact]
    public void Ship_on_pending_order_throws_conflict()
    {
        var order = CreateOrder();

        var action = () => order.Ship(null, null, null);

        action.Should().Throw<PhoneStore.BuildingBlocks.Exceptions.ConflictException>();
    }

    [Fact]
    public void Deliver_moves_order_to_delivered_and_records_collected_amount()
    {
        var order = CreateOrder();
        order.Confirm(null);
        order.Ship(null, null, null);

        order.Deliver(order.TotalAmount, "Đã thu tiền");

        order.Status.Should().Be(OrderStatus.DELIVERED);
        order.CollectedAmount.Should().Be(order.TotalAmount);
        order.CollectedAt.Should().NotBeNull();
        order.DeliveredAt.Should().NotBeNull();
    }

    [Fact]
    public void Deliver_with_amount_below_total_throws_validation_error()
    {
        var order = CreateOrder();
        order.Confirm(null);
        order.Ship(null, null, null);

        var action = () => order.Deliver(order.TotalAmount - 1, null);

        action.Should().Throw<PhoneStore.BuildingBlocks.Exceptions.ValidationException>();
    }

    [Fact]
    public void Failed_delivery_without_cancel_moves_order_to_delivery_failed()
    {
        var order = CreateOrder();
        order.Confirm(null);
        order.Ship(null, null, null);

        order.MarkDeliveryFailed("Khách không nhận hàng", cancelOrder: false);

        order.Status.Should().Be(OrderStatus.DELIVERY_FAILED);
        order.CancelledAt.Should().BeNull();
    }

    [Fact]
    public void Failed_delivery_with_cancel_cancels_order_and_raises_domain_event()
    {
        var order = CreateOrder();
        order.Confirm(null);
        order.Ship(null, null, null);

        order.MarkDeliveryFailed("Khách từ chối nhận", cancelOrder: true);

        order.Status.Should().Be(OrderStatus.CANCELLED);
        order.CancelReason.Should().Be("Khách từ chối nhận");
        order.CancelledAt.Should().NotBeNull();

        // Event OrderCancelled được phát để Inventory hoàn trả tồn kho.
        var cancelledEvent = order.DomainEvents
            .OfType<PhoneStore.BuildingBlocks.Contracts.Order.OrderCancelled>()
            .Should().ContainSingle().Subject;

        cancelledEvent.OrderId.Should().Be(order.Id);
        cancelledEvent.Items.Should().ContainSingle();
        cancelledEvent.Items[0].ProductVariantId.Should().Be(10);
        cancelledEvent.Items[0].Quantity.Should().Be(1);
    }

    [Fact]
    public void Cancel_pending_order_is_allowed_and_raises_domain_event()
    {
        var order = CreateOrder();

        order.Cancel("Không còn nhu cầu");

        order.Status.Should().Be(OrderStatus.CANCELLED);
        order.CancelReason.Should().Be("Không còn nhu cầu");
        order.DomainEvents.Should().ContainSingle();
    }

    [Fact]
    public void Cancel_shipped_order_throws_conflict()
    {
        var order = CreateOrder();
        order.Confirm(null);
        order.Ship(null, null, null);

        var action = () => order.Cancel("Đổi ý");

        action.Should().Throw<PhoneStore.BuildingBlocks.Exceptions.ConflictException>();
    }

    [Fact]
    public void Cancelled_order_cannot_transition_to_another_status()
    {
        var order = CreateOrder();
        order.Cancel("Hủy");

        var action = () => order.ChangeStatus(OrderStatus.CONFIRMED, "Không hợp lệ");

        action.Should().Throw<PhoneStore.BuildingBlocks.Exceptions.ConflictException>();
    }

    [Fact]
    public void Delivered_order_cannot_transition_to_another_status()
    {
        var order = CreateOrder();
        order.Confirm(null);
        order.Ship(null, null, null);
        order.Deliver(order.TotalAmount, null);

        var action = () => order.ChangeStatus(OrderStatus.CANCELLED, "Không hợp lệ");

        action.Should().Throw<PhoneStore.BuildingBlocks.Exceptions.ConflictException>();
    }
}
