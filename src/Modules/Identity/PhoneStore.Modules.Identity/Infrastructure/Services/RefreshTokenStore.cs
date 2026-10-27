using Microsoft.EntityFrameworkCore;
using PhoneStore.BuildingBlocks.Application;
using PhoneStore.Modules.Identity.Domain.Entities;
using PhoneStore.Modules.Identity.Infrastructure.Persistence;

namespace PhoneStore.Modules.Identity.Infrastructure.Services;

/// <summary>
/// N2LHTBLTT-9: Lưu trữ refresh token trong schema identity.
///
/// Dùng khóa chính <c>TokenHash</c> để tìm nhanh; ConsumeAsync chạy trong
/// transaction với khóa <c>FOR UPDATE</c> để tránh race condition khi hai
/// request làm mới cùng một token (token reuse) — chỉ một request được thành công.
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

        // Khóa dòng để chống token reuse: hai request refresh song song sẽ được
        // serialize, request thứ hai thấy token đã bị thu hồi nên bị từ chối.
        var token = await _dbContext.RefreshTokens
            .FromSqlInterpolated($"SELECT * FROM identity.refresh_tokens WHERE token_hash = {tokenHash} FOR UPDATE")
            .FirstOrDefaultAsync(cancellationToken);

        if (token is null || !token.IsUsable(now))
        {
            return null;
        }

        token.Revoke();
        await _dbContext.SaveChangesAsync(cancellationToken);

        return token.UserId;
    }

    public async Task RevokeAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        var token = await _dbContext.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (token is null || token.IsRevoked)
        {
            return;
        }

        token.Revoke();
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
