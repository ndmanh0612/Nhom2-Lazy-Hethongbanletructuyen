using FluentValidation;
using PhoneStore.BuildingBlocks.Validation;
using PhoneStore.Modules.Identity.Application.DTOs;

namespace PhoneStore.Modules.Identity.Application.Validators;

/// <summary>
/// FR-01 / FR-02: Validation cho đăng ký, đăng nhập và quản lý hồ sơ.
/// </summary>
public class RegisterRequestValidator : AppValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(request => request.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(254)
            .WithMessage("Email không hợp lệ.");

        RuleFor(request => request.Password)
            .NotEmpty()
            .MinimumLength(8)
            .MaximumLength(72)
            .Matches(@"^(?=.*[A-Za-z])(?=.*\d).+$")
            .WithMessage("Mật khẩu phải có ít nhất 8 ký tự, gồm chữ và số.");

        RuleFor(request => request.FullName)
            .NotEmpty()
            .MaximumLength(100)
            .WithMessage("Họ tên không được để trống và tối đa 100 ký tự.");

        RuleFor(request => request.Phone)
            .Matches(@"^(0|\+84)\d{8,11}$")
            .When(request => !string.IsNullOrWhiteSpace(request.Phone))
            .WithMessage("Số điện thoại không hợp lệ. Ví dụ hợp lệ: 0912345678.");
    }
}

public class LoginRequestValidator : AppValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(request => request.Email)
            .NotEmpty()
            .EmailAddress()
            .WithMessage("Email không hợp lệ.");

        RuleFor(request => request.Password)
            .NotEmpty()
            .WithMessage("Mật khẩu không được để trống.");
    }
}

public class UpdateProfileRequestValidator : AppValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(request => request.FullName)
            .NotEmpty()
            .MaximumLength(100)
            .WithMessage("Họ tên không được để trống và tối đa 100 ký tự.");

        RuleFor(request => request.Phone)
            .Matches(@"^(0|\+84)\d{8,11}$")
            .When(request => !string.IsNullOrWhiteSpace(request.Phone))
            .WithMessage("Số điện thoại không hợp lệ. Ví dụ hợp lệ: 0912345678.");
    }
}

public class ChangePasswordRequestValidator : AppValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(request => request.CurrentPassword)
            .NotEmpty()
            .WithMessage("Mật khẩu hiện tại không được để trống.");

        RuleFor(request => request.NewPassword)
            .NotEmpty()
            .MinimumLength(8)
            .MaximumLength(72)
            .Matches(@"^(?=.*[A-Za-z])(?=.*\d).+$")
            .WithMessage("Mật khẩu mới phải có ít nhất 8 ký tự, gồm chữ và số.");

        RuleFor(request => request.NewPassword)
            .NotEqual(request => request.CurrentPassword)
            .WithMessage("Mật khẩu mới không được trùng mật khẩu hiện tại.");
    }
}

public class CreateAddressRequestValidator : AppValidator<CreateAddressRequest>
{
    public CreateAddressRequestValidator()
    {
        RuleFor(request => request.ReceiverName)
            .NotEmpty()
            .MaximumLength(100)
            .WithMessage("Tên người nhận không được để trống và tối đa 100 ký tự.");

        RuleFor(request => request.ReceiverPhone)
            .NotEmpty()
            .Matches(@"^(0|\+84)\d{8,11}$")
            .WithMessage("Số điện thoại không hợp lệ.");

        RuleFor(request => request.AddressText)
            .NotEmpty()
            .MaximumLength(500)
            .WithMessage("Địa chỉ không được để trống và tối đa 500 ký tự.");
    }
}

// ---------------------------------------------------------------------------
// FR-15 (N2LHTBLTT-16): Validation cho quản trị tài khoản nhân viên.
// ---------------------------------------------------------------------------

public class CreateStaffRequestValidator : AppValidator<CreateStaffRequest>
{
    public CreateStaffRequestValidator()
    {
        RuleFor(request => request.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(254)
            .WithMessage("Email không hợp lệ.");

        RuleFor(request => request.Password)
            .NotEmpty()
            .MinimumLength(8)
            .MaximumLength(72)
            .Matches(@"^(?=.*[A-Za-z])(?=.*\d).+$")
            .WithMessage("Mật khẩu phải có ít nhất 8 ký tự, gồm chữ và số.");

        RuleFor(request => request.FullName)
            .NotEmpty()
            .MaximumLength(100)
            .WithMessage("Họ tên không được để trống và tối đa 100 ký tự.");

        RuleFor(request => request.Role)
            .NotEmpty()
            .Must(role => role.Equals("STAFF", StringComparison.OrdinalIgnoreCase)
                || role.Equals("ADMIN", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Vai trò chỉ nhận STAFF hoặc ADMIN.");

        RuleFor(request => request.Phone)
            .Matches(@"^(0|\+84)\d{8,11}$")
            .When(request => !string.IsNullOrWhiteSpace(request.Phone))
            .WithMessage("Số điện thoại không hợp lệ.");
    }
}

public class ChangeRoleRequestValidator : AppValidator<ChangeRoleRequest>
{
    public ChangeRoleRequestValidator()
    {
        RuleFor(request => request.Role)
            .NotEmpty()
            .Must(role => role.Equals("STAFF", StringComparison.OrdinalIgnoreCase)
                || role.Equals("ADMIN", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Vai trò chỉ nhận STAFF hoặc ADMIN.");
    }
}

public class ResetPasswordRequestValidator : AppValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(request => request.NewPassword)
            .NotEmpty()
            .MinimumLength(8)
            .MaximumLength(72)
            .Matches(@"^(?=.*[A-Za-z])(?=.*\d).+$")
            .WithMessage("Mật khẩu phải có ít nhất 8 ký tự, gồm chữ và số.");
    }
}

public class StaffFilterQueryValidator : AppValidator<StaffFilterQuery>
{
    public StaffFilterQueryValidator()
    {
        RuleFor(query => query.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Số trang phải bắt đầu từ 1.");

        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("Số bản ghi mỗi trang phải từ 1 đến 100.");

        RuleFor(query => query.Role)
            .Must(role => string.IsNullOrWhiteSpace(role)
                || role.Equals("STAFF", StringComparison.OrdinalIgnoreCase)
                || role.Equals("ADMIN", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Vai trò chỉ nhận STAFF hoặc ADMIN.");
    }
}
