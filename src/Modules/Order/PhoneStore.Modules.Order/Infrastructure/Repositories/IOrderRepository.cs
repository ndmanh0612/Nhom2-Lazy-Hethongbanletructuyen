using PhoneStore.BuildingBlocks.Application;
using PhoneStore.Modules.Order.Application.DTOs;
using PhoneStore.Modules.Order.Domain.Entities;
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

    /// <summary>
    /// N2LHTBLTT-24: Tìm đơn hàng đã tạo từ một giỏ hàng — dùng để chống tạo trùng.
    /// </summary>
    Task<OrderEntity?> GetByCartIdAsync(long cartId, CancellationToken cancellationToken = default);

    /// <summary>
    /// FR-11 (N2LHTBLTT-28): Truy vấn các đơn đã giao thành công (đã thu COD).
    /// </summary>
    IQueryable<OrderEntity> QueryDelivered();

    Task<bool> ExistsOrderNumberAsync(string orderNumber, CancellationToken cancellationToken = default);
    Task AddAsync(OrderEntity order, CancellationToken cancellationToken = default);
    void Remove(OrderEntity order);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
