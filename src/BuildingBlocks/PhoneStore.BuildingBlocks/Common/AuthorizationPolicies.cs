namespace PhoneStore.BuildingBlocks.Common;

/// <summary>
/// N2LHTBLTT-9 / N2LHTBLTT-16: Vai trò người dùng trong hệ thống.
/// Định nghĩa ở BuildingBlocks để mọi module dùng chung mà không cần
/// reference trực tiếp module Identity (ràng buộc kiến trúc Modular Monolith).
/// </summary>
public static class AppRoles
{
    public const string Customer = "CUSTOMER";
    public const string Staff = "STAFF";
    public const string Admin = "ADMIN";

    public static readonly string[] All = [Customer, Staff, Admin];
}

/// <summary>
/// N2LHTBLTT-16 / N2LHTBLTT-33: Tên các authorization policy dùng chung.
/// Mỗi policy được khai báo trong <c>BuildingBlocksRegistration.AddBuildingBlocks</c>.
/// </summary>
public static class AuthorizationPolicies
{
    /// <summary>Yêu cầu đã đăng nhập (mọi vai trò).</summary>
    public const string RequireAuthenticated = "RequireAuthenticated";

    /// <summary>Yêu cầu vai trò CUSTOMER hoặc cao hơn.</summary>
    public const string RequireCustomer = "RequireCustomer";

    /// <summary>Yêu cầu vai trò STAFF hoặc ADMIN.</summary>
    public const string RequireStaff = "RequireStaff";

    /// <summary>Yêu cầu vai trò ADMIN.</summary>
    public const string RequireAdmin = "RequireAdmin";
}

/// <summary>
/// N2LHTBLTT-38: Tên cookie dùng cho cơ chế session + CSRF.
/// </summary>
public static class AuthCookies
{
    /// <summary>Cookie chứa refresh token (HttpOnly — không đọc được bằng JavaScript).</summary>
    public const string RefreshToken = "phonestore_rt";

    /// <summary>Cookie chứa CSRF token (đọc được bởi client để gửi kèm header).</summary>
    public const string CsrfToken = "phonestore_csrf";
}

/// <summary>
/// N2LHTBLTT-38: Tên header mà client gửi kèm CSRF token.
/// </summary>
public static class AuthHeaders
{
    public const string CsrfToken = "X-CSRF-TOKEN";
}
