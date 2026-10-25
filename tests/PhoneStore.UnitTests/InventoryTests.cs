using FluentAssertions;
using PhoneStore.Modules.Inventory.Domain.Entities;
using Xunit;

namespace PhoneStore.UnitTests;

public class InventoryTests
{
    [Fact]
    public void Create_ShouldInitializeStock()
    {
        var stock = ProductStock.Create(100, 10);

        stock.ProductVariantId.Should().Be(100);
        stock.StockQuantity.Should().Be(10);
    }

    [Fact]
    public void Create_ShouldRejectNegativeStock()
    {
        var action = () => ProductStock.Create(100, -1);

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Reserve_ShouldDecreaseStock()
    {
        var stock = ProductStock.Create(100, 10);

        stock.Reserve(3);

        stock.StockQuantity.Should().Be(7);
    }

    [Fact]
    public void Reserve_WhenNotEnoughStock_ShouldBeRejected()
    {
        var stock = ProductStock.Create(100, 2);

        var action = () => stock.Reserve(3);

        action.Should().Throw<InvalidOperationException>();
        stock.StockQuantity.Should().Be(2);
    }

    [Fact]
    public void Restore_ShouldIncreaseStock()
    {
        var stock = ProductStock.Create(100, 10);
        stock.Reserve(4);

        stock.Restore(4);

        stock.StockQuantity.Should().Be(10);
    }

    [Fact]
    public void Restore_ShouldRejectNonPositiveQuantity()
    {
        var stock = ProductStock.Create(100, 10);

        var action = () => stock.Restore(0);

        action.Should().Throw<ArgumentException>();
    }
}
