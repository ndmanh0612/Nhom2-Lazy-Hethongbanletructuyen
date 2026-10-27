using FluentAssertions;
using Xunit;
using PhoneStore.Modules.Inventory.Domain.Entities;

namespace PhoneStore.UnitTests;

/// <summary>
/// FR-14 (N2LHTBLTT-21) / N2LHTBLTT-22: Kiểm thử logic tồn kho.
///
/// Bao gồm cả trường hợp chống bán quá số lượng tồn (oversell).
/// </summary>
public class ProductStockTests
{
    [Fact]
    public void New_stock_starts_with_given_quantity()
    {
        var stock = ProductStock.Create(1, 10);

        stock.ProductVariantId.Should().Be(1);
        stock.StockQuantity.Should().Be(10);
    }

    [Fact]
    public void New_stock_rejects_negative_quantity()
    {
        var action = () => ProductStock.Create(1, -1);

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Reserve_reduces_stock_quantity()
    {
        var stock = ProductStock.Create(1, 10);

        stock.Reserve(3);

        stock.StockQuantity.Should().Be(7);
    }

    [Fact]
    public void Reserve_more_than_available_throws_invalid_operation()
    {
        var stock = ProductStock.Create(1, 2);

        var action = () => stock.Reserve(3);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*không đủ*");
        stock.StockQuantity.Should().Be(2);
    }

    [Fact]
    public void Reserve_with_non_positive_quantity_throws_argument_exception()
    {
        var stock = ProductStock.Create(1, 10);

        var action = () => stock.Reserve(0);

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Restore_increases_stock_quantity()
    {
        var stock = ProductStock.Create(1, 10);
        stock.Reserve(4);

        stock.Restore(4);

        stock.StockQuantity.Should().Be(10);
    }

    [Fact]
    public void Import_increases_stock_quantity()
    {
        var stock = ProductStock.Create(1, 10);

        var applied = stock.Import(5);

        applied.Should().Be(5);
        stock.StockQuantity.Should().Be(15);
    }

    [Fact]
    public void Adjust_with_positive_delta_increases_stock()
    {
        var stock = ProductStock.Create(1, 10);

        var applied = stock.Adjust(7);

        applied.Should().Be(7);
        stock.StockQuantity.Should().Be(17);
    }

    [Fact]
    public void Adjust_with_negative_delta_decreases_stock()
    {
        var stock = ProductStock.Create(1, 10);

        stock.Adjust(-3);

        stock.StockQuantity.Should().Be(7);
    }

    [Fact]
    public void Adjust_below_zero_throws_invalid_operation()
    {
        var stock = ProductStock.Create(1, 2);

        var action = () => stock.Adjust(-5);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*không đủ*");
        stock.StockQuantity.Should().Be(2);
    }

    [Fact]
    public void Adjust_with_zero_delta_throws_argument_exception()
    {
        var stock = ProductStock.Create(1, 2);

        var action = () => stock.Adjust(0);

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Stock_movement_records_quantity_before_and_after()
    {
        var movement = StockMovement.Create(
            productVariantId: 1,
            quantity: -3,
            quantityBefore: 10,
            quantityAfter: 7,
            type: StockMovementType.RESERVATION,
            orderId: 42);

        movement.ProductVariantId.Should().Be(1);
        movement.Quantity.Should().Be(-3);
        movement.QuantityBefore.Should().Be(10);
        movement.QuantityAfter.Should().Be(7);
        movement.Type.Should().Be(StockMovementType.RESERVATION);
        movement.OrderId.Should().Be(42);
    }

    [Fact]
    public void Stock_movement_rejects_zero_quantity()
    {
        var action = () => StockMovement.Create(1, 0, 10, 10, StockMovementType.IMPORT);

        action.Should().Throw<ArgumentException>();
    }
}
