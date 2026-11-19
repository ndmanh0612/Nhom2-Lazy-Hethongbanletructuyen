using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using PhoneStore.BuildingBlocks.Exceptions;
using PhoneStore.BuildingBlocks.Infrastructure.Identity;
using PhoneStore.Modules.Identity.Application.DTOs;
using PhoneStore.Modules.Identity.Domain.Entities;
using PhoneStore.Modules.Identity.Infrastructure.Persistence;
using PhoneStore.Modules.Identity.Infrastructure.Services;

namespace PhoneStore.Modules.Identity.Application.Services;

public sealed class AccountRecoveryService(IdentityDbContext db, IPasswordHasher passwords,
    IRecoveryDelivery delivery, IOptions<RecoveryOptions> options, TimeProvider clock)
{
    private const string Recovery = "password-reset";
    private const string Phone = "phone-verification";
    private const string GenericMessage = "Nếu tài khoản đủ điều kiện, mã sẽ được gửi qua kênh đã chọn. Mã có hiệu lực 5 phút; chờ ít nhất 60 giây trước khi yêu cầu lại.";

    public async Task<ChallengeDto> RequestAsync(ForgotPasswordRequest request, CancellationToken ct)
    {
        EnsureReady(request.Channel);
        var id = Guid.NewGuid();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FromSqlInterpolated($"""SELECT * FROM identity.users WHERE "Email" = {email} FOR UPDATE""").FirstOrDefaultAsync(ct);
        if (user is not null && user.IsActive)
        {
            var destination = request.Channel == "email" ? user.Email : user.VerifiedPhone;
            if (destination is not null && await CanIssueAsync(user.Id, Recovery, ct))
                await IssueAsync(id, user.Id, Recovery, request.Channel, destination, ct);
        }
        await tx.CommitAsync(ct);
        return new(id, GenericMessage);
    }

    public async Task<RecoveryTokenDto> VerifyAsync(VerifyRecoveryRequest request, CancellationToken ct)
    {
        EnsureKey();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var challenge = await LockChallengeAsync(request.ChallengeId, Recovery, ct);
        if (challenge is null || !challenge.CanVerify(clock.GetUtcNow())) throw InvalidCode();
        if (!Matches(challenge.CodeHash, HashCode(challenge.Id, challenge.Purpose, request.Code)))
        {
            challenge.FailAttempt();
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct); // Failed attempts must persist even though the response is 400.
            throw InvalidCode();
        }
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        challenge.Verify(HashToken(token), clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new(token);
    }

    public async Task CompleteAsync(CompleteRecoveryRequest request, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Every flow locks user BEFORE challenge/session rows, including admin invalidation.
        var ownerId = await db.VerificationChallenges.AsNoTracking().Where(x => x.Id == request.ChallengeId).Select(x => (long?)x.UserId).FirstOrDefaultAsync(ct);
        var user = ownerId.HasValue ? await LockUserAsync(ownerId.Value, ct) : null;
        var challenge = user is null ? null : await LockChallengeAsync(request.ChallengeId, Recovery, ct);
        if (user is null || !user.IsActive || challenge is null || challenge.ConsumedAt is not null
            || challenge.VerifiedAt is null || challenge.ExpiresAt <= clock.GetUtcNow()
            || challenge.ResetTokenHash is null || !Matches(challenge.ResetTokenHash, HashToken(request.ResetToken))
            || (challenge.Channel == "sms" && challenge.Destination != user.VerifiedPhone)) throw InvalidCode();
        user.UpdatePassword(passwords.HashPassword(request.NewPassword));
        challenge.Consume(clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        await InvalidateCredentialsAsync(user.Id, ct);
        db.AuditEntries.Add(IdentityAudit.Create(user.Id, user.Id, "PasswordRecovered"));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task<ChallengeDto> RequestPhoneAsync(long userId, VerifyPhoneRequest request, CancellationToken ct)
    {
        EnsureReady("sms");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var user = await LockUserAsync(userId, ct);
        if (user is null || !user.IsActive || !passwords.VerifyPassword(request.CurrentPassword, user.PasswordHash))
            throw new ValidationException("currentPassword", "Không thể xác minh tài khoản bằng mật khẩu này.");
        var phone = NormalizePhone(user.Phone);
        if (!await CanIssueAsync(userId, Phone, ct))
            throw new AppException("Vui lòng chờ 60 giây trước khi yêu cầu lại.", "RECOVERY_COOLDOWN", HttpStatusCode.TooManyRequests);
        var id = Guid.NewGuid();
        await IssueAsync(id, userId, Phone, "sms", phone, ct);
        await tx.CommitAsync(ct);
        return new(id, "Nhà cung cấp đã chấp nhận yêu cầu gửi mã. Mã có hiệu lực 5 phút.");
    }

    public async Task ConfirmPhoneAsync(long userId, ConfirmPhoneRequest request, CancellationToken ct)
    {
        EnsureKey();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var user = await LockUserAsync(userId, ct);
        var challenge = await LockChallengeAsync(request.ChallengeId, Phone, ct);
        if (user is null || !user.IsActive || challenge is null || challenge.UserId != userId
            || !challenge.CanVerify(clock.GetUtcNow()) || challenge.Destination != NormalizePhone(user.Phone)) throw InvalidCode();
        if (!Matches(challenge.CodeHash, HashCode(challenge.Id, Phone, request.Code)))
        {
            challenge.FailAttempt();
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            throw InvalidCode();
        }
        user.VerifyPhone(challenge.Destination);
        challenge.Consume(clock.GetUtcNow());
        db.AuditEntries.Add(IdentityAudit.Create(userId, userId, "PhoneVerified"));
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_users_VerifiedPhone" })
        { throw new ConflictException("Số điện thoại này đã được xác minh cho tài khoản khác.", "PHONE_ALREADY_VERIFIED"); }
        await tx.CommitAsync(ct);
    }

    public static string NormalizePhone(string? value)
    {
        var phone = value?.Trim() ?? "";
        if (Regex.IsMatch(phone, "^0[0-9]{9}$")) phone = "+84" + phone[1..];
        if (!Regex.IsMatch(phone, @"^\+[1-9][0-9]{7,14}$"))
            throw new ValidationException("phone", "Số SMS phải theo E.164, hoặc số Việt Nam 10 chữ số bắt đầu bằng 0.");
        return phone;
    }

    private async Task IssueAsync(Guid id, long userId, string purpose, string channel, string destination, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        await db.VerificationChallenges.Where(x => x.UserId == userId && x.Purpose == purpose && x.ConsumedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ConsumedAt, now), ct);
        var code = RandomNumberGenerator.GetInt32(1_000_000).ToString("D6", CultureInfo.InvariantCulture);
        db.VerificationChallenges.Add(VerificationChallenge.Create(id, userId, purpose, channel, destination, HashCode(id, purpose, code), now));
        await db.SaveChangesAsync(ct);
        // Provider failure rolls back the challenge; no success is claimed without acceptance.
        await delivery.SendAsync(channel, destination, code, ct);
    }
    private async Task<bool> CanIssueAsync(long userId, string purpose, CancellationToken ct)
    {
        var cutoff = clock.GetUtcNow().AddSeconds(-60);
        return !await db.VerificationChallenges.AnyAsync(x => x.UserId == userId && x.Purpose == purpose && x.CreatedAt > cutoff, ct);
    }
    private Task<User?> LockUserAsync(long id, CancellationToken ct) => db.Users
        .FromSqlInterpolated($"""SELECT * FROM identity.users WHERE "Id" = {id} FOR UPDATE""").FirstOrDefaultAsync(ct);
    private Task<VerificationChallenge?> LockChallengeAsync(Guid id, string purpose, CancellationToken ct) => db.VerificationChallenges
        .FromSqlInterpolated($"""SELECT * FROM identity.verification_challenges WHERE "Id" = {id} AND "Purpose" = {purpose} FOR UPDATE""").FirstOrDefaultAsync(ct);
    private async Task InvalidateCredentialsAsync(long userId, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        await db.RefreshTokens.Where(x => x.UserId == userId && !x.IsRevoked)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsRevoked, true).SetProperty(x => x.RevokedAt, now), ct);
        await db.VerificationChallenges.Where(x => x.UserId == userId && x.ConsumedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ConsumedAt, now), ct);
    }
    private void EnsureReady(string channel) { EnsureKey(); delivery.EnsureConfigured(channel); }
    private void EnsureKey()
    {
        if (Encoding.UTF8.GetByteCount(options.Value.HashKey) < 32)
            throw new AppException("Dịch vụ khôi phục chưa được cấu hình.", "RECOVERY_NOT_CONFIGURED", HttpStatusCode.ServiceUnavailable);
    }
    private string HashCode(Guid id, string purpose, string code) => Convert.ToHexString(HMACSHA256.HashData(
        Encoding.UTF8.GetBytes(options.Value.HashKey), Encoding.UTF8.GetBytes($"{id:N}:{purpose}:{code}")));
    private static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    private static bool Matches(string expected, string actual) => CryptographicOperations.FixedTimeEquals(
        Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(actual));
    private static AppException InvalidCode() => new("Mã xác minh không hợp lệ, đã dùng, hết hạn hoặc vượt số lần thử.", "RECOVERY_INVALID", HttpStatusCode.BadRequest);
}
