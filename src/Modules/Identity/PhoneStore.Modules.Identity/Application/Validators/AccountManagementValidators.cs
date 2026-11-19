using FluentValidation;
using PhoneStore.BuildingBlocks.Validation;
using PhoneStore.Modules.Identity.Application.DTOs;

namespace PhoneStore.Modules.Identity.Application.Validators;

public sealed class ForgotPasswordRequestValidator : AppValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(254);
        RuleFor(x => x.Channel).Must(x => x is "email" or "sms");
    }
}
public sealed class VerifyRecoveryRequestValidator : AppValidator<VerifyRecoveryRequest>
{
    public VerifyRecoveryRequestValidator()
    {
        RuleFor(x => x.ChallengeId).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().Matches("^[0-9]{6}$");
    }
}
public sealed class CompleteRecoveryRequestValidator : AppValidator<CompleteRecoveryRequest>
{
    public CompleteRecoveryRequestValidator()
    {
        RuleFor(x => x.ChallengeId).NotEmpty();
        RuleFor(x => x.ResetToken).NotEmpty().Matches("^[0-9A-F]{64}$");
        RuleFor(x => new ResetPasswordRequest(x.NewPassword)).SetValidator(new ResetPasswordRequestValidator());
    }
}
public sealed class VerifyPhoneRequestValidator : AppValidator<VerifyPhoneRequest>
{
    public VerifyPhoneRequestValidator() => RuleFor(x => new LoginRequest("valid@example.com", x.CurrentPassword)).SetValidator(new LoginRequestValidator());
}
public sealed class ConfirmPhoneRequestValidator : AppValidator<ConfirmPhoneRequest>
{
    public ConfirmPhoneRequestValidator() => RuleFor(x => new VerifyRecoveryRequest(x.ChallengeId, x.Code)).SetValidator(new VerifyRecoveryRequestValidator());
}
public sealed class CustomerFilterQueryValidator : AppValidator<CustomerFilterQuery>
{
    public CustomerFilterQueryValidator()
    {
        RuleFor(x => x.Search).MaximumLength(100);
        RuleFor(x => x.Sort).Must(x => x is "createdAt" or "fullName" or "email");
        RuleFor(x => x.Page).GreaterThan(0).Must((x, p) => ((long)p - 1) * x.PageSize <= int.MaxValue);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
public sealed class UpdateCustomerRequestValidator : AppValidator<UpdateCustomerRequest>
{
    public UpdateCustomerRequestValidator()
    {
        RuleFor(x => new UpdateProfileRequest(x.FullName, x.Phone)).SetValidator(new UpdateProfileRequestValidator());
        RuleFor(x => x.ExpectedUpdatedAt).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}
public sealed class AuditFilterQueryValidator : AppValidator<AuditFilterQuery>
{
    public AuditFilterQueryValidator()
    {
        RuleFor(x => x.Action).MaximumLength(50);
        RuleFor(x => x.ActorId).GreaterThan(0).When(x => x.ActorId.HasValue);
        RuleFor(x => x.TargetId).GreaterThan(0).When(x => x.TargetId.HasValue);
        RuleFor(x => x.To).Must((x, to) => !to.HasValue || !x.From.HasValue || to >= x.From);
        RuleFor(x => x.Page).GreaterThan(0).Must((x, p) => ((long)p - 1) * x.PageSize <= int.MaxValue);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
