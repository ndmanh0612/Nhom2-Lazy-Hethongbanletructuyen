using PhoneStore.BuildingBlocks.Domain;

namespace PhoneStore.BuildingBlocks.Application;

/// <summary>
/// Phát các domain event đã được gom nhóm trên aggregate.
/// Mặc định dispatch sau khi SaveChanges để đảm bảo dữ liệu đã được commit.
/// </summary>
public interface IDomainEventDispatcher
{
    Task DispatchAsync(
        IEnumerable<IDomainEvent> domainEvents,
        CancellationToken cancellationToken = default);
}
