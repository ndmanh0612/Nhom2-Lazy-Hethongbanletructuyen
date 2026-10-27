using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace PhoneStore.IntegrationTests;

/// <summary>
/// N2LHTBLTT-8 / N2LHTBLTT-9: Helper đăng nhập thật qua API và trả về
/// HttpClient đã gắn header "Authorization: Bearer {accessToken}".
///
/// Trước đây test dùng header giả "X-User-Id" để mạo danh — không phản ánh
/// xác thực thật. Nay mọi endpoint có xác thực đều được gọi đúng cách:
/// đăng ký / đăng nhập → lấy JWT → gọi API với Bearer token.
/// </summary>
internal static class AuthTestHelper
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static int _counter = 1;

    internal static string NewEmail() =>
        $"user{Interlocked.Increment(ref _counter)}.{Guid.NewGuid():N}@example.com";

    /// <summary>
    /// Đăng ký một tài khoản khách hàng và trả về client đã xác thực.
    /// </summary>
    internal static async Task<HttpClient> CreateAuthenticatedCustomerAsync(
        PhoneStoreApiFactory factory,
        string? email = null,
        string password = "Pass@123")
    {
        var client = factory.CreateClient();
        var accountEmail = email ?? NewEmail();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { email = accountEmail, fullName = "Nguyễn Test", phone = "0912345678", password },
            Json);

        response.StatusCode.Should().Be(
            System.Net.HttpStatusCode.Created,
            "Đăng ký tài khoản phải trả 201 nhưng thực tế trả {0}.", response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        var accessToken = payload.GetProperty("data").GetProperty("accessToken").GetString();

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        return client;
    }

    /// <summary>
    /// Đăng nhập bằng tài khoản có sẵn và trả về client đã xác thực.
    /// </summary>
    internal static async Task<HttpClient> CreateAuthenticatedClientAsync(
        PhoneStoreApiFactory factory,
        string email,
        string password)
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email, password },
            Json);

        response.StatusCode.Should().Be(
            System.Net.HttpStatusCode.OK,
            "Đăng nhập phải trả 200 nhưng thực tế trả {0}.", response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        var accessToken = payload.GetProperty("data").GetProperty("accessToken").GetString();

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        return client;
    }

    /// <summary>
    /// Tạo tài khoản nhân viên (STAFF) qua API quản trị và trả về client đã
    /// xác thực bằng tài khoản đó.
    ///
    /// Tài khoản STAFF được tạo bởi ADMIN seed sẵn trong môi trường Development.
    /// </summary>
    internal static async Task<HttpClient> CreateAuthenticatedStaffAsync(
        PhoneStoreApiFactory factory,
        string? email = null,
        string password = "Staff@123")
    {
        var staffEmail = email ?? NewEmail();

        using var adminClient = await CreateAuthenticatedClientAsync(
            factory,
            "admin@phonestore.vn",
            "Admin@123");

        var createResponse = await adminClient.PostAsJsonAsync(
            "/api/v1/admin/staff",
            new { email = staffEmail, password, fullName = "Nhân Viên Test", role = "STAFF", phone = "0912345678" },
            Json);

        createResponse.StatusCode.Should().Be(
            System.Net.HttpStatusCode.Created,
            "Tạo tài khoản nhân viên phải trả 201 nhưng thực tế trả {0}.", createResponse.StatusCode);

        return await CreateAuthenticatedClientAsync(factory, staffEmail, password);
    }

    /// <summary>
    /// Tạo client đã xác thực bằng tài khoản ADMIN.
    /// </summary>
    internal static Task<HttpClient> CreateAuthenticatedAdminAsync(
        PhoneStoreApiFactory factory,
        string email = "admin@phonestore.vn",
        string password = "Admin@123")
    {
        return CreateAuthenticatedClientAsync(factory, email, password);
    }
}
