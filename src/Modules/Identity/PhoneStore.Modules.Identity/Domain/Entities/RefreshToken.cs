using PhoneStore.BuildingBlocks.Domain;

namespace PhoneStore.Modules.Identity.Domain.Entities;

/// <summary>
/// N2LHTBLTT-9: Bản ghi refresh token.
///
/// Chỉ lưu SHA-256 hash của token (không lưu bản gốc) để giảm rủi ro lộ
/// token khi database bị truy cập trái phép. Token bị "xoay vòng" (rotate) mỗi
/// lần làm mới: token cũ được đánh dấu đã thay thế và không dùng lại được.
/// </summary>
public class RefreshToken : BaseEntity<long>
{
    public long UserId { get; private set; }

    /// <summary>SHA-256 hash (hex) của refresh token.</summary>
    public string TokenHash { get; private set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>Token đã bị thu hồi (đăng xuất) hoặc đã bị thay thế sau khi xoay vòng.</summary>
    public bool IsRevoked { get; private set; }

    /// <summary>Thời điểm token bị thay thế bởi một token mới.</summary>
    public DateTimeOffset? RevokedAt { get; private set; }

    protected RefreshToken() { }

    public static RefreshToken Create(long userId, string tokenHash, DateTimeOffset expiresAt)
    {
        if (userId <= 0)
        {
            throw new ArgumentException("Id người dùng không hợp lệ.", nameof(userId));
        }

        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new ArgumentException("Hash token không được để trống.", nameof(tokenHash));
        }

        return new RefreshToken
        {
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
            IsRevoked = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>Đánh dấu token không còn dùng được nữa.</summary>
    public void Revoke()
    {
        if (IsRevoked)
        {
            return;
        }

        IsRevoked = true;
        RevokedAt = DateTimeOffset.UtcNow;
        Touch();
    }

    /// <summary>Token còn hiệu lực và chưa bị thu hồi.</summary>
    public bool IsUsable(DateTimeOffset now) => !IsRevoked && ExpiresAt > now;
}
