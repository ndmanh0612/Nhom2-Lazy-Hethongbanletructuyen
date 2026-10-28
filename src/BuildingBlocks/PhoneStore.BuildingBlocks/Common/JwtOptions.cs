using System.Security.Cryptography;

namespace PhoneStore.BuildingBlocks.Common;

/// <summary>
/// N2LHTBLTT-9: Cấu hình xác thực (JWT + thời hạn token), đọc từ appsettings
/// theo mục "Authentication".
/// </summary>
public class JwtOptions
{
    // Chỉ dùng local: khởi động lại process sẽ vô hiệu hóa JWT dùng khóa này.
    private static readonly string DevelopmentSigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    public const string SectionName = "Authentication";

    /// <summary>Issuer của access token.</summary>
    public string Issuer { get; set; } = "PhoneStore";

    /// <summary>Audience của access token.</summary>
    public string Audience { get; set; } = "PhoneStoreClient";

    /// <summary>Khoá ký (bắt buộc ≥ 32 ký tự để HMAC-SHA256 an toàn).</summary>
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>Thời hạn access token (phút).</summary>
    public int AccessTokenMinutes { get; set; } = 30;

    /// <summary>Thời hạn refresh token (ngày).</summary>
    public int RefreshTokenDays { get; set; } = 7;

    /// <summary>
    /// True để sinh khóa tạm trong process — Host chỉ cho bật ở Development.
    /// Production bắt buộc phải cấu hình SigningKey qua biến môi trường.
    /// </summary>
    public bool AllowDevelopmentSigningKey { get; set; }

    /// <summary>Khoá ký dùng thực tế: ưu tiên SigningKey, fallback sang khoá dev.</summary>
    public string ResolveSigningKey()
    {
        if (string.IsNullOrWhiteSpace(Issuer) || string.IsNullOrWhiteSpace(Audience)
            || AccessTokenMinutes <= 0 || RefreshTokenDays <= 0)
        {
            throw new InvalidOperationException("Cấu hình Authentication không hợp lệ.");
        }

        if (!string.IsNullOrWhiteSpace(SigningKey))
        {
            if (SigningKey.Length < 32)
                throw new InvalidOperationException("Authentication:SigningKey phải có ít nhất 32 ký tự.");
            return SigningKey;
        }

        if (AllowDevelopmentSigningKey)
        {
            return DevelopmentSigningKey;
        }

        throw new InvalidOperationException(
            "Authentication:SigningKey chưa được cấu hình hoặc ngắn hơn 32 ký tự.");
    }
}
