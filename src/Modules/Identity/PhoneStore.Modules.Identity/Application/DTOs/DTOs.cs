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

public record AuthResponseDto(
    long UserId,
    string Email,
    string FullName,
    string Role,
    string Token
);

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
