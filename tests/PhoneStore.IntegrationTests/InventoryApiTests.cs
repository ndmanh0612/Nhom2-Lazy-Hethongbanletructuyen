using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace PhoneStore.IntegrationTests;

/// <summary>
/// FR-14 (N2LHTBLTT-21): Kiểm thử tích hợp quản trị tồn kho.
///
/// Phủ:
/// - Nhập kho tăng tồn và ghi vào nhật ký biến động.
/// - Điều chỉnh tồn thủ công (tăng / giảm).
/// - Nhập kho hàng loạt.
/// - Nhật ký tồn kho có thể lọc theo phiên bản sản phẩm và loại biến động.
/// - Chỉ nhân viên / admin mới được gọi được API quản trị tồn kho.
/// </summary>
[Collection(ApiTestCollection.Name)]
public class InventoryApiTests : IAsyncLifetime
{
    private readonly PhoneStoreApiFactory _factory;
    private HttpClient _staffClient = null!;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public InventoryApiTests(PhoneStoreApiFactory factory)
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

    [Fact]
    public async Task Import_stock_increases_quantity_and_writes_movement_log()
    {
        var variantId = await GetFirstVariantIdAsync();

        // Bảo đảm có bản ghi tồn kho trước khi nhập.
        await _staffClient.PostAsJsonAsync(
            "/api/v1/admin/inventory/stocks/import",
            new { productVariantId = variantId, quantity = 50, note = "Nhập kho test" },
            Json);

        var response = await _staffClient.PostAsJsonAsync(
            "/api/v1/admin/inventory/stocks/import",
            new { productVariantId = variantId, quantity = 20, note = "Nhập kho test" },
            Json);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        var stock = payload.GetProperty("data");
        stock.GetProperty("productVariantId").GetInt64().Should().Be(variantId);
        stock.GetProperty("stockQuantity").GetInt32().Should().BeGreaterThanOrEqualTo(70);

        // Nhật ký biến động phải ghi lại.
        var movements = await _staffClient.GetFromJsonAsync<JsonElement>(
            $"/api/v1/admin/inventory/movements?productVariantId={variantId}&type=IMPORT",
            Json);

        var items = movements.GetProperty("data").GetProperty("items");
        items.GetArrayLength().Should().BeGreaterThan(0);

        var latest = items[0];
        latest.GetProperty("quantity").GetInt32().Should().Be(20);
        latest.GetProperty("quantityAfter").GetInt32()
            .Should().Be(latest.GetProperty("quantityBefore").GetInt32() + 20);
        latest.GetProperty("note").GetString().Should().Be("Nhập kho test");
    }

    [Fact]
    public async Task Adjust_stock_increase_and_decrease_quantity()
    {
        var variantId = await GetFirstVariantIdAsync();

        await _staffClient.PostAsJsonAsync(
            "/api/v1/admin/inventory/stocks/import",
            new { productVariantId = variantId, quantity = 30, note = (string?)null },
            Json);

        var increase = await _staffClient.PostAsJsonAsync(
            "/api/v1/admin/inventory/stocks/adjust",
            new { productVariantId = variantId, delta = 5, note = "Tăng thủ công" },
            Json);

        increase.StatusCode.Should().Be(HttpStatusCode.OK);
        var increased = (await increase.Content.ReadFromJsonAsync<JsonElement>(Json))
            .GetProperty("data").GetProperty("stockQuantity").GetInt32();

        var decrease = await _staffClient.PostAsJsonAsync(
            "/api/v1/admin/inventory/stocks/adjust",
            new { productVariantId = variantId, delta = -3, note = "Giảm thủ công" },
            Json);

        decrease.StatusCode.Should().Be(HttpStatusCode.OK);
        var decreased = (await decrease.Content.ReadFromJsonAsync<JsonElement>(Json))
            .GetProperty("data").GetProperty("stockQuantity").GetInt32();

        decreased.Should().Be(increased - 3);
    }

    [Fact]
    public async Task Adjust_stock_below_zero_is_rejected()
    {
        var variantId = await GetFirstVariantIdAsync();

        var response = await _staffClient.PostAsJsonAsync(
            "/api/v1/admin/inventory/stocks/adjust",
            new { productVariantId = variantId, delta = -999_999, note = (string?)null },
            Json);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Adjust_stock_with_zero_delta_is_rejected()
    {
        var variantId = await GetFirstVariantIdAsync();

        var response = await _staffClient.PostAsJsonAsync(
            "/api/v1/admin/inventory/stocks/adjust",
            new { productVariantId = variantId, delta = 0, note = (string?)null },
            Json);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Import_stock_batch_processes_all_items()
    {
        var firstVariantId = await GetFirstVariantIdAsync();
        var secondVariantId = await GetSecondVariantIdAsync();

        var response = await _staffClient.PostAsJsonAsync(
            "/api/v1/admin/inventory/stocks/import/batch",
            new
            {
                items = new[]
                {
                    new { productVariantId = firstVariantId, quantity = 5, note = (string?)null },
                    new { productVariantId = secondVariantId, quantity = 7, note = (string?)null }
                },
                note = "Nhập kho hàng loạt"
            },
            Json);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var items = (await response.Content.ReadFromJsonAsync<JsonElement>(Json))
            .GetProperty("data");

        items.GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task Stock_movements_can_be_filtered_by_type()
    {
        var variantId = await GetFirstVariantIdAsync();

        await _staffClient.PostAsJsonAsync(
            "/api/v1/admin/inventory/stocks/import",
            new { productVariantId = variantId, quantity = 3, note = (string?)null },
            Json);

        var response = await _staffClient.GetAsync(
            "/api/v1/admin/inventory/movements?type=ADJUSTMENT&page=1&pageSize=10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        var items = payload.GetProperty("data").GetProperty("items");

        items.EnumerateArray()
            .Should().OnlyContain(item => item.GetProperty("type").GetString() == "ADJUSTMENT");
    }

    [Fact]
    public async Task Low_stock_list_returns_variants_at_or_below_threshold()
    {
        var response = await _staffClient.GetAsync("/api/v1/admin/inventory/low-stock?threshold=0");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        var items = payload.GetProperty("data");

        items.EnumerateArray()
            .Should().OnlyContain(item => item.GetProperty("stockQuantity").GetInt32() <= 0);
    }

    [Fact]
    public async Task Customer_cannot_import_stock()
    {
        using var customerClient = await AuthTestHelper.CreateAuthenticatedCustomerAsync(_factory);

        var response = await customerClient.PostAsJsonAsync(
            "/api/v1/admin/inventory/stocks/import",
            new { productVariantId = 1, quantity = 1, note = (string?)null },
            Json);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Import_stock_with_invalid_quantity_returns_validation_error()
    {
        var variantId = await GetFirstVariantIdAsync();

        var response = await _staffClient.PostAsJsonAsync(
            "/api/v1/admin/inventory/stocks/import",
            new { productVariantId = variantId, quantity = 0, note = (string?)null },
            Json);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        payload.GetProperty("code").GetString().Should().Be("VALIDATION_ERROR");
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private async Task<long> GetFirstVariantIdAsync()
    {
        using var client = _factory.CreateClient();

        var products = await client.GetFromJsonAsync<JsonElement>("/api/v1/products", Json);
        var productId = products.GetProperty("data").GetProperty("items")[0].GetProperty("id").GetInt64();

        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/v1/products/{productId}", Json);

        return detail.GetProperty("data").GetProperty("variants")[0].GetProperty("id").GetInt64();
    }

    private async Task<long> GetSecondVariantIdAsync()
    {
        using var client = _factory.CreateClient();

        var products = await client.GetFromJsonAsync<JsonElement>("/api/v1/products", Json);
        var items = products.GetProperty("data").GetProperty("items");

        for (var index = 0; index < items.GetArrayLength(); index++)
        {
            var productId = items[index].GetProperty("id").GetInt64();
            var detail = await client.GetFromJsonAsync<JsonElement>($"/api/v1/products/{productId}", Json);
            var variants = detail.GetProperty("data").GetProperty("variants");

            if (variants.GetArrayLength() > 1)
            {
                return variants[1].GetProperty("id").GetInt64();
            }
        }

        // Không có sản phẩm nào có nhiều phiên bản — dùng lại phiên bản đầu.
        return await GetFirstVariantIdAsync();
    }
}
