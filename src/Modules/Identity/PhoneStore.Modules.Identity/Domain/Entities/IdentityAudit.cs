namespace PhoneStore.Modules.Identity.Domain.Entities;

// Application append-only history. No passwords, OTPs, tokens, or profile payloads.
public sealed class IdentityAudit
{
    public Guid Id { get; private set; }
    public long ActorId { get; private set; }
    public long TargetId { get; private set; }
    public string Action { get; private set; } = "";
    public string? Reason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static IdentityAudit Create(long actorId, long targetId, string action, string? reason = null) => new()
    {
        Id = Guid.NewGuid(), ActorId = actorId, TargetId = targetId, Action = action,
        Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(), CreatedAt = DateTimeOffset.UtcNow
    };
}
