using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace PhoneStore.IntegrationTests;

/// <summary>
/// N2LHTBLTT-39: Regression test cho Catalog (FR-04, FR-05, FR-12) và
/// Identity (FR-01, FR-02, FR-03).
///
/// Trọng tâm là các quy tắc lọc/sắp xếp/phân trang và các quy tắc đăng nhập —
/// những chỗ dễ bị hồi quy khi module khác thay đổi model hoặc mapping.
/// </summary>
[Collection(ApiTestCollection.Name)]
public class CatalogAndIdentityRegressionTests : IAsyncLifetime
{
    private readonly PhoneStoreApiFactory _factory;
    private HttpClient _client = null!;

    public CatalogAndIdentityRegressionTests(PhoneStoreApiFactory factory)
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
    // Catalog — lọc
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Filter_by_brand_returns_only_products_of_that_brand()
    {
        var brandId = await ApiTestHelper.GetBrandIdWithProductsAsync(_client);

        var filtered = await ApiTestHelper.GetDataAsync(
            _client,
            $"/api/v1/products?brandId={brandId}&pageSize=50");

        var items = filtered.GetProperty("items").EnumerateArray().ToList();
        items.Should().NotBeEmpty();

        foreach (var item in items)
        {
            item.GetProperty("brandId").GetInt64().Should().Be(brandId);
        }
    }

    [Fact]
    public async Task Filter_by_min_and_max_price_returns_only_products_inside_the_range()
    {
        var all = await ApiTestHelper.GetDataAsync(_client, "/api/v1/products?pageSize=50");
        var items = all.GetProperty("items").EnumerateArray().ToList();

        var maxPrice = items.Max(item => item.GetProperty("maxPrice").GetInt64());
        var minPrice = items.Min(item => item.GetProperty("minPrice").GetInt64());

        var filtered = await ApiTestHelper.GetDataAsync(
            _client,
            $"/api/v1/products?minPrice={minPrice}&maxPrice={maxPrice}&pageSize=50");

        foreach (var item in filtered.GetProperty("items").EnumerateArray())
        {
            var itemMin = item.GetProperty("minPrice").GetInt64();
            var itemMax = item.GetProperty("maxPrice").GetInt64();

            // Sản phẩm được giữ khi có ít nhất một phiên bản trong khoảng giá.
            itemMax.Should().BeGreaterThanOrEqualTo(minPrice);
            itemMin.Should().BeLessThanOrEqualTo(maxPrice);
        }
    }

    [Fact]
    public async Task Filter_by_search_is_case_insensitive()
    {
        var lower = await ApiTestHelper.GetDataAsync(_client, "/api/v1/products?search=iphone");
        var upper = await ApiTestHelper.GetDataAsync(_client, "/api/v1/products?search=IPHONE");

        var lowerIds = lower.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("id").GetInt64()).ToList();
        var upperIds = upper.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("id").GetInt64()).ToList();

        lowerIds.Should().NotBeEmpty();
        lowerIds.Should().Equal(upperIds);
    }

    [Fact]
    public async Task Search_with_no_match_returns_an_empty_page()
    {
        var filtered = await ApiTestHelper.GetDataAsync(
            _client,
            "/api/v1/products?search=khong-ton-tai-san-pham-abcxyz");

        filtered.GetProperty("items").GetArrayLength().Should().Be(0);
        filtered.GetProperty("totalCount").GetInt64().Should().Be(0);
    }

    [Fact]
    public async Task Filtering_by_an_unknown_category_returns_an_empty_page()
    {
        var filtered = await ApiTestHelper.GetDataAsync(_client, "/api/v1/products?categoryId=999999");

        filtered.GetProperty("items").GetArrayLength().Should().Be(0);
        filtered.GetProperty("totalCount").GetInt64().Should().Be(0);
    }

    // ---------------------------------------------------------------------
    // Catalog — sắp xếp
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Sort_by_newest_is_stable_for_products_created_at_the_same_time()
    {
        // Seed data tạo sản phẩm cùng lúc → thứ tự phải ổn định giữa 2 lần gọi.
        var first = await ApiTestHelper.GetDataAsync(_client, "/api/v1/products?sortBy=newest&pageSize=50");
        var second = await ApiTestHelper.GetDataAsync(_client, "/api/v1/products?sortBy=newest&pageSize=50");

        var firstIds = first.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("id").GetInt64()).ToList();
        var secondIds = second.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("id").GetInt64()).ToList();

        firstIds.Should().Equal(secondIds);
    }

    [Fact]
    public async Task Sort_by_price_desc_can_be_reversed_by_price_asc()
    {
        var desc = await GetSortedIdsAsync("price_desc");
        var asc = await GetSortedIdsAsync("price_asc");

        desc.Should().Equal(asc.AsEnumerable().Reverse());
    }

    [Fact]
    public async Task Sorted_result_contains_the_same_products_as_the_unsorted_one()
    {
        var unsorted = await ApiTestHelper.GetDataAsync(_client, "/api/v1/products?pageSize=50");
        var sorted = await ApiTestHelper.GetDataAsync(_client, "/api/v1/products?sortBy=price_asc&pageSize=50");

        var unsortedIds = unsorted.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("id").GetInt64()).OrderBy(id => id).ToList();
        var sortedIds = sorted.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("id").GetInt64()).OrderBy(id => id).ToList();

        sortedIds.Should().Equal(unsortedIds);
    }

    // ---------------------------------------------------------------------
    // Catalog — validation
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Invalid_page_number_returns_validation_error()
    {
        var (status, body) = await ApiTestHelper.SendAsync(_client, HttpMethod.Get, "/api/v1/products?page=0");

        status.Should().Be(HttpStatusCode.BadRequest);
        ApiTestHelper.AssertErrorEnvelope(body, "VALIDATION_ERROR");
    }

    [Fact]
    public async Task Negative_min_price_returns_validation_error()
    {
        var (status, body) = await ApiTestHelper.SendAsync(_client, HttpMethod.Get, "/api/v1/products?minPrice=-1");

        status.Should().Be(HttpStatusCode.BadRequest);
        ApiTestHelper.AssertErrorEnvelope(body, "VALIDATION_ERROR");
    }

    [Fact]
    public async Task Max_price_lower_than_min_price_returns_validation_error()
    {
        var (status, body) = await ApiTestHelper.SendAsync(
            _client,
            HttpMethod.Get,
            "/api/v1/products?minPrice=50000000&maxPrice=10000000");

        status.Should().Be(HttpStatusCode.BadRequest);
        ApiTestHelper.AssertErrorEnvelope(body, "VALIDATION_ERROR");
    }

    [Fact]
    public async Task Page_size_above_the_limit_returns_validation_error()
    {
        var (status, body) = await ApiTestHelper.SendAsync(_client, HttpMethod.Get, "/api/v1/products?pageSize=51");

        status.Should().Be(HttpStatusCode.BadRequest);
        ApiTestHelper.AssertErrorEnvelope(body, "VALIDATION_ERROR");
    }

    // ---------------------------------------------------------------------
    // Catalog — chi tiết sản phẩm
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Product_detail_contains_brand_and_category_names()
    {
        var products = await ApiTestHelper.GetDataAsync(_client, "/api/v1/products");
        var product = products.GetProperty("items")[0];

        var detail = await ApiTestHelper.GetDataAsync(
            _client,
            $"/api/v1/products/{product.GetProperty("id").GetInt64()}");

        detail.GetProperty("categoryName").GetString().Should().NotBeNullOrWhiteSpace();
        detail.GetProperty("brandName").GetString().Should().NotBeNullOrWhiteSpace();
        detail.GetProperty("description").GetString().Should().NotBeNull();
        detail.GetProperty("specificationsJson").GetString().Should().NotBeNull();
    }

    [Fact]
    public async Task Product_detail_exposes_variants_matching_the_list_view_prices()
    {
        var products = await ApiTestHelper.GetDataAsync(_client, "/api/v1/products");
        var product = products.GetProperty("items")[0];
        var productId = product.GetProperty("id").GetInt64();

        var detail = await ApiTestHelper.GetDataAsync(_client, $"/api/v1/products/{productId}");
        var variantPrices = detail.GetProperty("variants").EnumerateArray()
            .Select(variant => variant.GetProperty("price").GetInt64())
            .OrderBy(price => price)
            .ToList();

        variantPrices.Should().NotBeEmpty();
        variantPrices.First().Should().Be(product.GetProperty("minPrice").GetInt64());
        variantPrices.Last().Should().Be(product.GetProperty("maxPrice").GetInt64());
    }

    [Fact]
    public async Task Unknown_product_id_returns_not_found()
    {
        var (status, body) = await ApiTestHelper.SendAsync(_client, HttpMethod.Get, "/api/v1/products/999999999");

        status.Should().Be(HttpStatusCode.NotFound);
        ApiTestHelper.AssertErrorEnvelope(body, "PRODUCT_NOT_FOUND");
    }

    [Fact]
    public async Task Non_numeric_product_id_is_handled_without_a_server_error()
    {
        var (status, _) = await ApiTestHelper.SendAsync(_client, HttpMethod.Get, "/api/v1/products/abc");

        status.Should().NotBe(HttpStatusCode.InternalServerError);
    }

    // ---------------------------------------------------------------------
    // Identity — đăng ký
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Register_normalizes_the_email_to_lowercase()
    {
        var email = $"MixedCase.{Guid.NewGuid():N}@Example.COM";

        var (status, body) = await ApiTestHelper.SendAsync(
            _client,
            HttpMethod.Post,
            "/api/v1/auth/register",
            new { email, fullName = "Khách Viết Hoa", phone = "0912345678", password = "Pass@123" });

        status.Should().Be(HttpStatusCode.Created);
        body.GetProperty("data").GetProperty("email").GetString().Should().Be(email.ToLowerInvariant());
    }

    [Fact]
    public async Task Register_rejects_a_password_without_digits()
    {
        var (status, body) = await ApiTestHelper.SendAsync(
            _client,
            HttpMethod.Post,
            "/api/v1/auth/register",
            new
            {
                email = $"nodigit.{Guid.NewGuid():N}@example.com",
                fullName = "Khách Không Số",
                phone = "0912345678",
                password = "PasswordOnly"
            });

        status.Should().Be(HttpStatusCode.BadRequest);
        ApiTestHelper.AssertErrorEnvelope(body, "VALIDATION_ERROR");
    }

    [Fact]
    public async Task Register_rejects_a_password_shorter_than_8_characters()
    {
        var (status, body) = await ApiTestHelper.SendAsync(
            _client,
            HttpMethod.Post,
            "/api/v1/auth/register",
            new
            {
                email = $"shortpwd.{Guid.NewGuid():N}@example.com",
                fullName = "Khách Mật Khẩu Ngắn",
                phone = "0912345678",
                password = "Ab1"
            });

        status.Should().Be(HttpStatusCode.BadRequest);
        ApiTestHelper.AssertErrorEnvelope(body, "VALIDATION_ERROR");
    }

    [Fact]
    public async Task Register_allows_omitting_the_phone_number()
    {
        var (status, body) = await ApiTestHelper.SendAsync(
            _client,
            HttpMethod.Post,
            "/api/v1/auth/register",
            new
            {
                email = $"nophone.{Guid.NewGuid():N}@example.com",
                fullName = "Khách Không SĐT",
                password = "Pass@123"
            });

        status.Should().Be(HttpStatusCode.Created);
        body.GetProperty("data").GetProperty("userId").GetInt64().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Register_rejects_a_blank_full_name()
    {
        var (status, body) = await ApiTestHelper.SendAsync(
            _client,
            HttpMethod.Post,
            "/api/v1/auth/register",
            new
            {
                email = $"noname.{Guid.NewGuid():N}@example.com",
                fullName = "   ",
                phone = "0912345678",
                password = "Pass@123"
            });

        status.Should().Be(HttpStatusCode.BadRequest);
        ApiTestHelper.AssertErrorEnvelope(body, "VALIDATION_ERROR");
    }

    [Fact]
    public async Task Registering_the_same_email_twice_is_detected_case_insensitively()
    {
        var suffix = Guid.NewGuid().ToString("N");

        var (firstStatus, _) = await ApiTestHelper.SendAsync(
            _client,
            HttpMethod.Post,
            "/api/v1/auth/register",
            new
            {
                email = $"dupcase.{suffix}@example.com",
                fullName = "Khách Trùng Email",
                phone = "0912345678",
                password = "Pass@123"
            });

        firstStatus.Should().Be(HttpStatusCode.Created);

        var (secondStatus, secondBody) = await ApiTestHelper.SendAsync(
            _client,
            HttpMethod.Post,
            "/api/v1/auth/register",
            new
            {
                email = $"DUPCASE.{suffix}@EXAMPLE.COM",
                fullName = "Khách Trùng Email",
                phone = "0912345678",
                password = "Pass@123"
            });

        secondStatus.Should().Be(HttpStatusCode.Conflict);
        ApiTestHelper.AssertErrorEnvelope(secondBody, "EMAIL_ALREADY_EXISTS");
    }

    // ---------------------------------------------------------------------
    // Identity — đăng nhập
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Login_is_case_insensitive_for_the_email()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var email = $"login.{suffix}@example.com";

        await ApiTestHelper.SendAsync(
            _client,
            HttpMethod.Post,
            "/api/v1/auth/register",
            new { email, fullName = "Khách Đăng Nhập", phone = "0912345678", password = "Pass@123" });

        var (status, body) = await ApiTestHelper.SendAsync(
            _client,
            HttpMethod.Post,
            "/api/v1/auth/login",
            new { email = $"LOGIN.{suffix}@EXAMPLE.COM", password = "Pass@123" });

        status.Should().Be(HttpStatusCode.OK);
        body.GetProperty("data").GetProperty("email").GetString().Should().Be(email);
    }

    [Fact]
    public async Task Login_with_a_blank_email_returns_validation_error()
    {
        var (status, body) = await ApiTestHelper.SendAsync(
            _client,
            HttpMethod.Post,
            "/api/v1/auth/login",
            new { email = "", password = "Pass@123" });

        status.Should().Be(HttpStatusCode.BadRequest);
        ApiTestHelper.AssertErrorEnvelope(body, "VALIDATION_ERROR");
    }

    [Fact]
    public async Task Login_with_a_blank_password_returns_validation_error()
    {
        var (status, body) = await ApiTestHelper.SendAsync(
            _client,
            HttpMethod.Post,
            "/api/v1/auth/login",
            new { email = "someone@example.com", password = "" });

        status.Should().Be(HttpStatusCode.BadRequest);
        ApiTestHelper.AssertErrorEnvelope(body, "VALIDATION_ERROR");
    }

    [Fact]
    public async Task Login_failure_does_not_leak_whether_the_email_exists()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var email = $"existing.{suffix}@example.com";

        await ApiTestHelper.SendAsync(
            _client,
            HttpMethod.Post,
            "/api/v1/auth/register",
            new { email, fullName = "Khách Tồn Tại", phone = "0912345678", password = "Pass@123" });

        var (_, wrongPasswordBody) = await ApiTestHelper.SendAsync(
            _client,
            HttpMethod.Post,
            "/api/v1/auth/login",
            new { email, password = "SaiMatKhau@999" });

        var (_, unknownEmailBody) = await ApiTestHelper.SendAsync(
            _client,
            HttpMethod.Post,
            "/api/v1/auth/login",
            new { email = $"unknown.{suffix}@example.com", password = "SaiMatKhau@999" });

        // NFR-01: thông báo phải giống nhau, không tiết lộ email có tồn tại hay không.
        wrongPasswordBody.GetProperty("message").GetString()
            .Should().Be(unknownEmailBody.GetProperty("message").GetString());
        wrongPasswordBody.GetProperty("code").GetString()
            .Should().Be(unknownEmailBody.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Registering_returns_a_non_empty_token()
    {
        var (status, body) = await ApiTestHelper.SendAsync(
            _client,
            HttpMethod.Post,
            "/api/v1/auth/register",
            new
            {
                email = $"token.{Guid.NewGuid():N}@example.com",
                fullName = "Khách Có Token",
                phone = "0912345678",
                password = "Pass@123"
            });

        status.Should().Be(HttpStatusCode.Created);
        body.GetProperty("data").GetProperty("token").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Registered_user_is_persisted_with_the_customer_role()
    {
        var email = $"role.{Guid.NewGuid():N}@example.com";

        await ApiTestHelper.SendAsync(
            _client,
            HttpMethod.Post,
            "/api/v1/auth/register",
            new { email, fullName = "Khách Vai Trò", phone = "0912345678", password = "Pass@123" });

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<PhoneStore.Modules.Identity.Infrastructure.Persistence.IdentityDbContext>();

        var user = await dbContext.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email);

        user.Should().NotBeNull();
        user!.Role.Should().Be(PhoneStore.Modules.Identity.Domain.Entities.UserRole.CUSTOMER);
        user.IsActive.Should().BeTrue();
        user.PasswordHash.Should().NotBe("Pass@123");
    }

    [Fact]
    public async Task Profile_of_an_unknown_user_returns_not_found()
    {
        using var client = ApiTestHelper.CreateUserClient(_factory, 999_999_999);

        var (status, body) = await ApiTestHelper.SendAsync(client, HttpMethod.Get, "/api/v1/me");

        status.Should().Be(HttpStatusCode.NotFound);
        ApiTestHelper.AssertErrorEnvelope(body, "USER_NOT_FOUND");
    }

    [Fact]
    public async Task Logout_returns_a_successful_envelope()
    {
        var (status, body) = await ApiTestHelper.SendAsync(_client, HttpMethod.Post, "/api/v1/auth/logout");

        status.Should().Be(HttpStatusCode.OK);
        body.GetProperty("success").GetBoolean().Should().BeTrue();
    }

    private async Task<List<long>> GetSortedIdsAsync(string sortBy)
    {
        var data = await ApiTestHelper.GetDataAsync(
            _client,
            $"/api/v1/products?sortBy={sortBy}&pageSize=50");

        return data.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("id").GetInt64())
            .ToList();
    }
}
