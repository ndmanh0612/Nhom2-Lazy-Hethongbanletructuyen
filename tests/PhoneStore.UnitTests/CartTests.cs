using FluentAssertions;
using PhoneStore.BuildingBlocks.Exceptions;
using PhoneStore.Modules.Cart.Domain.Entities;
using PhoneStore.Modules.Cart.Domain.Enums;
using Xunit;

namespace PhoneStore.UnitTests;

public class CartTests
{
    [Fact]
    public void Create_ShouldInitializeActiveCartWithEmptyItems()
    {
        var cart = Cart.Create(42);

        cart.UserId.Should().Be(42);
        cart.Status.Should().Be(CartStatus.ACTIVE);
        cart.Items.Should().BeEmpty();
        cart.TotalQuantity.Should().Be(0);
        cart.Subtotal.Should().Be(0);
    }

    [Fact]
    public void Create_ShouldRejectNonPositiveUserId()
    {
        var action = () => Cart.Create(0);

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddItem_ShouldCreateItemWithSubtotal()
    {
        var cart = Cart.Create(1);

        var cartItem = cart.AddItem(100, "iPhone 17 Pro", "IP17P-256-BLK", 25_000_000, 2);

        cartItem.ProductVariantId.Should().Be(100);
        cartItem.Quantity.Should().Be(2);
        cartItem.Subtotal.Should().Be(50_000_000);
        cart.Items.Should().ContainSingle();
        cart.TotalQuantity.Should().Be(2);
        cart.Subtotal.Should().Be(50_000_000);
    }

    [Fact]
    public void AddItem_WithExistingVariant_ShouldIncreaseQuantityInsteadOfDuplicating()
    {
        var cart = Cart.Create(1);
        cart.AddItem(100, "iPhone 17 Pro", "IP17P-256-BLK", 25_000_000, 1);

        var cartItem = cart.AddItem(100, "iPhone 17 Pro", "IP17P-256-BLK", 25_000_000, 2);

        cart.Items.Should().ContainSingle();
        cartItem.Quantity.Should().Be(3);
    }

    [Fact]
    public void AddItem_ShouldRejectQuantityAboveMaximum()
    {
        var cart = Cart.Create(1);

        var action = () => cart.AddItem(100, "iPhone 17 Pro", "IP17P-256-BLK", 25_000_000, CartItem.MaxQuantityPerItem + 1);

        // N2LHTBLTT-39: domain ném ValidationException (AppException) để
        // AppExceptionMiddleware map về 400 VALIDATION_ERROR thay vì 500.
        action.Should().Throw<ValidationException>()
            .Which.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
    }

    [Fact]
    public void AddItem_ExceedingMaximumViaMerge_ShouldBeRejected()
    {
        var cart = Cart.Create(1);
        cart.AddItem(100, "iPhone 17 Pro", "IP17P-256-BLK", 25_000_000, 6);

        var action = () => cart.AddItem(100, "iPhone 17 Pro", "IP17P-256-BLK", 25_000_000, 6);

        action.Should().Throw<ValidationException>()
            .Which.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);

        // Giỏ giữ nguyên số lượng cũ.
        cart.Items.Should().ContainSingle();
        cart.TotalQuantity.Should().Be(6);
    }

    [Fact]
    public void UpdateItemQuantity_ShouldReplaceQuantity()
    {
        var cart = Cart.Create(1);
        var cartItem = cart.AddItem(100, "iPhone 17 Pro", "IP17P-256-BLK", 25_000_000, 2);

        cart.UpdateItemQuantity(cartItem, 5);

        cartItem.Quantity.Should().Be(5);
        cart.Subtotal.Should().Be(125_000_000);
    }

    [Fact]
    public void UpdateItemQuantity_ShouldRejectZeroQuantity()
    {
        var cart = Cart.Create(1);
        var cartItem = cart.AddItem(100, "iPhone 17 Pro", "IP17P-256-BLK", 25_000_000, 2);

        var action = () => cart.UpdateItemQuantity(cartItem, 0);

        action.Should().Throw<ValidationException>()
            .Which.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
    }

    [Fact]
    public void RemoveItem_ShouldRemoveItemFromCart()
    {
        var cart = Cart.Create(1);
        var cartItem = cart.AddItem(100, "iPhone 17 Pro", "IP17P-256-BLK", 25_000_000, 1);

        cart.RemoveItem(cartItem);

        cart.Items.Should().BeEmpty();
        cart.Subtotal.Should().Be(0);
    }

    [Fact]
    public void MarkAsConverted_ShouldChangeStatus()
    {
        var cart = Cart.Create(1);

        cart.MarkAsConverted();

        cart.Status.Should().Be(CartStatus.CONVERTED);
    }

    [Fact]
    public void AddItem_OnConvertedCart_ShouldBeRejected()
    {
        var cart = Cart.Create(1);
        cart.MarkAsConverted();

        var action = () => cart.AddItem(100, "iPhone 17 Pro", "IP17P-256-BLK", 25_000_000, 1);

        action.Should().Throw<ValidationException>()
            .Which.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
    }
}
