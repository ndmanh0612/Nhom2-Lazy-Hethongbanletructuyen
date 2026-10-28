using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PhoneStore.BuildingBlocks.Application;
using PhoneStore.BuildingBlocks.Common;
using PhoneStore.BuildingBlocks.Infrastructure.Identity;
using PhoneStore.Modules.Identity.Application.DTOs;
using PhoneStore.Modules.Identity.Application.Services;
using PhoneStore.Modules.Identity.Infrastructure.Persistence;
using PhoneStore.Modules.Identity.Infrastructure.Services;

namespace PhoneStore.IntegrationTests;

[Collection(ApiTestCollection.Name)]
public class IdentitySessionRegressionTests
{
    private readonly PhoneStoreApiFactory _factory;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public IdentitySessionRegressionTests(PhoneStoreApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Logout_invalidates_access_token_immediately_and_is_repeatable()
    {
        using var client = await AuthTestHelper.CreateAuthenticatedCustomerAsync(_factory);
        (await client.GetAsync("/api/v1/me")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsJsonAsync("/api/v1/auth/logout", new { }, Json)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/api/v1/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsJsonAsync("/api/v1/auth/logout", new { }, Json)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Logout_does_not_revoke_other_devices()
    {
        var email = AuthTestHelper.NewEmail();
        using var first = await AuthTestHelper.CreateAuthenticatedCustomerAsync(_factory, email);
        using var second = await AuthTestHelper.CreateAuthenticatedClientAsync(_factory, email, "Pass@123");
        (await first.PostAsJsonAsync("/api/v1/auth/logout", new { }, Json)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await first.GetAsync("/api/v1/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await second.GetAsync("/api/v1/me")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Register_and_token_persistence_are_one_transaction()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var store = new RefreshTokenStore(db);
        var service = new IdentityService(db, hasher, new FailingTokenService(), store);
        var email = AuthTestHelper.NewEmail();

        var action = () => service.RegisterAsync(new RegisterRequest(email, "Pass@123", "Transaction Test", null));
        await action.Should().ThrowAsync<InvalidOperationException>();
        db.ChangeTracker.Clear();
        (await db.Users.AnyAsync(user => user.Email == email)).Should().BeFalse();
    }

    [Fact]
    public async Task Failed_refresh_rolls_back_consumption_of_the_old_token()
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email = AuthTestHelper.NewEmail(), password = "Pass@123", fullName = "Refresh Rollback"
        }, Json);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var raw = ReadCookie(response, AuthCookies.RefreshToken);
        var hash = JwtTokenService.HashToken(raw);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var service = new IdentityService(db, scope.ServiceProvider.GetRequiredService<IPasswordHasher>(),
            new FailingTokenService(), new RefreshTokenStore(db));

        var action = () => service.RefreshAsync(raw);
        await action.Should().ThrowAsync<InvalidOperationException>();
        db.ChangeTracker.Clear();
        (await db.RefreshTokens.SingleAsync(token => token.TokenHash == hash)).IsRevoked.Should().BeFalse();
    }

    [Fact]
    public async Task Parallel_refresh_of_the_same_token_has_only_one_winner()
    {
        using var client = _factory.CreateClient();
        var registration = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email = AuthTestHelper.NewEmail(), password = "Pass@123", fullName = "Parallel Refresh"
        }, Json);
        registration.StatusCode.Should().Be(HttpStatusCode.Created);
        var refresh = ReadCookie(registration, AuthCookies.RefreshToken);
        var csrf = ReadCookie(registration, AuthCookies.CsrfToken);
        var hash = JwtTokenService.HashToken(refresh);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        // Only this random test database is modified. Slow the matching UPDATE so both
        // requests overlap instead of relying on an accidental thread scheduling race.
        var suffix = Convert.ToHexString(RandomNumberGenerator.GetBytes(5)).ToLowerInvariant();
        var function = $"test_refresh_{suffix}";
        var sql = $"""
            CREATE FUNCTION identity.{function}() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                IF OLD."TokenHash" = '{hash}' THEN PERFORM pg_sleep(0.2); END IF;
                RETURN NEW;
            END; $$;
            CREATE TRIGGER {function} BEFORE UPDATE ON identity.refresh_tokens
            FOR EACH ROW EXECUTE FUNCTION identity.{function}();
            """;
        await db.Database.ExecuteSqlRawAsync(sql);
        try
        {
            async Task<HttpResponseMessage> RefreshAsync()
            {
                using var device = _factory.CreateClient(new() { HandleCookies = false });
                device.DefaultRequestHeaders.Add("Cookie", $"{AuthCookies.RefreshToken}={Uri.EscapeDataString(refresh)}; {AuthCookies.CsrfToken}={csrf}");
                device.DefaultRequestHeaders.Add(AuthHeaders.CsrfToken, csrf);
                return await device.PostAsJsonAsync("/api/v1/auth/refresh", new { }, Json);
            }
            var responses = await Task.WhenAll(RefreshAsync(), RefreshAsync());
            responses.Count(result => result.StatusCode == HttpStatusCode.OK).Should().Be(1);
            responses.Count(result => result.StatusCode == HttpStatusCode.Unauthorized).Should().Be(1);
            foreach (var result in responses) result.Dispose();
        }
        finally
        {
            var cleanupSql = $"DROP TRIGGER {function} ON identity.refresh_tokens; DROP FUNCTION identity.{function}();";
            await db.Database.ExecuteSqlRawAsync(cleanupSql);
        }
    }

    [Fact]
    public async Task Concurrent_registration_returns_created_or_conflict_not_internal_error()
    {
        var email = AuthTestHelper.NewEmail();
        using var first = _factory.CreateClient();
        using var second = _factory.CreateClient();
        var body = new { email, password = "Pass@123", fullName = "Concurrent User" };
        var responses = await Task.WhenAll(
            first.PostAsJsonAsync("/api/v1/auth/register", body, Json),
            second.PostAsJsonAsync("/api/v1/auth/register", body, Json));
        responses.Count(result => result.StatusCode == HttpStatusCode.Created).Should().Be(1);
        responses.Count(result => result.StatusCode == HttpStatusCode.Conflict).Should().Be(1);
        foreach (var result in responses) result.Dispose();
    }

    [Fact]
    public async Task Untrusted_origin_cannot_register_and_auth_responses_are_not_cached()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Origin", "https://untrusted.example");
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email = AuthTestHelper.NewEmail(), password = "Pass@123", fullName = "Origin Test"
        }, Json);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        response.Headers.CacheControl?.NoStore.Should().BeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Logout_works_without_a_valid_access_token_when_cookie_and_csrf_are_present(bool sendInvalidToken)
    {
        using var client = await AuthTestHelper.CreateAuthenticatedCustomerAsync(_factory);
        var original = client.DefaultRequestHeaders.Authorization;
        client.DefaultRequestHeaders.Authorization = sendInvalidToken
            ? new AuthenticationHeaderValue("Bearer", "invalid-jwt") : null;

        (await client.PostAsJsonAsync("/api/v1/auth/logout", new { }, Json)).StatusCode.Should().Be(HttpStatusCode.OK);
        client.DefaultRequestHeaders.Authorization = original;
        (await client.GetAsync("/api/v1/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Bearer_only_logout_revokes_the_session_referenced_by_the_signed_claim()
    {
        using var original = await AuthTestHelper.CreateAuthenticatedCustomerAsync(_factory);
        var csrfResponse = await original.GetAsync("/api/v1/auth/csrf");
        var csrf = ReadCookie(csrfResponse, AuthCookies.CsrfToken);
        using var device = _factory.CreateClient(new() { HandleCookies = false });
        device.DefaultRequestHeaders.Authorization = original.DefaultRequestHeaders.Authorization;
        device.DefaultRequestHeaders.Add("Cookie", $"{AuthCookies.CsrfToken}={csrf}");
        device.DefaultRequestHeaders.Add(AuthHeaders.CsrfToken, csrf);

        (await device.PostAsJsonAsync("/api/v1/auth/logout", new { }, Json)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await original.GetAsync("/api/v1/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_replaces_both_tokens_and_only_the_new_access_token_is_accepted()
    {
        using var client = await AuthTestHelper.CreateAuthenticatedCustomerAsync(_factory);
        var refresh = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { }, Json);
        refresh.StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/api/v1/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var payload = await refresh.Content.ReadFromJsonAsync<JsonElement>(Json);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            payload.GetProperty("data").GetProperty("accessToken").GetString());
        (await client.GetAsync("/api/v1/me")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("refresh")]
    [InlineData("logout")]
    public async Task Cookie_authenticated_actions_reject_missing_csrf_header(string action)
    {
        using var client = await AuthTestHelper.CreateAuthenticatedCustomerAsync(_factory);
        client.DefaultRequestHeaders.Remove(AuthHeaders.CsrfToken);
        var response = await client.PostAsJsonAsync($"/api/v1/auth/{action}", new { }, Json);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.GetAsync("/api/v1/me")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Same_origin_registration_is_allowed_and_response_is_not_cached()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Origin", client.BaseAddress!.GetLeftPart(UriPartial.Authority));
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email = AuthTestHelper.NewEmail(), password = "Pass@123", fullName = "Same Origin"
        }, Json);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.CacheControl?.NoStore.Should().BeTrue();
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        payload.GetProperty("data").TryGetProperty("refreshToken", out _).Should().BeFalse();
    }

    [Fact]
    public async Task Expired_database_session_rejects_both_access_and_refresh_tokens()
    {
        var email = AuthTestHelper.NewEmail();
        using var client = await AuthTestHelper.CreateAuthenticatedCustomerAsync(_factory, email);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var userId = await db.Users.Where(user => user.Email == email).Select(user => user.Id).SingleAsync();
        await db.RefreshTokens.Where(token => token.UserId == userId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.ExpiresAt, DateTimeOffset.UtcNow.AddMinutes(-1)));

        (await client.GetAsync("/api/v1/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsJsonAsync("/api/v1/auth/refresh", new { }, Json)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("password")]
    [InlineData("role")]
    [InlineData("lock")]
    [InlineData("reset-password")]
    public async Task Account_security_changes_invalidate_all_devices(string action)
    {
        var email = AuthTestHelper.NewEmail();
        using var first = await AuthTestHelper.CreateAuthenticatedStaffAsync(_factory, email);
        using var second = await AuthTestHelper.CreateAuthenticatedClientAsync(_factory, email, "Staff@123");
        var profile = await (await first.GetAsync("/api/v1/me")).Content.ReadFromJsonAsync<JsonElement>(Json);
        var userId = profile.GetProperty("data").GetProperty("id").GetInt64();
        using var admin = await AuthTestHelper.CreateAuthenticatedAdminAsync(_factory);

        using var response = action switch
        {
            "password" => await first.PostAsJsonAsync("/api/v1/me/change-password",
                new { currentPassword = "Staff@123", newPassword = "Changed@123" }, Json),
            "role" => await admin.PatchAsJsonAsync($"/api/v1/admin/staff/{userId}/role", new { role = "ADMIN" }, Json),
            "lock" => await admin.PatchAsJsonAsync($"/api/v1/admin/staff/{userId}/lock", new { isLocked = true, reason = "Test" }, Json),
            _ => await admin.PostAsJsonAsync($"/api/v1/admin/staff/{userId}/reset-password", new { newPassword = "Changed@123" }, Json)
        };
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await first.GetAsync("/api/v1/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await second.GetAsync("/api/v1/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await second.PostAsJsonAsync("/api/v1/auth/refresh", new { }, Json)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    internal static string ReadCookie(HttpResponseMessage response, string name)
    {
        var cookie = response.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith(name + "=", StringComparison.Ordinal));
        return Uri.UnescapeDataString(cookie.Split(';')[0].Split('=', 2)[1]);
    }

    private sealed class FailingTokenService : ITokenService
    {
        public Task<TokenPair> CreateTokenPairAsync(long userId, string email, string role, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Forced token persistence failure for regression test.");
    }
}
