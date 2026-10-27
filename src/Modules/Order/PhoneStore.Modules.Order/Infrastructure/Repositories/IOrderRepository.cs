using Microsoft.EntityFrameworkCore;
using PhoneStore.BuildingBlocks.Application;
using PhoneStore.BuildingBlocks.Contracts.Catalog;
using PhoneStore.Modules.Order.Application.DTOs;
using PhoneStore.Modules.Order.Domain.Enums;
using PhoneStore.Modules.Order.Infrastructure.Persistence;
using OrderEntity = PhoneStore.Modules.Order.Domain.Entities.Order;

namespace PhoneStore.Modules.Order.Infrastructure.Repositories;

public interface IOrderRepository
{
    Task<OrderEntity?> GetByIdAsync(long orderId, CancellationToken cancellationToken = default);
    Task<OrderEntity?> GetByOrderNumberAsync(string orderNumber, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OrderEntity>> GetByCustomerAsync(long customerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// FR-08: Lấy danh sách đơn hàng của khách hàng kèm lọc và phân trang.
    /// </summary>
    Task<PagedResult<OrderEntity>> GetByCustomerPagedAsync(
        long customerId,
        OrderFilterQuery query,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsOrderNumberAsync(string orderNumber, CancellationToken cancellationToken = default);
    Task AddAsync(OrderEntity order, CancellationToken cancellationToken = default);
    void Remove(OrderEntity order);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
