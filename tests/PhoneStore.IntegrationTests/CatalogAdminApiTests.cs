using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace PhoneStore.IntegrationTests;

/// <summary>
/// FR-12 (N2LHTBLTT-10) / FR-13 (N2LHTBLTT-18): Kiểm thử tích hợp quản trị
/// danh mục, thương hiệu, sản phẩm và SKU.
///
/// Phủ:
/// - Tạo / cập nhật / xoá danh mục và thương hiệu.
/// - Tên danh mục / thương hiệu phải duy nhất.
/// - Tạo sản phẩm kèm SKU, cập nhật giá, SKU phải duy nhất toàn hệ thống.
/// - Chỉ nhân viên / admin mới được gọi API quản trị.
/// </summary>
[Collection(ApiTestCollection.Name)]
public class CatalogAdminApiTests : IAsyncLifetime
{
    private readonly PhoneStoreApiFactory _factory;
    private HttpClient _staffClient = null!;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public CatalogAdminApiTests(PhoneStoreApiFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        _staffClient = await AuthTestHelper.CreateAuthenticatedStaffAsync(_factory);
    }

    public Task DisposeAsync()
    {
        _staffClient.Dispose();
        return Task.CompletedTask;
    }

    // ---------------------------------------------------------------------
    // FR-12: Danh mục.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Staff_can_create_update_and_delete_category()
    {
        var name = $"Danh mục test {Guid.NewGuid():N}";

        var createResponse = await _staffClient.PostAsJsonAsync(
            "/api/v1/admin/categories",
            new { name },
            Json);

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = (await createResponse.Content.ReadFromJsonAsync<JsonElement>(Json))
            .GetProperty("data");

        var categoryId = created.GetProperty("id").GetInt64();
        created.GetProperty("name").GetString().Should().Be(name);
        created.GetProperty("isActive").GetBoolean().Should().BeTrue();

        var updateResponse = await _staffClient.PutAsJsonAsync(
            $"/api/v1/admin/categories/{categoryId}",
            new { name = $"{name} cập nhật", isActive = false },
            Json);

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = (await updateResponse.Content.ReadFromJsonAsync<JsonElement>(Json))
            .GetProperty("data");

        updated.GetProperty("name").GetString().Should().Be($"{name} cập nhật");
        updated.GetProperty("isActive").GetBoolean().Should().BeFalse();

        var deleteResponse = await _staffClient.DeleteAsync($"/api/v1/admin/categories/{categoryId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Creating_duplicate_category_returns_conflict()
    {
        var name = $"Danh mục trùng {Guid.NewGuid():N}";

        var first = await _staffClient.PostAsJsonAsync("/api/v1/admin/categories", new { name }, Json);
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await _staffClient.PostAsJsonAsync("/api/v1/admin/categories", new { name }, Json);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var payload = await second.Content.ReadFromJsonAsync<JsonElement>(Json);
        payload.GetProperty("code").GetString().Should().Be("CATEGORY_ALREADY_EXISTS");
    }

    [Fact]
    public async Task Updating_unknown_category_returns_not_found()
    {
        var response = await _staffClient.PutAsJsonAsync(
            "/api/v1/admin/categories/999999999",
            new { name = "Không tồn tại", isActive = true },
            Json);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        payload.GetProperty("code").GetString().Should().Be("CATEGORY_NOT_FOUND");
    }

    // ---------------------------------------------------------------------
    // FR-12: Thương hiệu.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Staff_can_create_update_and_delete_brand()
    {
        var name = $"Thương hiệu test {Guid.NewGuid():N}";

        var createResponse = await _staffClient.PostAsJsonAsync(
            "/api/v1/admin/brands",
            new { name },
            Json);

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var brandId = (await createResponse.Content.ReadFromJsonAsync<JsonElement>(Json))
            .GetProperty("data").GetProperty("id").GetInt64();

        var updateResponse = await _staffClient.PutAsJsonAsync(
            $"/api/v1/admin/brands/{brandId}",
            new { name = $"{name} cập nhật", isActive = true },
            Json);

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var deleteResponse = await _staffClient.DeleteAsync($"/api/v1/admin/brands/{brandId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Creating_duplicate_brand_returns_conflict()
    {
        var name = $"Thương hiệu trùng {Guid.NewGuid():N}";

        await _staffClient.PostAsJsonAsync("/api/v1/admin/brands", new { name }, Json);
        var second = await _staffClient.PostAsJsonAsync("/api/v1/admin/brands", new { name }, Json);

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ---------------------------------------------------------------------
    // FR-13: Sản phẩm và SKU.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Staff_can_create_product_with_variants()
    {
        var (categoryId, brandId) = await GetCatalogReferenceIdsAsync();
        var sku = $"SKU-{Guid.NewGuid():N}"[..20].ToUpperInvariant();

        var response = await _staffClient.PostAsJsonAsync(
            "/api/v1/admin/products",
            new
            {
                categoryId,
                brandId,
                name = "Sản phẩm test",
                description = "Mô tả sản phẩm test",
                specificationsJson = "{\"ram\":\"8GB\"}",
                variants = new[]
                {
                    new { sku, color = "Đen", capacity = "128GB", price = 20_000_000, stockQuantity = 10 }
                }
            },
            Json);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var data = (await response.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("data");
        var variants = data.GetProperty("variants");

        variants.GetArrayLength().Should().Be(1);
        variants[0].GetProperty("sku").GetString().Should().Be(sku);
        variants[0].GetProperty("price").GetInt64().Should().Be(20_000_000);
        data.GetProperty("specificationsJson").GetString().Should().Contain("8GB");
    }

    [Fact]
    public async Task Creating_product_with_duplicate_sku_returns_conflict()
    {
        var (categoryId, brandId) = await GetCatalogReferenceIdsAsync();
        var sku = $"SKU-{Guid.NewGuid():N}"[..20].ToUpperInvariant();

        var createRequest = new
        {
            categoryId,
            brandId,
            name = "Sản phẩm trùng SKU",
            description = "Mô tả",
            specificationsJson = (string?)null,
            variants = new[]
            {
                new { sku, color = "Đen", capacity = "128GB", price = 1_000_000, stockQuantity = 1 }
            }
        };

        var first = await _staffClient.PostAsJsonAsync("/api/v1/admin/products", createRequest, Json);
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await _staffClient.PostAsJsonAsync("/api/v1/admin/products", createRequest, Json);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var payload = await second.Content.ReadFromJsonAsync<JsonElement>(Json);
        payload.GetProperty("code").GetString().Should().Be("SKU_ALREADY_EXISTS");
    }

    [Fact]
    public async Task Staff_can_update_variant_price()
    {
        var (categoryId, brandId) = await GetCatalogReferenceIdsAsync();
        var sku = $"SKU-{Guid.NewGuid():N}"[..20].ToUpperInvariant();

        var createResponse = await _staffClient.PostAsJsonAsync(
            "/api/v1/admin/products",
            new
            {
                categoryId,
                brandId,
                name = "Sản phẩm cập nhật giá",
                description = "Mô tả",
                specificationsJson = (string?)null,
                variants = new[]
                {
                    new { sku, color = "Đen", capacity = "256GB", price = 30_000_000, stockQuantity = 5 }
                }
            },
            Json);

        var variantId = (await createResponse.Content.ReadFromJsonAsync<JsonElement>(Json))
            .GetProperty("data").GetProperty("variants")[0].GetProperty("id").GetInt64();

        var updateResponse = await _staffClient.PutAsJsonAsync(
            $"/api/v1/admin/products/variants/{variantId}",
            new { sku, color = "Đen", capacity = "256GB", price = 33_000_000, isActive = true },
            Json);

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = (await updateResponse.Content.ReadFromJsonAsync<JsonElement>(Json))
            .GetProperty("data");

        updated.GetProperty("price").GetInt64().Should().Be(33_000_000);
    }

    [Fact]
    public async Task Product_list_can_be_filtered_by_category_brand_and_price()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/products?page=1&pageSize=5&sortBy=price_asc");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        var data = payload.GetProperty("data");

        data.GetProperty("items").GetArrayLength().Should().BeGreaterThan(0);
        data.GetProperty("page").GetInt32().Should().Be(1);
        data.GetProperty("pageSize").GetInt32().Should().Be(5);
    }

    [Fact]
    public async Task Customer_cannot_create_product()
    {
        using var customerClient = await AuthTestHelper.CreateAuthenticatedCustomerAsync(_factory);
        var (categoryId, brandId) = await GetCatalogReferenceIdsAsync();

        var response = await customerClient.PostAsJsonAsync(
            "/api/v1/admin/products",
            new
            {
                categoryId,
                brandId,
                name = "Sản phẩm trái phép",
                description = "Mô tả",
                specificationsJson = (string?)null,
                variants = Array.Empty<object>()
            },
            Json);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Creating_product_with_unknown_category_returns_validation_error()
    {
        var (_, brandId) = await GetCatalogReferenceIdsAsync();

        var response = await _staffClient.PostAsJsonAsync(
            "/api/v1/admin/products",
            new
            {
                categoryId = 999_999_999,
                brandId,
                name = "Sản phẩm lỗi",
                description = "Mô tả",
                specificationsJson = (string?)null,
                variants = Array.Empty<object>()
            },
            Json);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private async Task<(long CategoryId, long BrandId)> GetCatalogReferenceIdsAsync()
    {
        using var client = _factory.CreateClient();

        var categories = await client.GetFromJsonAsync<JsonElement>("/api/v1/categories", Json);
        var categoryId = categories.GetProperty("data")[0].GetProperty("id").GetInt64();

        var brands = await client.GetFromJsonAsync<JsonElement>("/api/v1/brands", Json);
        var brandId = brands.GetProperty("data")[0].GetProperty("id").GetInt64();

        return (categoryId, brandId);
    }
}
