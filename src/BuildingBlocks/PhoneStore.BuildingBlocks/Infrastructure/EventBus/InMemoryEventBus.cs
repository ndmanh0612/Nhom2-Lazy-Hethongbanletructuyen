using MediatR;
using Microsoft.Extensions.Logging;
using PhoneStore.BuildingBlocks.Application;
using PhoneStore.BuildingBlocks.Domain;

namespace PhoneStore.BuildingBlocks.Infrastructure.EventBus;

public class InMemoryEventBus : IEventBus
{
    private readonly IMediator _mediator;
    private readonly ILogger<InMemoryEventBus> _logger;

    public InMemoryEventBus(IMediator mediator, ILogger<InMemoryEventBus> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : IDomainEvent
    {
        _logger.LogInformation("Publishing domain event: {EventType} occurred at {OccurredOn}",
            typeof(TEvent).Name, @event.OccurredOn);

        if (@event is INotification notification)
        {
            await _mediator.Publish(notification, cancellationToken);
        }
    }
}
