using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PhoneStore.BuildingBlocks.Application;
using PhoneStore.BuildingBlocks.Common;

namespace PhoneStore.BuildingBlocks.Infrastructure.Identity;

/// <summary>
/// N2LHTBLTT-9: Phát access token (JWT) và refresh token (opaque).
///
/// Access token mang các claim: sub (user id), email, role, jti, sid (phiên trong DB).
/// Refresh token là chuỗi ngẫu nhiên 64 byte; chỉ lưu bản SHA-256 trong
/// database (IRefreshTokenStore) — không lưu bản gốc để giảm rủi ro lộ token.
/// </summary>
public class JwtTokenService : ITokenService
{
    private readonly JwtOptions _options;
    private readonly IRefreshTokenStore _refreshTokenStore;
    private readonly TimeProvider _timeProvider;

    public JwtTokenService(
        IOptions<JwtOptions> options,
        IRefreshTokenStore refreshTokenStore,
        TimeProvider timeProvider)
    {
        _options = options.Value;
        _refreshTokenStore = refreshTokenStore;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Phát cặp token mới. Vì StoreAsync là I/O, method này bất đồng bộ.
    /// </summary>
    public async Task<TokenPair> CreateTokenPairAsync(
        long userId,
        string email,
        string role,
        CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();
        var expiresAt = now.AddMinutes(_options.AccessTokenMinutes);
        var refreshToken = GenerateRefreshToken();
        var sessionId = HashToken(refreshToken);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new("sid", sessionId),
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Role, role)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.ResolveSigningKey()));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);

        await _refreshTokenStore.StoreAsync(
            sessionId,
            userId,
            now.AddDays(_options.RefreshTokenDays),
            cancellationToken);

        return new TokenPair(accessToken, refreshToken, expiresAt);
    }

    public TokenPair CreateTokenPair(long userId, string email, string role)
    {
        return CreateTokenPairAsync(userId, email, role).GetAwaiter().GetResult();
    }

    internal static string GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes);
    }

    /// <summary>
    /// SHA-256 hex của refresh token. Công khai để module Identity dùng
    /// cùng một thuật toán khi tra cứu / thu hồi token.
    /// </summary>
    public static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }
}
