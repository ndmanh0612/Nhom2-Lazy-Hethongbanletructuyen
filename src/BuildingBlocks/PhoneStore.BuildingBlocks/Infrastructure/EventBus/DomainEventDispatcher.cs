using Microsoft.Extensions.Logging;
using PhoneStore.BuildingBlocks.Application;
using PhoneStore.BuildingBlocks.Domain;

namespace PhoneStore.BuildingBlocks.Infrastructure.EventBus;

public class DomainEventDispatcher : IDomainEventDispatcher
{
    private readonly IEventBus _eventBus;
    private readonly ILogger<DomainEventDispatcher> _logger;

    public DomainEventDispatcher(IEventBus eventBus, ILogger<DomainEventDispatcher> logger)
    {
        _eventBus = eventBus;
        _logger = logger;
    }

    public async Task DispatchAsync(
        IEnumerable<IDomainEvent> domainEvents,
        CancellationToken cancellationToken = default)
    {
        foreach (var domainEvent in domainEvents)
        {
            _logger.LogInformation(
                "Dispatching domain event {EventType} occurred at {OccurredOn}",
                domainEvent.GetType().Name,
                domainEvent.OccurredOn);

            await _eventBus.PublishAsync(domainEvent, cancellationToken);
        }
    }
}
