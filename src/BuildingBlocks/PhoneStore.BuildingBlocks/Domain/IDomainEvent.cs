namespace PhoneStore.BuildingBlocks.Domain;

public interface IDomainEvent
{
    DateTimeOffset OccurredOn { get; }
}
