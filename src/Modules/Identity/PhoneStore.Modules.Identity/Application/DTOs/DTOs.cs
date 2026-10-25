using System.ComponentModel.DataAnnotations;

namespace PhoneStore.Modules.Identity.Application.DTOs;

public record RegisterRequest(
    [Required, EmailAddress] string Email,
    [Required, MinLength(8), MaxLength(72)] string Password,
    [Required, MaxLength(100)] string FullName,
    [Phone] string? Phone
);

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password
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
    [Required, MaxLength(100)] string FullName,
    [Phone] string? Phone
);

public record ChangePasswordRequest(
    [Required] string CurrentPassword,
    [Required, MinLength(8), MaxLength(72)] string NewPassword
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
    [Required, MaxLength(100)] string ReceiverName,
    [Required, Phone] string ReceiverPhone,
    [Required, MaxLength(500)] string AddressText
);
