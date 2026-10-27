namespace PhoneStore.BuildingBlocks.Common;

/// <summary>
/// N2LHTBLTT-9: Cấu hình xác thực (JWT + thời hạn token), đọc từ appsettings
/// theo mục "Authentication".
/// </summary>
public class JwtOptions
{
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
    /// True nếu cấu hình dùng khoá ký mặc định yếu — chỉ nên bật ở Development.
    /// Production bắt buộc phải cấu hình SigningKey qua biến môi trường.
    /// </summary>
    public bool AllowDevelopmentSigningKey { get; set; } = true;

    /// <summary>Khoá ký dùng thực tế: ưu tiên SigningKey, fallback sang khoá dev.</summary>
    public string ResolveSigningKey()
    {
        if (!string.IsNullOrWhiteSpace(SigningKey) && SigningKey.Length >= 32)
        {
            return SigningKey;
        }

        if (AllowDevelopmentSigningKey)
        {
            return "PhoneStore-Development-Signing-Key-Change-Me-Please-2026";
        }

        throw new InvalidOperationException(
            "Authentication:SigningKey chưa được cấu hình hoặc ngắn hơn 32 ký tự.");
    }
}
