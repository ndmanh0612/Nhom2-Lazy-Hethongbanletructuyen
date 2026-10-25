using Microsoft.EntityFrameworkCore;
using PhoneStore.BuildingBlocks.Application;
using PhoneStore.Modules.Order.Application.DTOs;
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

    /// <summary>
    /// FR-08: Danh sách đơn hàng có lọc theo trạng thái / khoảng thời gian và phân trang.
    /// </summary>
    public async Task<PagedResult<OrderEntity>> GetByCustomerPagedAsync(
        long customerId,
        OrderFilterQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 100 ? 20 : query.PageSize;

        var baseQuery = _dbContext.Orders
            .AsNoTracking()
            .Where(order => order.CustomerId == customerId);

        if (query.Status.HasValue)
        {
            baseQuery = baseQuery.Where(order => order.Status == query.Status.Value);
        }

        if (query.FromDate.HasValue)
        {
            var fromDate = query.FromDate.Value;
            baseQuery = baseQuery.Where(order => order.CreatedAt >= fromDate);
        }

        if (query.ToDate.HasValue)
        {
            var toDate = query.ToDate.Value;
            baseQuery = baseQuery.Where(order => order.CreatedAt <= toDate);
        }

        var totalCount = await baseQuery.LongCountAsync(cancellationToken);

        var orders = await baseQuery
            .OrderByDescending(order => order.CreatedAt)
            .ThenByDescending(order => order.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(order => order.Items)
            .ToListAsync(cancellationToken);

        return PagedResult<OrderEntity>.Create(orders, page, pageSize, totalCount);
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

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
