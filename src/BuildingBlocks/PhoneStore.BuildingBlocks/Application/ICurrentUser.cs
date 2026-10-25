namespace PhoneStore.BuildingBlocks.Application;

/// <summary>
/// Lấy id người dùng đang đăng nhập từ request hiện tại.
/// Hiện tại Identity chưa có session thật (task N2LHTBLTT-8, N2LHTBLTT-9 của Trí),
/// nên implementation mặc định đọc claim "sub" rồi fallback sang header "X-User-Id".
/// Khi Identity có JWT thật, chỉ cần đổi implementation, các module khác không đổi.
/// </summary>
public interface ICurrentUser
{
    long? UserId { get; }
    bool IsAuthenticated { get; }
}
