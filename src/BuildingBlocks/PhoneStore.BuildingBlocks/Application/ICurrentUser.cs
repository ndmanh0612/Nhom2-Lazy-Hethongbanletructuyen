namespace PhoneStore.BuildingBlocks.Application;

/// <summary>
/// Lấy thông tin người dùng đang đăng nhập từ request hiện tại.
///
/// N2LHTBLTT-9: sau khi Identity phát JWT, danh tính được lấy từ claim trong
/// access token (claim "sub" / ClaimTypes.NameIdentifier) và claim vai trò.
/// Nếu không có token hợp lệ, <see cref="UserId"/> trả null để endpoint trả 401.
///
/// Lưu ý: cơ chế header "X-User-Id" tạm thời đã được gỡ bỏ vì không phản ánh
/// xác thực thật (N2LHTBLTT-38).</summary>
public interface ICurrentUser
{
    /// <summary>Id người dùng đang đăng nhập, null nếu chưa xác thực.</summary>
    long? UserId { get; }

    /// <summary>Vai trò của người dùng đang đăng nhập (CUSTOMER/STAFF/ADMIN).</summary>
    string? Role { get; }

    /// <summary>True nếu request mang token hợp lệ.</summary>
    bool IsAuthenticated { get; }

    /// <summary>True nếu người dùng đang đăng nhập có vai trò được chỉ định.</summary>
    bool IsInRole(string role);
}
