using PhoneStore.BuildingBlocks.Contracts.Cart;
using PhoneStore.Modules.Cart.Infrastructure.Repositories;
using CartEntity = PhoneStore.Modules.Cart.Domain.Entities.Cart;

namespace PhoneStore.Modules.Cart.Application.Services;

/// <summary>
/// Cart module là nơi implement ICartContract để Order không cần reference trực tiếp CartDbContext.
/// </summary>
public class CartContract : ICartContract
{
    private readonly ICartRepository _cartRepository;

    public CartContract(ICartRepository cartRepository)
    {
        _cartRepository = cartRepository;
    }

    public async Task<CartSnapshot?> GetActiveCartAsync(long userId, CancellationToken cancellationToken = default)
    {
        var cart = await _cartRepository.GetActiveCartByUserAsync(userId, cancellationToken);
        if (cart is null)
        {
            return null;
        }

        return ToSnapshot(cart);
    }

    public async Task MarkAsConvertedAsync(long cartId, CancellationToken cancellationToken = default)
    {
        var cart = await _cartRepository.GetByIdAsync(cartId, cancellationToken);
        if (cart is null)
        {
            return;
        }

        cart.MarkAsConverted();
        await _cartRepository.SaveChangesAsync(cancellationToken);
    }

    private static CartSnapshot ToSnapshot(CartEntity cart)
    {
        var items = cart.Items
            .OrderBy(item => item.Id)
            .Select(item => new CartSnapshotItem(
                item.Id,
                item.ProductVariantId,
                item.ProductName,
                item.Sku,
                item.UnitPrice,
                item.Quantity))
            .ToList();

        return new CartSnapshot(cart.Id, cart.UserId, items);
    }
}
