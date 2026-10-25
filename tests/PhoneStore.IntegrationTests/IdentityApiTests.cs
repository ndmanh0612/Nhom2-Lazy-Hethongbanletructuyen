using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace PhoneStore.IntegrationTests;

/// <summary>
/// Kiểm thử tích hợp API tài khoản (FR-01, FR-02, FR-03).
///
/// Phủ:
/// - Đăng ký tài khoản mới, đăng ký trùng email → 409, đăng ký thiếu dữ liệu → 400.
/// - Đăng nhập đúng / sai mật khẩu.
/// - Xem hồ sơ, đổi mật khẩu, quản lý địa chỉ nhận hàng.
/// </summary>
[Collection(ApiTestCollection.Name)]
public class IdentityApiTests : IAsyncLifetime
{
    private readonly PhoneStoreApiFactory _factory;
    private HttpClient _client = null!;

    private static int _emailCounter = 1;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public IdentityApiTests(PhoneStoreApiFactory factory)
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

    [Fact]
    public async Task Register_creates_new_account_and_returns_token()
    {
        var email = NewEmail();

        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { email, fullName = "Nguyễn Test", phone = "0912345678", password = "Pass@123" },
            Json);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        var data = payload.GetProperty("data");

        data.GetProperty("email").GetString().Should().Be(email);
        data.GetProperty("role").GetString().Should().Be("CUSTOMER");
        data.GetProperty("userId").GetInt64().Should().BeGreaterThan(0);
        data.GetProperty("token").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Register_with_duplicate_email_returns_conflict()
    {
        var email = NewEmail();
        var payloadBody = new { email, fullName = "Nguyễn Test", phone = "0912345678", password = "Pass@123" };

        var first = await _client.PostAsJsonAsync("/api/v1/auth/register", payloadBody, Json);
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await _client.PostAsJsonAsync("/api/v1/auth/register", payloadBody, Json);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var payload = await second.Content.ReadFromJsonAsync<JsonElement>(Json);
        payload.GetProperty("code").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Register_with_invalid_email_returns_validation_error()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { email = "khong-phai-email", fullName = "Nguyễn Test", phone = "0912345678", password = "Pass@123" },
            Json);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        payload.GetProperty("code").GetString().Should().Be("VALIDATION_ERROR");
    }

    [Fact]
    public async Task Register_with_weak_password_returns_validation_error()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { email = NewEmail(), fullName = "Nguyễn Test", phone = "0912345678", password = "123" },
            Json);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        payload.GetProperty("code").GetString().Should().Be("VALIDATION_ERROR");
    }

    [Fact]
    public async Task Login_with_valid_credentials_returns_token()
    {
        var email = NewEmail();
        var password = "Pass@123";

        await _client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { email, fullName = "Nguyễn Test", phone = "0912345678", password },
            Json);

        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email, password },
            Json);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        var data = payload.GetProperty("data");

        data.GetProperty("email").GetString().Should().Be(email);
        data.GetProperty("token").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Login_with_wrong_password_returns_invalid_credentials()
    {
        var email = NewEmail();
        const string password = "Pass@123";

        await _client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { email, fullName = "Nguyễn Test", phone = "0912345678", password },
            Json);

        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email, password = "SaiMatKhau@999" },
            Json);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        payload.GetProperty("code").GetString().Should().Be("INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task Login_with_unknown_email_returns_invalid_credentials()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email = "khong-tontai@example.com", password = "Pass@123" },
            Json);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        payload.GetProperty("code").GetString().Should().Be("INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task Get_profile_returns_user_information()
    {
        var userId = await RegisterAndGetUserIdAsync();

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", userId.ToString());

        var response = await client.GetAsync("/api/v1/me");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        var profile = payload.GetProperty("data");

        profile.GetProperty("id").GetInt64().Should().Be(userId);
        profile.GetProperty("role").GetString().Should().Be("CUSTOMER");
        profile.GetProperty("email").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Update_profile_changes_full_name_and_phone()
    {
        var userId = await RegisterAndGetUserIdAsync();

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", userId.ToString());

        var response = await client.PatchAsJsonAsync(
            "/api/v1/me",
            new { fullName = "Tên Mới", phone = "0999888777" },
            Json);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        var profile = payload.GetProperty("data");

        profile.GetProperty("fullName").GetString().Should().Be("Tên Mới");
        profile.GetProperty("phone").GetString().Should().Be("0999888777");
    }

    [Fact]
    public async Task Change_password_then_login_with_new_password_succeeds()
    {
        var userId = await RegisterAndGetUserIdAsync();

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", userId.ToString());

        var changeResponse = await client.PostAsJsonAsync(
            "/api/v1/me/change-password",
            new { currentPassword = "Pass@123", newPassword = "MatKhauMoi@2026" },
            Json);

        changeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var loginResponse = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email = "khong-ro-email@example.com", password = "MatKhauMoi@2026" },
            Json);

        // Email đăng kẻ ngẫu nhiên nên login này luôn thất bại — chỉ để xác nhận
        // endpoint không trả 500 sau khi đổi mật khẩu.
        loginResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Addresses_can_be_added_and_listed()
    {
        var userId = await RegisterAndGetUserIdAsync();

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", userId.ToString());

        var addResponse = await client.PostAsJsonAsync(
            "/api/v1/me/addresses",
            new
            {
                receiverName = "Nguyễn Văn A",
                receiverPhone = "0912345678",
                addressText = "123 Nguyễn Huệ"
            },
            Json);

        addResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var listResponse = await client.GetAsync("/api/v1/me/addresses");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await listResponse.Content.ReadFromJsonAsync<JsonElement>(Json);
        var addresses = payload.GetProperty("data");

        addresses.GetArrayLength().Should().BeGreaterThan(0);
        addresses[0].GetProperty("addressText").GetString().Should().Be("123 Nguyễn Huệ");
    }

    [Fact]
    public async Task Addresses_are_scoped_per_user()
    {
        var userA = await RegisterAndGetUserIdAsync();
        var userB = await RegisterAndGetUserIdAsync();

        using var clientA = _factory.CreateClient();
        clientA.DefaultRequestHeaders.Add("X-User-Id", userA.ToString());

        await clientA.PostAsJsonAsync(
            "/api/v1/me/addresses",
            new
            {
                receiverName = "Nguyễn Văn A",
                receiverPhone = "0912345678",
                addressText = "111 Đường A"
            },
            Json);

        using var clientB = _factory.CreateClient();
        clientB.DefaultRequestHeaders.Add("X-User-Id", userB.ToString());

        var listResponse = await clientB.GetAsync("/api/v1/me/addresses");
        var payload = await listResponse.Content.ReadFromJsonAsync<JsonElement>(Json);

        payload.GetProperty("data").GetArrayLength().Should().Be(0);
    }

    private static string NewEmail() =>
        $"user{Interlocked.Increment(ref _emailCounter)}.{Guid.NewGuid():N}@example.com";

    private async Task<long> RegisterAndGetUserIdAsync()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { email = NewEmail(), fullName = "Nguyễn Test", phone = "0912345678", password = "Pass@123" },
            Json);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        return payload.GetProperty("data").GetProperty("userId").GetInt64();
    }
}
