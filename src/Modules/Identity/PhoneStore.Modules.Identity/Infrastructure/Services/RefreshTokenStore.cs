using Microsoft.EntityFrameworkCore;
using PhoneStore.BuildingBlocks.Application;
using PhoneStore.Modules.Identity.Domain.Entities;
using PhoneStore.Modules.Identity.Infrastructure.Persistence;

namespace PhoneStore.Modules.Identity.Infrastructure.Services;

/// <summary>
/// N2LHTBLTT-9: Lưu trữ refresh token trong schema identity.
///
/// TokenHash có unique index. UPDATE có điều kiện chỉ cho phép một request
/// tiêu thụ token; service bao transaction cho việc tiêu thụ và phát token mới.
/// </summary>
public class RefreshTokenStore : IRefreshTokenStore
{
    private readonly IdentityDbContext _dbContext;

    public RefreshTokenStore(IdentityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task StoreAsync(
        string tokenHash,
        long userId,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default)
    {
        var token = RefreshToken.Create(userId, tokenHash, expiresAt);
        _dbContext.RefreshTokens.Add(token);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<long?> ConsumeAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        var updated = await _dbContext.RefreshTokens
            .Where(t => t.TokenHash == tokenHash && !t.IsRevoked && t.ExpiresAt > now)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(t => t.IsRevoked, true)
                .SetProperty(t => t.RevokedAt, now)
                .SetProperty(t => t.UpdatedAt, now), cancellationToken);

        return updated == 1
            ? await _dbContext.RefreshTokens.Where(t => t.TokenHash == tokenHash)
                .Select(t => (long?)t.UserId).SingleAsync(cancellationToken)
            : null;
    }

    public async Task RevokeAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        await _dbContext.RefreshTokens
            .Where(t => t.TokenHash == tokenHash && !t.IsRevoked)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(t => t.IsRevoked, true)
                .SetProperty(t => t.RevokedAt, now)
                .SetProperty(t => t.UpdatedAt, now), cancellationToken);
    }

    public async Task<bool> IsActiveAsync(string tokenHash, long userId, string role, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<UserRole>(role, out var userRole) || !Enum.IsDefined(userRole)) return false;

        var now = DateTimeOffset.UtcNow;
        return await (from token in _dbContext.RefreshTokens
                      join user in _dbContext.Users on token.UserId equals user.Id
                      where token.TokenHash == tokenHash && token.UserId == userId
                          && !token.IsRevoked && token.ExpiresAt > now && user.IsActive && user.Role == userRole
                      select token.Id).AnyAsync(cancellationToken);
    }
}
