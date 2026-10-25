using Microsoft.EntityFrameworkCore;
using PhoneStore.Modules.Cart.Domain.Entities;
using PhoneStore.Modules.Cart.Domain.Enums;
using PhoneStore.Modules.Cart.Infrastructure.Persistence;
using CartEntity = PhoneStore.Modules.Cart.Domain.Entities.Cart;

namespace PhoneStore.Modules.Cart.Infrastructure.Repositories;

public class CartRepository : ICartRepository
{
    private readonly CartDbContext _dbContext;

    public CartRepository(CartDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<CartEntity?> GetActiveCartByUserAsync(long userId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Carts
            .Include(cart => cart.Items)
            .FirstOrDefaultAsync(cart => cart.UserId == userId && cart.Status == CartStatus.ACTIVE, cancellationToken);
    }

    public Task<CartEntity?> GetByIdAsync(long cartId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Carts
            .Include(cart => cart.Items)
            .FirstOrDefaultAsync(cart => cart.Id == cartId, cancellationToken);
    }

    public async Task<IReadOnlyList<CartEntity>> GetByUserAsync(long userId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Carts
            .AsNoTracking()
            .Include(cart => cart.Items)
            .Where(cart => cart.UserId == userId)
            .OrderByDescending(cart => cart.UpdatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> ExistsActiveCartAsync(long userId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Carts.AnyAsync(cart => cart.UserId == userId && cart.Status == CartStatus.ACTIVE, cancellationToken);
    }

    public async Task AddAsync(CartEntity cart, CancellationToken cancellationToken = default)
    {
        await _dbContext.Carts.AddAsync(cart, cancellationToken);
    }

    public void Remove(CartEntity cart)
    {
        _dbContext.Carts.Remove(cart);
    }
}
