namespace PhoneStore.BuildingBlocks.Application;

/// <summary>
/// N2LHTBLTT-9: Lưu trữ refresh token để hỗ trợ đăng xuất (thu hồi token)
/// và xoay vòng token khi làm mới.
///
/// Interface đặt ở BuildingBlocks để module Identity (nơi implement)
/// không bị phụ thuộc ngược lại và không rò rỉ DbContext ra ngoài.
/// </summary>
public interface IRefreshTokenStore
{
    /// <summary>Lưu refresh token mới kèm thông tin người dùng.</summary>
    Task StoreAsync(
        string tokenHash,
        long userId,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy userId gắn với refresh token và đánh dấu token cũ đã bị thay thế.
    /// Trả về null nếu token không tồn tại, đã hết hạn hoặc đã bị thu hồi.
    /// </summary>
    Task<long?> ConsumeAsync(string tokenHash, CancellationToken cancellationToken = default);

    /// <summary>Đánh dấu refresh token đã bị thu hồi (đăng xuất).</summary>
    Task RevokeAsync(string tokenHash, CancellationToken cancellationToken = default);

    /// <summary>Kiểm tra phiên JWT và trạng thái/vai trò hiện tại của tài khoản.</summary>
    Task<bool> IsActiveAsync(string tokenHash, long userId, string role, CancellationToken cancellationToken = default);
}
