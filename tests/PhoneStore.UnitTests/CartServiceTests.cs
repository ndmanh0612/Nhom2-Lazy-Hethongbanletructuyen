using FluentAssertions;
using PhoneStore.BuildingBlocks.Contracts.Catalog;
using PhoneStore.BuildingBlocks.Exceptions;
using PhoneStore.Modules.Cart.Application.DTOs;
using PhoneStore.Modules.Cart.Application.Services;
using PhoneStore.Modules.Cart.Domain.Enums;
using PhoneStore.Modules.Cart.Infrastructure.Repositories;
using PhoneStore.Modules.Cart.Infrastructure.Persistence;
using Xunit;
using CartEntity = PhoneStore.Modules.Cart.Domain.Entities.Cart;

namespace PhoneStore.UnitTests;

/// <summary>
/// Test CartService với repository giả lập, không cần database.
/// </summary>
public class CartServiceTests
{
    private const long UserId = 42;

    [Fact]
    public async Task AddItemAsync_ShouldUseServerSidePrice_NotClientInput()
    {
        var repository = new FakeCartRepository();
        var pricing = new FakePricingContract(new ProductVariantPricing(
            100, 1, "iPhone 17 Pro", "IP17P-256-BLK", 25_000_000, 10, true));

        var service = new CartService(repository, pricing);

        var result = await service.AddItemAsync(UserId, new AddToCartRequest(100, 2));

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].UnitPrice.Should().Be(25_000_000);
        result.Value.Items[0].ProductName.Should().Be("iPhone 17 Pro");
        result.Value.Subtotal.Should().Be(50_000_000);
    }

    [Fact]
    public async Task AddItemAsync_WhenVariantNotFound_ShouldThrowNotFound()
    {
        var repository = new FakeCartRepository();
        var pricing = new FakePricingContract(null);

        var service = new CartService(repository, pricing);

        var action = () => service.AddItemAsync(UserId, new AddToCartRequest(999, 1));

        await action.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetCartAsync_ShouldCreateCartWhenNoneExists()
    {
        var repository = new FakeCartRepository();
        var pricing = new FakePricingContract(null);

        var service = new CartService(repository, pricing);

        var result = await service.GetCartAsync(UserId);

        result.IsSuccess.Should().BeTrue();
        result.Value.UserId.Should().Be(UserId);
        result.Value.Status.Should().Be(CartStatus.ACTIVE.ToString());
        repository.SavedCarts.Should().ContainSingle();
    }

    private sealed class FakePricingContract(ProductVariantPricing? pricing) : IProductPricingContract
    {
        public Task<ProductVariantPricing?> GetVariantPricingAsync(long productVariantId, CancellationToken cancellationToken = default)
            => Task.FromResult(productVariantId == 100 ? pricing : null);
    }
}
