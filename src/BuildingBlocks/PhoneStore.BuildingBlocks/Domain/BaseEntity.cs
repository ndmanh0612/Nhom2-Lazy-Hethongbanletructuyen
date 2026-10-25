namespace PhoneStore.BuildingBlocks.Domain;

public abstract class BaseEntity<TId>
{
    public TId Id { get; protected set; } = default!;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public void Touch()
    {
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
