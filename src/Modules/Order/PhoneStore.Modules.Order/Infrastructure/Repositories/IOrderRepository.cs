using PhoneStore.Modules.Order.Domain.Enums;
using OrderEntity = PhoneStore.Modules.Order.Domain.Entities.Order;

namespace PhoneStore.Modules.Order.Infrastructure.Repositories;

public interface IOrderRepository
{
    Task<OrderEntity?> GetByIdAsync(long orderId, CancellationToken cancellationToken = default);
    Task<OrderEntity?> GetByOrderNumberAsync(string orderNumber, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OrderEntity>> GetByCustomerAsync(long customerId, CancellationToken cancellationToken = default);
    Task<bool> ExistsOrderNumberAsync(string orderNumber, CancellationToken cancellationToken = default);
    Task AddAsync(OrderEntity order, CancellationToken cancellationToken = default);
    void Remove(OrderEntity order);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
