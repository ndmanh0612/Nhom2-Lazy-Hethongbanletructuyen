using PhoneStore.Modules.Cart.Domain.Enums;
using PhoneStore.Modules.Cart.Infrastructure.Repositories;
using Xunit;
using CartEntity = PhoneStore.Modules.Cart.Domain.Entities.Cart;

namespace PhoneStore.UnitTests;

/// <summary>
/// Repository giả lập cho Cart, dùng trong unit test service.
/// </summary>
internal sealed class FakeCartRepository : ICartRepository
{
    private readonly List<CartEntity> _carts = [];

    public List<CartEntity> SavedCarts => _carts;

    public Task<CartEntity?> GetActiveCartByUserAsync(long userId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_carts.FirstOrDefault(cart => cart.UserId == userId && cart.Status == CartStatus.ACTIVE));
    }

    public Task<CartEntity?> GetByIdAsync(long cartId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_carts.FirstOrDefault(cart => cart.Id == cartId));
    }

    public Task<IReadOnlyList<CartEntity>> GetByUserAsync(long userId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<CartEntity>>(_carts.Where(cart => cart.UserId == userId).ToList());
    }

    public Task<bool> ExistsActiveCartAsync(long userId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_carts.Any(cart => cart.UserId == userId && cart.Status == CartStatus.ACTIVE));
    }

    public Task AddAsync(CartEntity cart, CancellationToken cancellationToken = default)
    {
        if (!_carts.Contains(cart))
        {
            _carts.Add(cart);
        }

        return Task.CompletedTask;
    }

    public void Remove(CartEntity cart)
    {
        _carts.Remove(cart);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(1);
    }
}
