using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PhoneStore.BuildingBlocks.Exceptions;
using PhoneStore.Modules.Identity.Infrastructure.Persistence;
using PhoneStore.Modules.Identity.Infrastructure.Services;
using Xunit;

namespace PhoneStore.IntegrationTests;

[Collection(ApiTestCollection.Name)]
public sealed class RecoveryCustomerAuditTests(PhoneStoreApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly FakeDelivery _delivery = new();

    private WebApplicationFactory<Program> RecoveryHost() => factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
    {
        services.AddSingleton<IRecoveryDelivery>(_delivery);
        var key = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        services.PostConfigure<RecoveryOptions>(options => options.HashKey = key);
    }));
    private static async Task<JsonElement> Data(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("data");
    }
    private static async Task<JsonElement> Profile(HttpClient owner) => await Data(await owner.GetAsync("/api/v1/me"));
    private static async Task<Guid> Request(HttpClient client, string email, string channel = "email") =>
        (await Data(await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email, channel }, Json))).GetProperty("challengeId").GetGuid();
    private async Task<string> Verify(HttpClient client, Guid id, string destination) =>
        (await Data(await client.PostAsJsonAsync("/api/v1/auth/verify-reset-otp", new { challengeId = id, code = _delivery.Messages[destination] }, Json)))
            .GetProperty("resetToken").GetString()!;

    [Fact]
    public async Task Email_recovery_revokes_old_sessions_and_reset_token_is_single_use()
    {
        using var owner = await AuthTestHelper.CreateAuthenticatedCustomerAsync(factory);
        var profile = await Profile(owner);
        var email = profile.GetProperty("email").GetString()!;
        using var host = RecoveryHost(); using var client = host.CreateClient();
        var id = await Request(client, email);
        var token = await Verify(client, id, email);
        var payload = new { challengeId = id, resetToken = token, newPassword = "Recovered@123" };
        (await client.PostAsJsonAsync("/api/v1/auth/reset-password", payload, Json)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await owner.GetAsync("/api/v1/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsJsonAsync("/api/v1/auth/reset-password", payload, Json)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Pass@123" }, Json)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Recovered@123" }, Json)).StatusCode.Should().Be(HttpStatusCode.OK);
        using var admin = await AuthTestHelper.CreateAuthenticatedAdminAsync(factory);
        var audit = await Data(await admin.GetAsync($"/api/v1/admin/audit?targetId={profile.GetProperty("id").GetInt64()}&action=PasswordRecovered"));
        audit.GetProperty("totalCount").GetInt64().Should().Be(1);
        audit.GetRawText().Should().NotContain(token).And.NotContain("Recovered@123").And.NotContain(_delivery.Messages[email]);
    }

    [Fact]
    public async Task Wrong_otp_attempts_persist_and_lock_out_the_correct_code()
    {
        using var owner = await AuthTestHelper.CreateAuthenticatedCustomerAsync(factory);
        var email = (await Profile(owner)).GetProperty("email").GetString()!;
        using var host = RecoveryHost(); using var client = host.CreateClient();
        var id = await Request(client, email);
        var wrong = _delivery.Messages[email] == "000000" ? "111111" : "000000";
        for (var i = 0; i < 5; i++)
            (await client.PostAsJsonAsync("/api/v1/auth/verify-reset-otp", new { challengeId = id, code = wrong }, Json)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.PostAsJsonAsync("/api/v1/auth/verify-reset-otp", new { challengeId = id, code = _delivery.Messages[email] }, Json)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var scope = factory.Services.CreateScope();
        var row = await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().VerificationChallenges.AsNoTracking().SingleAsync(x => x.Id == id);
        row.Attempts.Should().Be(5);
        row.CodeHash.Should().HaveLength(64).And.NotBe(_delivery.Messages[email]);
    }

    [Fact]
    public async Task Expired_otp_expired_reset_token_wrong_token_and_blank_password_are_rejected()
    {
        using var owner = await AuthTestHelper.CreateAuthenticatedCustomerAsync(factory);
        var email = (await Profile(owner)).GetProperty("email").GetString()!;
        using var host = RecoveryHost(); using var client = host.CreateClient();
        var id = await Request(client, email);
        var token = await Verify(client, id, email);
        (await client.PostAsJsonAsync("/api/v1/auth/reset-password", new { challengeId = id, resetToken = token, newPassword = "" }, Json))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.PostAsJsonAsync("/api/v1/auth/reset-password", new { challengeId = id, resetToken = new string('A', 64), newPassword = "Next@123" }, Json))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.VerificationChallenges.Where(x => x.Id == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.ExpiresAt, DateTimeOffset.UtcNow.AddMinutes(-1)));
        (await client.PostAsJsonAsync("/api/v1/auth/reset-password", new { challengeId = id, resetToken = token, newPassword = "Next@123" }, Json))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        // A separate account exercises OTP expiration before verification.
        using var other = await AuthTestHelper.CreateAuthenticatedCustomerAsync(factory);
        var otherEmail = (await Profile(other)).GetProperty("email").GetString()!;
        var otherId = await Request(client, otherEmail);
        await db.VerificationChallenges.Where(x => x.Id == otherId).ExecuteUpdateAsync(s => s.SetProperty(x => x.ExpiresAt, DateTimeOffset.UtcNow.AddMinutes(-1)));
        (await client.PostAsJsonAsync("/api/v1/auth/verify-reset-otp", new { challengeId = otherId, code = _delivery.Messages[otherEmail] }, Json))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Concurrent_reset_requests_allow_only_one_password_change()
    {
        using var owner = await AuthTestHelper.CreateAuthenticatedCustomerAsync(factory);
        var email = (await Profile(owner)).GetProperty("email").GetString()!;
        using var host = RecoveryHost(); using var first = host.CreateClient(); using var second = host.CreateClient();
        var id = await Request(first, email); var token = await Verify(first, id, email);
        var payload = new { challengeId = id, resetToken = token, newPassword = "Concurrent@123" };
        var responses = await Task.WhenAll(first.PostAsJsonAsync("/api/v1/auth/reset-password", payload, Json), second.PostAsJsonAsync("/api/v1/auth/reset-password", payload, Json));
        responses.Count(x => x.StatusCode == HttpStatusCode.OK).Should().Be(1);
        responses.Count(x => x.StatusCode == HttpStatusCode.BadRequest).Should().Be(1);
    }

    [Fact]
    public async Task Unknown_locked_and_unverified_sms_accounts_have_generic_responses_without_delivery()
    {
        using var owner = await AuthTestHelper.CreateAuthenticatedCustomerAsync(factory);
        var profile = await Profile(owner); var email = profile.GetProperty("email").GetString()!;
        using var host = RecoveryHost(); using var client = host.CreateClient();
        await Request(client, AuthTestHelper.NewEmail());
        await Request(client, email, "sms");
        using var admin = await AuthTestHelper.CreateAuthenticatedAdminAsync(factory);
        (await admin.PatchAsJsonAsync($"/api/v1/admin/customers/{profile.GetProperty("id").GetInt64()}/lock", new { isLocked = true, reason = "Test lock" }, Json))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        await Request(client, email);
        _delivery.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task Resend_cooldown_prevents_multiple_deliveries_and_provider_failure_rolls_back_challenge()
    {
        using var owner = await AuthTestHelper.CreateAuthenticatedCustomerAsync(factory);
        var profile = await Profile(owner); var email = profile.GetProperty("email").GetString()!;
        using var host = RecoveryHost(); using var client = host.CreateClient();
        await Request(client, email); await Request(client, email);
        _delivery.Count.Should().Be(1);
        using var secondOwner = await AuthTestHelper.CreateAuthenticatedCustomerAsync(factory);
        var secondProfile = await Profile(secondOwner);
        _delivery.Fail = true;
        (await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email = secondProfile.GetProperty("email").GetString() }, Json))
            .StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        using var scope = factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().VerificationChallenges.CountAsync(x => x.UserId == secondProfile.GetProperty("id").GetInt64()))
            .Should().Be(0);
    }

    [Fact]
    public async Task Unconfigured_provider_returns_503_even_for_unknown_accounts()
    {
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.PostConfigure<RecoveryOptions>(o => { o.HashKey = ""; o.Smtp.Host = ""; o.Sms.AccountSid = ""; })));
        using var client = host.CreateClient();
        (await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email = AuthTestHelper.NewEmail() }, Json)).StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task Verified_phone_enables_sms_recovery_but_cannot_be_reused_for_another_purpose_or_owner()
    {
        using var owner = await AuthTestHelper.CreateAuthenticatedCustomerAsync(factory);
        using var stranger = await AuthTestHelper.CreateAuthenticatedCustomerAsync(factory);
        var phone = "0" + RandomNumberGenerator.GetInt32(100_000_000, 999_999_999);
        await owner.PatchAsJsonAsync("/api/v1/me", new { fullName = "SMS Owner", phone }, Json);
        var email = (await Profile(owner)).GetProperty("email").GetString()!;
        using var host = RecoveryHost(); using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = owner.DefaultRequestHeaders.Authorization;
        var challenge = await Data(await client.PostAsJsonAsync("/api/v1/me/phone-verification", new { currentPassword = "Pass@123" }, Json));
        var id = challenge.GetProperty("challengeId").GetGuid();
        var code = _delivery.Messages["+84" + phone[1..]];
        (await client.PostAsJsonAsync("/api/v1/auth/verify-reset-otp", new { challengeId = id, code }, Json)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        client.DefaultRequestHeaders.Authorization = stranger.DefaultRequestHeaders.Authorization;
        (await client.PostAsJsonAsync("/api/v1/me/phone-verification/confirm", new { challengeId = id, code }, Json)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        client.DefaultRequestHeaders.Authorization = owner.DefaultRequestHeaders.Authorization;
        (await client.PostAsJsonAsync("/api/v1/me/phone-verification/confirm", new { challengeId = id, code }, Json)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Profile(owner)).GetProperty("phoneVerified").GetBoolean().Should().BeTrue();
        var recoveryId = await Request(client, email, "sms");
        var token = await Verify(client, recoveryId, "+84" + phone[1..]);
        // Changing phone removes verification and invalidates a previously authorized recovery.
        await owner.PatchAsJsonAsync("/api/v1/me", new { fullName = "SMS Owner", phone = "0987654321" }, Json);
        (await Profile(owner)).GetProperty("phoneVerified").GetBoolean().Should().BeFalse();
        (await client.PostAsJsonAsync("/api/v1/auth/reset-password", new { challengeId = recoveryId, resetToken = token, newPassword = "Sms@1234" }, Json)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Customer_crud_rejects_elevation_stale_updates_staff_targets_and_keeps_address_history()
    {
        using var admin = await AuthTestHelper.CreateAuthenticatedAdminAsync(factory);
        var email = AuthTestHelper.NewEmail();
        var created = await Data(await admin.PostAsJsonAsync("/api/v1/admin/customers", new { email, password = "Customer@123", fullName = "Customer", role = "ADMIN", isActive = false }, Json));
        created.GetProperty("role").GetString().Should().Be("CUSTOMER");
        created.GetProperty("isActive").GetBoolean().Should().BeTrue();
        var id = created.GetProperty("id").GetInt64();
        (await admin.PostAsJsonAsync("/api/v1/admin/customers", new { email, password = "Customer@123", fullName = "Duplicate" }, Json)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        using var owner = await AuthTestHelper.CreateAuthenticatedClientAsync(factory, email, "Customer@123");
        await owner.PostAsJsonAsync("/api/v1/me/addresses", new { receiverName = "Name", receiverPhone = "0912345678", addressText = "Retained address" }, Json);
        var detail = await Data(await admin.GetAsync($"/api/v1/admin/customers/{id}"));
        var version = detail.GetProperty("profile").GetProperty("updatedAt").GetDateTimeOffset();
        var update = new { fullName = "Updated", phone = "0912345678", expectedUpdatedAt = version, reason = "Correct customer profile", role = "ADMIN" };
        (await admin.PatchAsJsonAsync($"/api/v1/admin/customers/{id}", update, Json)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.PatchAsJsonAsync($"/api/v1/admin/customers/{id}", update, Json)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await admin.PatchAsJsonAsync($"/api/v1/admin/customers/{id}/lock", new { isLocked = true, reason = "Requested deactivation" }, Json)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await owner.GetAsync("/api/v1/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var lockedDetail = await Data(await admin.GetAsync($"/api/v1/admin/customers/{id}"));
        lockedDetail.GetProperty("addresses").GetArrayLength().Should().Be(1);
        lockedDetail.GetProperty("profile").GetProperty("role").GetString().Should().Be("CUSTOMER");
        (await admin.PatchAsJsonAsync($"/api/v1/admin/customers/{id}/lock", new { isLocked = false, reason = "Reactivation requested" }, Json)).StatusCode.Should().Be(HttpStatusCode.OK);
        var adminId = (await Profile(admin)).GetProperty("id").GetInt64();
        (await admin.GetAsync($"/api/v1/admin/customers/{adminId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await admin.GetAsync("/api/v1/admin/customers?sort=passwordHash")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.GetAsync("/api/v1/admin/customers?page=2147483647&pageSize=100")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var filtered = await Data(await admin.GetAsync($"/api/v1/admin/customers?search={Uri.EscapeDataString(email)}&sort=email&isActive=true"));
        filtered.GetProperty("totalCount").GetInt64().Should().Be(1);
        var audit = await Data(await admin.GetAsync($"/api/v1/admin/audit?targetId={id}"));
        audit.GetProperty("totalCount").GetInt64().Should().Be(4);
    }

    [Fact]
    public async Task Customer_and_staff_cannot_read_or_mutate_customer_administration_or_audit()
    {
        using var customer = await AuthTestHelper.CreateAuthenticatedCustomerAsync(factory);
        using var staff = await AuthTestHelper.CreateAuthenticatedStaffAsync(factory);
        using var anonymous = factory.CreateClient();
        foreach (var client in new[] { customer, staff, anonymous })
        {
            var expected = client == anonymous ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden;
            (await client.GetAsync("/api/v1/admin/customers")).StatusCode.Should().Be(expected);
            (await client.GetAsync("/api/v1/admin/audit")).StatusCode.Should().Be(expected);
            (await client.GetAsync("/api/v1/admin/customers/1")).StatusCode.Should().Be(expected);
            (await client.PostAsJsonAsync("/api/v1/admin/customers/1/revoke-sessions", new { }, Json)).StatusCode.Should().Be(expected);
        }
    }

    [Fact]
    public async Task Failed_customer_update_rolls_back_without_audit_and_revoke_ends_sessions()
    {
        using var owner = await AuthTestHelper.CreateAuthenticatedCustomerAsync(factory);
        var id = (await Profile(owner)).GetProperty("id").GetInt64();
        using var admin = await AuthTestHelper.CreateAuthenticatedAdminAsync(factory);
        (await admin.PatchAsJsonAsync($"/api/v1/admin/customers/{id}", new { fullName = "Should not save", expectedUpdatedAt = DateTimeOffset.UtcNow.AddYears(-1), reason = "Stale" }, Json)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        var audit = await Data(await admin.GetAsync($"/api/v1/admin/audit?targetId={id}&action=CustomerUpdated"));
        audit.GetProperty("totalCount").GetInt64().Should().Be(0);
        (await admin.PostAsJsonAsync($"/api/v1/admin/customers/{id}/revoke-sessions", new { }, Json)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await owner.GetAsync("/api/v1/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var response = await admin.GetAsync("/api/v1/admin/audit");
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        (await admin.GetAsync("/api/v1/admin/audit?from=2026-01-01T07%3A00%3A00%2B07%3A00&to=2027-01-01T07%3A00%3A00%2B07%3A00"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Audit_history_rejects_update_delete_and_truncate_at_database_level()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        foreach (var sql in new[] { "UPDATE identity.audit_entries SET \"Action\" = 'altered' WHERE false",
            "DELETE FROM identity.audit_entries WHERE false", "TRUNCATE identity.audit_entries" })
        {
            var action = () => db.Database.ExecuteSqlRawAsync(sql);
            var failure = await action.Should().ThrowAsync<Npgsql.PostgresException>();
            failure.Which.SqlState.Should().Be("42501");
        }
    }

    [Fact]
    public async Task Customer_lock_rolls_back_session_revocation_if_audit_insert_fails()
    {
        using var owner = await AuthTestHelper.CreateAuthenticatedCustomerAsync(factory);
        var id = (await Profile(owner)).GetProperty("id").GetInt64();
        using var admin = await AuthTestHelper.CreateAuthenticatedAdminAsync(factory);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        // Failure injection is scoped to this randomly generated test database/target only.
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION identity.test_reject_audit_insert() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                IF NEW."Reason" = 'Test rollback' AND NEW."Action" = 'CustomerLocked' THEN RAISE EXCEPTION 'test audit storage failure'; END IF;
                RETURN NEW;
            END; $$;
            CREATE TRIGGER test_reject_audit_insert BEFORE INSERT ON identity.audit_entries
            FOR EACH ROW EXECUTE FUNCTION identity.test_reject_audit_insert();
            """);
        try
        {
            (await admin.PatchAsJsonAsync($"/api/v1/admin/customers/{id}/lock", new { isLocked = true, reason = "Test rollback" }, Json))
                .StatusCode.Should().Be(HttpStatusCode.InternalServerError);
            (await Profile(owner)).GetProperty("isActive").GetBoolean().Should().BeTrue();
            (await db.AuditEntries.CountAsync(x => x.TargetId == id && x.Action == "CustomerLocked")).Should().Be(0);
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER test_reject_audit_insert ON identity.audit_entries; DROP FUNCTION identity.test_reject_audit_insert();");
        }
    }

    [Fact]
    public async Task Verified_phone_uniqueness_is_enforced_and_old_otp_is_invalid_after_password_change()
    {
        using var owner = await AuthTestHelper.CreateAuthenticatedCustomerAsync(factory);
        using var other = await AuthTestHelper.CreateAuthenticatedCustomerAsync(factory);
        var phone = "0" + RandomNumberGenerator.GetInt32(100_000_000, 999_999_999);
        await owner.PatchAsJsonAsync("/api/v1/me", new { fullName = "First", phone }, Json);
        await other.PatchAsJsonAsync("/api/v1/me", new { fullName = "Other", phone }, Json);
        using var host = RecoveryHost(); using var client = host.CreateClient();
        foreach (var person in new[] { owner, other })
        {
            client.DefaultRequestHeaders.Authorization = person.DefaultRequestHeaders.Authorization;
            var row = await Data(await client.PostAsJsonAsync("/api/v1/me/phone-verification", new { currentPassword = "Pass@123" }, Json));
            var result = await client.PostAsJsonAsync("/api/v1/me/phone-verification/confirm", new { challengeId = row.GetProperty("challengeId").GetGuid(), code = _delivery.Messages["+84" + phone[1..]] }, Json);
            result.StatusCode.Should().Be(person == owner ? HttpStatusCode.OK : HttpStatusCode.Conflict);
        }
        var email = (await Profile(owner)).GetProperty("email").GetString()!;
        var id = await Request(client, email);
        (await owner.PostAsJsonAsync("/api/v1/me/change-password", new { currentPassword = "Pass@123", newPassword = "Changed@123" }, Json)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsJsonAsync("/api/v1/auth/verify-reset-otp", new { challengeId = id, code = _delivery.Messages[email] }, Json)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Customer_password_reset_revokes_sessions_and_does_not_accept_staff_target()
    {
        using var owner = await AuthTestHelper.CreateAuthenticatedCustomerAsync(factory);
        var profile = await Profile(owner); var id = profile.GetProperty("id").GetInt64();
        using var admin = await AuthTestHelper.CreateAuthenticatedAdminAsync(factory);
        (await admin.PostAsJsonAsync($"/api/v1/admin/customers/{id}/reset-password", new { newPassword = "NewCustomer@123" }, Json)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await owner.GetAsync("/api/v1/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        using var fresh = await AuthTestHelper.CreateAuthenticatedClientAsync(factory, profile.GetProperty("email").GetString()!, "NewCustomer@123");
        var adminId = (await Profile(admin)).GetProperty("id").GetInt64();
        (await admin.PostAsJsonAsync($"/api/v1/admin/customers/{adminId}/reset-password", new { newPassword = "CannotApply@123" }, Json)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private sealed class FakeDelivery : IRecoveryDelivery
    {
        public ConcurrentDictionary<string, string> Messages { get; } = new();
        public int Count;
        public bool Fail;
        public void EnsureConfigured(string channel) { }
        public Task SendAsync(string channel, string destination, string code, CancellationToken ct)
        {
            if (Fail) throw new AppException("Provider rejected test delivery.", "RECOVERY_DELIVERY_FAILED", HttpStatusCode.ServiceUnavailable);
            Messages[destination] = code; Interlocked.Increment(ref Count);
            return Task.CompletedTask;
        }
    }
}
