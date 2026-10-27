namespace PhoneStore.BuildingBlocks.Application;

/// <summary>
/// N2LHTBLTT-9: Kết quả phát token sau khi đăng nhập / làm mới token.
/// </summary>
/// <param name="AccessToken">JWT trả về cho client (gửi kèm header Authorization: Bearer).</param>
/// <param name="RefreshToken">Refresh token dạng opaque, lưu trong cookie HttpOnly.</param>
/// <param name="ExpiresAt">Thời điểm access token hết hạn.</param>
public record TokenPair(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt);

/// <summary>
/// N2LHTBLTT-9: Phát access token / refresh token.
///
/// Lưu ý kiến trúc: nghiệp vụ làm mới token (đọc user, kiểm tra trạng thái tài
/// khoản) thuộc module Identity, nên interface này chỉ lo phần phát token.
/// </summary>
public interface ITokenService
{
    /// <summary>Phát cặp token mới cho người dùng với vai trò đã xác định.</summary>
    Task<TokenPair> CreateTokenPairAsync(
        long userId,
        string email,
        string role,
        CancellationToken cancellationToken = default);
}
