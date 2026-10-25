using PhoneStore.BuildingBlocks.Domain;

namespace PhoneStore.BuildingBlocks.Application;

public interface IEventBus
{
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : IDomainEvent;
}
