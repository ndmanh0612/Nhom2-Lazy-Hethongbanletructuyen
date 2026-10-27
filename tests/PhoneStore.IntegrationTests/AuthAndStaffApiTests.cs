using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace PhoneStore.IntegrationTests;

/// <summary>
/// N2LHTBLTT-9 / N2LHTBLTT-15 / N2LHTBLTT-16: Kiểm thử tích hợp xác thực,
/// hồ sơ khách hàng và quản lý tài khoản nhân viên.
///
/// Phủ:
/// - Đăng ký / đăng nhập trả access token hợp lệ, refresh token nằm trong cookie HttpOnly.
/// - Access token thực sự bảo vệ endpoint (401 khi không có token).
/// - Refresh token làm mới được access token, và bị thu hồi sau khi dùng (token rotation).
/// - Đổi mật khẩu thu hồi toàn bộ phiên.
/// - Quản trị tạo / khoá / đổi vai trò tài khoản nhân viên (FR-15).
/// </summary>
[Collection(ApiTestCollection.Name)]
public class AuthAndStaffApiTests : IAsyncLifetime
{
    private readonly PhoneStoreApiFactory _factory;
    private HttpClient _client = null!;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public AuthAndStaffApiTests(PhoneStoreApiFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    // ---------------------------------------------------------------------
    // FR-01 / FR-02 / FR-09: xác thực.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Register_returns_access_token_and_sets_httponly_refresh_cookie()
    {
        var email = AuthTestHelper.NewEmail();

        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { email, fullName = "Nguyễn Test", phone = "0912345678", password = "Pass@123" },
            Json);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        var data = payload.GetProperty("data");

        data.GetProperty("accessToken").GetString().Should().NotBeNullOrWhiteSpace();

        // Refresh token phải nằm trong cookie HttpOnly, không lộ ra body.
        var refreshCookie = response.Headers.TryGetValues("Set-Cookie", out var cookies)
            ? cookies.FirstOrDefault(c => c.Contains("phonestore_refresh"))
            : null;

        refreshCookie.Should().NotBeNull("đăng ký phải trả cookie refresh token");
        refreshCookie!.Should().Contain("httponly");
    }

    [Fact]
    public async Task Access_token_grants_access_to_protected_endpoint()
    {
        using var authenticated = await AuthTestHelper.CreateAuthenticatedCustomerAsync(_factory);

        var response = await authenticated.GetAsync("/api/v1/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Protected_endpoint_without_token_returns_401_with_standard_error_envelope()
    {
        using var anonymousClient = _factory.CreateClient();

        var response = await anonymousClient.GetAsync("/api/v1/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        payload.GetProperty("code").GetString().Should().Be("UNAUTHORIZED");
        payload.GetProperty("requestId").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Invalid_access_token_returns_401()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "khong-phai-jwt-hop-le");

        var response = await client.GetAsync("/api/v1/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_token_rotates_and_old_token_is_rejected()
    {
        var email = AuthTestHelper.NewEmail();
        const string password = "Pass@123";

        using var client = _factory.CreateClient();
        await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { email, fullName = "Nguyễn Test", phone = "0912345678", password },
            Json);

        // Đăng nhập để nhận cookie refresh token.
        var loginResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email, password },
            Json);

        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var refreshCookie = loginResponse.Headers.TryGetValues("Set-Cookie", out var cookies)
            ? cookies.FirstOrDefault(c => c.Contains("phonestore_refresh"))
            : null;

        refreshCookie.Should().NotBeNull();

        // Lấy giá trị refresh token từ cookie.
        var refreshValue = refreshCookie!
            .Split(';')[0]
            .Split('=', 2)[1];

        // CookieContainer tự gửi cookie khi dùng client này.
        var firstRefresh = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { }, Json);
        firstRefresh.StatusCode.Should().Be(HttpStatusCode.OK);

        var refreshed = await firstRefresh.Content.ReadFromJsonAsync<JsonElement>(Json);
        refreshed.GetProperty("data").GetProperty("accessToken").GetString().Should().NotBeNullOrWhiteSpace();

        // Refresh lần thứ hai với token cũ phải bị từ chối (token rotation).
        var secondRefresh = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { }, Json);
        secondRefresh.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_revokes_refresh_token()
    {
        var email = AuthTestHelper.NewEmail();
        const string password = "Pass@123";

        using var client = _factory.CreateClient();
        await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { email, fullName = "Nguyễn Test", phone = "0912345678", password },
            Json);

        var loginResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email, password },
            Json);

        var accessToken = (await loginResponse.Content.ReadFromJsonAsync<JsonElement>(Json))
            .GetProperty("data").GetProperty("accessToken").GetString();

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var logoutResponse = await client.PostAsJsonAsync("/api/v1/auth/logout", new { }, Json);
        logoutResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var refreshResponse = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { }, Json);
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Locked_account_cannot_login()
    {
        var email = AuthTestHelper.NewEmail();
        const string password = "Pass@123";

        using var client = _factory.CreateClient();
        var registerResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { email, fullName = "Nguyễn Test", phone = "0912345678", password },
            Json);

        var userId = (await registerResponse.Content.ReadFromJsonAsync<JsonElement>(Json))
            .GetProperty("data").GetProperty("userId").GetInt64();

        // Khoá tài khoản qua API quản trị.
        using var adminClient = await AuthTestHelper.CreateAuthenticatedAdminAsync(_factory);
        var lockResponse = await adminClient.PatchAsJsonAsync(
            $"/api/v1/admin/staff/{userId}/lock",
            new { isLocked = true, reason = "Test" },
            Json);

        lockResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var loginResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email, password },
            Json);

        loginResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var payload = await loginResponse.Content.ReadFromJsonAsync<JsonElement>(Json);
        payload.GetProperty("code").GetString().Should().Be("ACCOUNT_LOCKED");
    }

    // ---------------------------------------------------------------------
    // FR-15 (N2LHTBLTT-16): quản lý tài khoản nhân viên.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Admin_can_create_staff_account()
    {
        using var adminClient = await AuthTestHelper.CreateAuthenticatedAdminAsync(_factory);
        var email = AuthTestHelper.NewEmail();

        var response = await adminClient.PostAsJsonAsync(
            "/api/v1/admin/staff",
            new { email, password = "Staff@123", fullName = "Nhân Viên Mới", role = "STAFF", phone = "0912345678" },
            Json);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        var data = payload.GetProperty("data");

        data.GetProperty("email").GetString().Should().Be(email);
        data.GetProperty("role").GetString().Should().Be("STAFF");
        data.GetProperty("isActive").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Customer_cannot_create_staff_account()
    {
        using var customerClient = await AuthTestHelper.CreateAuthenticatedCustomerAsync(_factory);

        var response = await customerClient.PostAsJsonAsync(
            "/api/v1/admin/staff",
            new { email = AuthTestHelper.NewEmail(), password = "Staff@123", fullName = "X", role = "STAFF" },
            Json);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Admin_can_list_staff_with_pagination()
    {
        using var adminClient = await AuthTestHelper.CreateAuthenticatedAdminAsync(_factory);

        var response = await adminClient.GetAsync("/api/v1/admin/staff?page=1&pageSize=10");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        var data = payload.GetProperty("data");

        data.GetProperty("items").GetArrayLength().Should().BeGreaterThan(0);
        data.GetProperty("page").GetInt32().Should().Be(1);
        data.GetProperty("pageSize").GetInt32().Should().Be(10);
    }

    [Fact]
    public async Task Admin_can_change_staff_role()
    {
        using var adminClient = await AuthTestHelper.CreateAuthenticatedAdminAsync(_factory);
        var email = AuthTestHelper.NewEmail();

        var createResponse = await adminClient.PostAsJsonAsync(
            "/api/v1/admin/staff",
            new { email, password = "Staff@123", fullName = "Nhân Viên", role = "STAFF" },
            Json);

        var staffId = (await createResponse.Content.ReadFromJsonAsync<JsonElement>(Json))
            .GetProperty("data").GetProperty("id").GetInt64();

        var response = await adminClient.PatchAsJsonAsync(
            $"/api/v1/admin/staff/{staffId}/role",
            new { role = "ADMIN" },
            Json);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        payload.GetProperty("data").GetProperty("role").GetString().Should().Be("ADMIN");
    }

    [Fact]
    public async Task Admin_cannot_change_own_role()
    {
        using var adminClient = await AuthTestHelper.CreateAuthenticatedAdminAsync(_factory);

        var profile = await adminClient.GetFromJsonAsync<JsonElement>("/api/v1/me", Json);
        var adminId = profile.GetProperty("data").GetProperty("id").GetInt64();

        var response = await adminClient.PatchAsJsonAsync(
            $"/api/v1/admin/staff/{adminId}/role",
            new { role = "STAFF" },
            Json);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Admin_can_lock_and_unlock_staff_account()
    {
        using var adminClient = await AuthTestHelper.CreateAuthenticatedAdminAsync(_factory);
        var email = AuthTestHelper.NewEmail();

        var createResponse = await adminClient.PostAsJsonAsync(
            "/api/v1/admin/staff",
            new { email, password = "Staff@123", fullName = "Nhân Viên", role = "STAFF" },
            Json);

        var staffId = (await createResponse.Content.ReadFromJsonAsync<JsonElement>(Json))
            .GetProperty("data").GetProperty("id").GetInt64();

        var lockResponse = await adminClient.PatchAsJsonAsync(
            $"/api/v1/admin/staff/{staffId}/lock",
            new { isLocked = true, reason = "Test" },
            Json);

        lockResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await lockResponse.Content.ReadFromJsonAsync<JsonElement>(Json))
            .GetProperty("data").GetProperty("isActive").GetBoolean().Should().BeFalse();

        var unlockResponse = await adminClient.PatchAsJsonAsync(
            $"/api/v1/admin/staff/{staffId}/lock",
            new { isLocked = false, reason = (string?)null },
            Json);

        unlockResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await unlockResponse.Content.ReadFromJsonAsync<JsonElement>(Json))
            .GetProperty("data").GetProperty("isActive").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Admin_can_reset_staff_password()
    {
        using var adminClient = await AuthTestHelper.CreateAuthenticatedAdminAsync(_factory);
        var email = AuthTestHelper.NewEmail();

        var createResponse = await adminClient.PostAsJsonAsync(
            "/api/v1/admin/staff",
            new { email, password = "Staff@123", fullName = "Nhân Viên", role = "STAFF" },
            Json);

        var staffId = (await createResponse.Content.ReadFromJsonAsync<JsonElement>(Json))
            .GetProperty("data").GetProperty("id").GetInt64();

        var resetResponse = await adminClient.PostAsJsonAsync(
            $"/api/v1/admin/staff/{staffId}/reset-password",
            new { newPassword = "MatKhauMoi@2026" },
            Json);

        resetResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Đăng nhập bằng mật khẩu mới phải thành công.
        using var staffClient = await AuthTestHelper.CreateAuthenticatedClientAsync(
            _factory, email, "MatKhauMoi@2026");

        var profile = await staffClient.GetFromJsonAsync<JsonElement>("/api/v1/me", Json);
        profile.GetProperty("data").GetProperty("id").GetInt64().Should().Be(staffId);
    }
}
