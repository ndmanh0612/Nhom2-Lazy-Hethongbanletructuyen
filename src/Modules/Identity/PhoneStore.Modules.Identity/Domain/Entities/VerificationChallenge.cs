namespace PhoneStore.Modules.Identity.Domain.Entities;

// Only hashes of credentials are persisted. Purpose separates phone enrollment from recovery.
public sealed class VerificationChallenge
{
    public Guid Id { get; private set; }
    public long UserId { get; private set; }
    public string Purpose { get; private set; } = "";
    public string Channel { get; private set; } = "";
    public string Destination { get; private set; } = "";
    public string CodeHash { get; private set; } = "";
    public string? ResetTokenHash { get; private set; }
    public int Attempts { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? VerifiedAt { get; private set; }
    public DateTimeOffset? ConsumedAt { get; private set; }

    public static VerificationChallenge Create(Guid id, long userId, string purpose, string channel,
        string destination, string codeHash, DateTimeOffset now) => new()
    {
        Id = id, UserId = userId, Purpose = purpose, Channel = channel, Destination = destination,
        CodeHash = codeHash, CreatedAt = now, ExpiresAt = now.AddMinutes(5)
    };

    public bool CanVerify(DateTimeOffset now) => ConsumedAt is null && VerifiedAt is null && Attempts < 5 && ExpiresAt > now;
    public void FailAttempt() => Attempts++;
    public void Verify(string tokenHash, DateTimeOffset now)
    {
        VerifiedAt = now;
        ResetTokenHash = tokenHash;
        ExpiresAt = now.AddMinutes(5);
    }
    public void Consume(DateTimeOffset now) => ConsumedAt = now;
}
