using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PhoneStore.Modules.Identity.Application.DTOs;
using PhoneStore.Modules.Identity.Application.Services;
using PhoneStore.Modules.Identity.Domain.Entities;
using PhoneStore.Modules.Identity.Infrastructure.Persistence;
using PhoneStore.Modules.Identity.Infrastructure.Services;
using Xunit;
using ForbiddenException = PhoneStore.BuildingBlocks.Exceptions.ForbiddenException;

namespace PhoneStore.IntegrationTests;

[Collection(ApiTestCollection.Name)]
public class AccountAdministrationRegressionTests
{
    private readonly PhoneStoreApiFactory _factory;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public AccountAdministrationRegressionTests(PhoneStoreApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Address_owner_can_update_and_delete_but_other_users_receive_404()
    {
        using var owner = await AuthTestHelper.CreateAuthenticatedCustomerAsync(_factory);
        using var stranger = await AuthTestHelper.CreateAuthenticatedCustomerAsync(_factory);
        var created = await owner.PostAsJsonAsync("/api/v1/me/addresses",
            new { receiverName = "Owner", receiverPhone = "0912345678", addressText = "Old address" }, Json);
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("data").GetProperty("id").GetInt64();
        var path = $"/api/v1/me/addresses/{id}";
        var updated = new { receiverName = "New owner", receiverPhone = "0987654321", addressText = "New address" };
        (await stranger.PutAsJsonAsync(path, updated, Json)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await stranger.DeleteAsync(path)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await owner.PutAsJsonAsync(path, updated, Json)).StatusCode.Should().Be(HttpStatusCode.OK);
        var addresses = await owner.GetFromJsonAsync<JsonElement>("/api/v1/me/addresses", Json);
        addresses.GetProperty("data").EnumerateArray().Single(a => a.GetProperty("id").GetInt64() == id)
            .GetProperty("addressText").GetString().Should().Be("New address");
        (await owner.DeleteAsync(path)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await owner.DeleteAsync(path)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Profile_payload_cannot_promote_or_deactivate_the_customer()
    {
        using var client = await AuthTestHelper.CreateAuthenticatedCustomerAsync(_factory);
        var result = await client.PatchAsJsonAsync("/api/v1/me",
            new { fullName = "Safe Name", phone = (string?)null, role = "ADMIN", isActive = false, passwordHash = "injected" }, Json);
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        var data = (await result.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("data");
        data.GetProperty("role").GetString().Should().Be("CUSTOMER");
        data.GetProperty("isActive").GetBoolean().Should().BeTrue();
        (await client.GetAsync("/api/v1/admin/staff")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("99")]
    [InlineData("CUSTOMER")]
    public async Task Invalid_staff_roles_are_400_not_internal_errors(string? role)
    {
        using var admin = await AuthTestHelper.CreateAuthenticatedAdminAsync(_factory);
        var result = await admin.PostAsJsonAsync("/api/v1/admin/staff",
            new { email = AuthTestHelper.NewEmail(), password = "Staff@123", fullName = "Name", role }, Json);
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await result.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("code").GetString().Should().Be("VALIDATION_ERROR");
    }

    [Fact]
    public async Task Customer_cannot_be_promoted_through_the_staff_role_endpoint()
    {
        using var customer = await AuthTestHelper.CreateAuthenticatedCustomerAsync(_factory);
        var profile = await customer.GetFromJsonAsync<JsonElement>("/api/v1/me", Json);
        var customerId = profile.GetProperty("data").GetProperty("id").GetInt64();
        using var admin = await AuthTestHelper.CreateAuthenticatedAdminAsync(_factory);
        (await admin.PatchAsJsonAsync($"/api/v1/admin/staff/{customerId}/role", new { role = "ADMIN" }, Json))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await customer.GetAsync("/api/v1/me")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Concurrent_staff_creation_returns_one_created_and_one_conflict()
    {
        using var first = await AuthTestHelper.CreateAuthenticatedAdminAsync(_factory);
        using var second = await AuthTestHelper.CreateAuthenticatedAdminAsync(_factory);
        var request = new { email = AuthTestHelper.NewEmail(), password = "Staff@123", fullName = "Concurrent Staff", role = "STAFF" };
        var results = await Task.WhenAll(first.PostAsJsonAsync("/api/v1/admin/staff", request, Json),
            second.PostAsJsonAsync("/api/v1/admin/staff", request, Json));
        results.Count(r => r.StatusCode == HttpStatusCode.Created).Should().Be(1);
        results.Count(r => r.StatusCode == HttpStatusCode.Conflict).Should().Be(1);
        foreach (var result in results) result.Dispose();
    }

    [Fact]
    public async Task Huge_staff_page_is_rejected_before_integer_overflow()
    {
        using var admin = await AuthTestHelper.CreateAuthenticatedAdminAsync(_factory);
        (await admin.GetAsync("/api/v1/admin/staff?page=2147483647&pageSize=100"))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Revoked_admin_cannot_apply_a_mutation_after_endpoint_authorization()
    {
        using var admin = await AuthTestHelper.CreateAuthenticatedAdminAsync(_factory);
        var created = await CreateStaffAsync(admin, "ADMIN");
        var actorId = created.Id;
        var target = await CreateStaffAsync(admin, "STAFF");
        (await admin.PatchAsJsonAsync($"/api/v1/admin/staff/{actorId}/lock", new { isLocked = true }, Json))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IIdentityService>();
        var action = () => service.LockAccountAsync(target.Id, new LockAccountRequest(true, null), actorId);
        await action.Should().ThrowAsync<ForbiddenException>();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        (await db.Users.AsNoTracking().SingleAsync(u => u.Id == target.Id)).IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Concurrent_admin_locks_preserve_an_active_administrator()
    {
        using var seedAdmin = await AuthTestHelper.CreateAuthenticatedAdminAsync(_factory);
        var first = await CreateStaffAsync(seedAdmin, "ADMIN");
        var second = await CreateStaffAsync(seedAdmin, "ADMIN");
        using var firstClient = await AuthTestHelper.CreateAuthenticatedClientAsync(_factory, first.Email, "Staff@123");
        using var secondClient = await AuthTestHelper.CreateAuthenticatedClientAsync(_factory, second.Email, "Staff@123");
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var originalActiveIds = await db.Users.Where(u => u.Role == UserRole.ADMIN && u.IsActive
            && u.Id != first.Id && u.Id != second.Id).Select(u => u.Id).ToListAsync();
        // Only the fixture's randomly named test database is modified; always restore seed admins.
        try
        {
            await db.Users.Where(u => originalActiveIds.Contains(u.Id))
                .ExecuteUpdateAsync(setters => setters.SetProperty(u => u.IsActive, false));
            var results = await Task.WhenAll(
                firstClient.PatchAsJsonAsync($"/api/v1/admin/staff/{second.Id}/lock", new { isLocked = true }, Json),
                secondClient.PatchAsJsonAsync($"/api/v1/admin/staff/{first.Id}/lock", new { isLocked = true }, Json));
            results.Count(r => r.StatusCode == HttpStatusCode.OK).Should().Be(1);
            results.Count(r => r.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized).Should().Be(1);
            (await db.Users.CountAsync(u => u.Role == UserRole.ADMIN && u.IsActive)).Should().Be(1);
            foreach (var result in results) result.Dispose();
        }
        finally
        {
            await db.Users.Where(u => originalActiveIds.Contains(u.Id))
                .ExecuteUpdateAsync(setters => setters.SetProperty(u => u.IsActive, true));
            await db.Users.Where(u => u.Id == first.Id || u.Id == second.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(u => u.IsActive, false));
        }
    }

    [Fact]
    public async Task Credential_rate_limit_returns_standard_429_and_retry_after()
    {
        using var limitedFactory = _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.PostConfigure<IdentityRateLimitOptions>(options => options.PermitLimit = 1)));
        using var client = limitedFactory.CreateClient();
        var credentials = new { email = AuthTestHelper.NewEmail(), password = "Wrong@123" };
        (await client.PostAsJsonAsync("/api/v1/auth/login", credentials, Json)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var limited = await client.PostAsJsonAsync("/api/v1/auth/login", credentials, Json);
        limited.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        limited.Headers.RetryAfter.Should().NotBeNull();
        (await limited.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("code").GetString().Should().Be("RATE_LIMIT_EXCEEDED");
    }

    private static async Task<(long Id, string Email)> CreateStaffAsync(HttpClient admin, string role)
    {
        var email = AuthTestHelper.NewEmail();
        var result = await admin.PostAsJsonAsync("/api/v1/admin/staff",
            new { email, password = "Staff@123", fullName = "Account Test", role }, Json);
        result.StatusCode.Should().Be(HttpStatusCode.Created);
        return ((await result.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("data").GetProperty("id").GetInt64(), email);
    }
}
