using System.Text.Json.Serialization;

namespace PhoneStore.Modules.Identity.Application.DTOs;

// N2LHTBLTT-14: Validation được thực hiện bằng FluentValidation (IdentityValidators),
// không dùng DataAnnotations để tránh chạy validation hai lần.

public record RegisterRequest(
    string Email,
    string Password,
    string FullName,
    string? Phone
);

public record LoginRequest(
    string Email,
    string Password
);

/// <summary>
/// FR-01 / FR-02: Kết quả xác thực.
///
/// AccessToken trả trong body để client gửi kèm header Authorization: Bearer.
/// RefreshToken bị đánh dấu [JsonIgnore] — controller đưa nó vào cookie HttpOnly,
/// nhờ vậy JavaScript không thể đọc được token làm mới phiên.
/// </summary>
public record AuthResponseDto(
    long UserId,
    string Email,
    string FullName,
    string Role,
    string AccessToken,
    DateTimeOffset ExpiresAt)
{
    /// <summary>Refresh token — chỉ dùng để set cookie, không serialize ra body.</summary>
    [JsonIgnore]
    public string RefreshToken { get; init; } = string.Empty;
}

public record UserProfileDto(
    long Id,
    string Email,
    string FullName,
    string? Phone,
    string Role,
    bool IsActive,
    DateTimeOffset CreatedAt
);

public record UpdateProfileRequest(
    string FullName,
    string? Phone
);

public record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword
);

public record AddressDto(
    long Id,
    long UserId,
    string ReceiverName,
    string ReceiverPhone,
    string AddressText,
    DateTimeOffset CreatedAt
);

public record CreateAddressRequest(
    string ReceiverName,
    string ReceiverPhone,
    string AddressText
);

// ---------------------------------------------------------------------------
// FR-15 (N2LHTBLTT-16): Quản lý tài khoản nhân viên.
// ---------------------------------------------------------------------------

/// <summary>FR-15: Tạo tài khoản nhân viên (STAFF hoặc ADMIN).</summary>
public record CreateStaffRequest(
    string Email,
    string Password,
    string FullName,
    string Role,
    string? Phone);

/// <summary>FR-15: Lọc danh sách tài khoản.</summary>
public record StaffFilterQuery(
    string? Search,
    string? Role,
    bool? IsActive,
    int Page = 1,
    int PageSize = 20);

/// <summary>FR-15: Đổi vai trò của một tài khoản.</summary>
public record ChangeRoleRequest(string Role);

/// <summary>FR-15: Khóa / mở khóa tài khoản.</summary>
public record LockAccountRequest(bool IsLocked, string? Reason);

/// <summary>FR-15: Đặt lại mật khẩu cho tài khoản.</summary>
public record ResetPasswordRequest(string NewPassword);
