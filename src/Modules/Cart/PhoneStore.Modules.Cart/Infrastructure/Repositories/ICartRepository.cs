using PhoneStore.Modules.Cart.Domain.Entities;
using CartEntity = PhoneStore.Modules.Cart.Domain.Entities.Cart;

namespace PhoneStore.Modules.Cart.Infrastructure.Repositories;

public interface ICartRepository
{
    Task<CartEntity?> GetActiveCartByUserAsync(long userId, CancellationToken cancellationToken = default);
    Task<CartEntity?> GetByIdAsync(long cartId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CartEntity>> GetByUserAsync(long userId, CancellationToken cancellationToken = default);
    Task<bool> ExistsActiveCartAsync(long userId, CancellationToken cancellationToken = default);
    Task AddAsync(CartEntity cart, CancellationToken cancellationToken = default);
    void Remove(CartEntity cart);
}
