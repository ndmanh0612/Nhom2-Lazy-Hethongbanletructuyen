namespace PhoneStore.Modules.Identity.Application.DTOs;

public record ForgotPasswordRequest(string Email, string Channel = "email");
public record VerifyRecoveryRequest(Guid ChallengeId, string Code);
public record CompleteRecoveryRequest(Guid ChallengeId, string ResetToken, string NewPassword);
public record ChallengeDto(Guid ChallengeId, string Message);
public record RecoveryTokenDto(string ResetToken);
public record VerifyPhoneRequest(string CurrentPassword);
public record ConfirmPhoneRequest(Guid ChallengeId, string Code);
public record CustomerFilterQuery(string? Search, bool? IsActive, string Sort = "createdAt", bool Descending = false, int Page = 1, int PageSize = 20);
public record UpdateCustomerRequest(string FullName, string? Phone, DateTimeOffset ExpectedUpdatedAt, string Reason);
public record CustomerDetailDto(UserProfileDto Profile, IReadOnlyList<AddressDto> Addresses);
public record AuditFilterQuery(long? ActorId, long? TargetId, string? Action, DateTimeOffset? From, DateTimeOffset? To, int Page = 1, int PageSize = 20);
public record AuditDto(Guid Id, long ActorId, long TargetId, string Action, string? Reason, DateTimeOffset CreatedAt);
