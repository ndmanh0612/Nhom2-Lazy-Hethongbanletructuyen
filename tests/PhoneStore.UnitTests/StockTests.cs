using FluentAssertions;
using PhoneStore.Modules.Inventory.Domain.Entities;
using Xunit;

namespace PhoneStore.UnitTests;

public class StockTests
{
    [Fact]
    public void Create_ValidParameters_ShouldInitializeStock()
    {
        var stock = Stock.Create(1, "IPHONE-15-PRO-256", 10);

        stock.ProductVariantId.Should().Be(1);
        stock.Sku.Should().Be("IPHONE-15-PRO-256");
        stock.QuantityOnHand.Should().Be(10);
        stock.QuantityReserved.Should().Be(0);
        stock.AvailableQuantity.Should().Be(10);
    }

    [Fact]
    public void AddStock_ValidQuantity_ShouldIncreaseQuantityOnHandAndAddMovement()
    {
        var stock = Stock.Create(1, "IPHONE-15-PRO-256", 10);

        stock.AddStock(5, "Nhập hàng từ nhà máy");

        stock.QuantityOnHand.Should().Be(15);
        stock.Movements.Should().HaveCount(1);
        stock.Movements.First().Type.Should().Be(MovementType.INBOUND);
        stock.Movements.First().Quantity.Should().Be(5);
        stock.Movements.First().QuantityAfter.Should().Be(15);
    }

    [Fact]
    public void RemoveStock_ValidQuantity_ShouldDecreaseQuantityOnHandAndAddMovement()
    {
        var stock = Stock.Create(1, "IPHONE-15-PRO-256", 10);

        stock.RemoveStock(4, "Bán cho khách lẻ");

        stock.QuantityOnHand.Should().Be(6);
        stock.Movements.Should().HaveCount(1);
        stock.Movements.First().Type.Should().Be(MovementType.OUTBOUND);
        stock.Movements.First().Quantity.Should().Be(-4);
        stock.Movements.First().QuantityAfter.Should().Be(6);
    }

    [Fact]
    public void Reserve_ValidQuantity_ShouldIncreaseQuantityReserved()
    {
        var stock = Stock.Create(1, "IPHONE-15-PRO-256", 10);

        stock.Reserve(3);

        stock.QuantityReserved.Should().Be(3);
        stock.AvailableQuantity.Should().Be(7);
    }

    [Fact]
    public void Reserve_MoreThanAvailable_ShouldThrowInvalidOperationException()
    {
        var stock = Stock.Create(1, "IPHONE-15-PRO-256", 5);

        var act = () => stock.Reserve(6);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ReleaseReservation_ValidQuantity_ShouldDecreaseQuantityReserved()
    {
        var stock = Stock.Create(1, "IPHONE-15-PRO-256", 10);
        stock.Reserve(4);

        stock.ReleaseReservation(2);

        stock.QuantityReserved.Should().Be(2);
        stock.AvailableQuantity.Should().Be(8);
    }

    [Fact]
    public void Adjust_ValidReason_ShouldUpdateQuantityOnHand()
    {
        var stock = Stock.Create(1, "IPHONE-15-PRO-256", 10);

        stock.Adjust(-2, "Kiểm kê kho thất thoát 2 máy");

        stock.QuantityOnHand.Should().Be(8);
        stock.Movements.Should().HaveCount(1);
        stock.Movements.First().Type.Should().Be(MovementType.ADJUSTMENT_OUT);
    }
}
