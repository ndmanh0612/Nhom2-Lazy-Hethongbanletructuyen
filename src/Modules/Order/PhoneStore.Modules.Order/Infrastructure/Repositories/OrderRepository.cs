using Microsoft.EntityFrameworkCore;
using PhoneStore.Modules.Order.Domain.Entities;
using PhoneStore.Modules.Order.Infrastructure.Persistence;
using OrderEntity = PhoneStore.Modules.Order.Domain.Entities.Order;

namespace PhoneStore.Modules.Order.Infrastructure.Repositories;

public class OrderRepository : IOrderRepository
{
    private readonly OrderDbContext _dbContext;

    public OrderRepository(OrderDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<OrderEntity?> GetByIdAsync(long orderId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Orders
            .Include(order => order.Items)
            .Include(order => order.StatusHistories)
            .FirstOrDefaultAsync(order => order.Id == orderId, cancellationToken);
    }

    public Task<OrderEntity?> GetByOrderNumberAsync(string orderNumber, CancellationToken cancellationToken = default)
    {
        var normalizedOrderNumber = orderNumber.Trim().ToUpperInvariant();
        return _dbContext.Orders
            .Include(order => order.Items)
            .Include(order => order.StatusHistories)
            .FirstOrDefaultAsync(order => order.OrderNumber == normalizedOrderNumber, cancellationToken);
    }

    public async Task<IReadOnlyList<OrderEntity>> GetByCustomerAsync(long customerId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Orders
            .AsNoTracking()
            .Include(order => order.Items)
            .Where(order => order.CustomerId == customerId)
            .OrderByDescending(order => order.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> ExistsOrderNumberAsync(string orderNumber, CancellationToken cancellationToken = default)
    {
        var normalizedOrderNumber = orderNumber.Trim().ToUpperInvariant();
        return _dbContext.Orders.AnyAsync(order => order.OrderNumber == normalizedOrderNumber, cancellationToken);
    }

    public async Task AddAsync(OrderEntity order, CancellationToken cancellationToken = default)
    {
        await _dbContext.Orders.AddAsync(order, cancellationToken);
    }

    public void Remove(OrderEntity order)
    {
        _dbContext.Orders.Remove(order);
    }
}
