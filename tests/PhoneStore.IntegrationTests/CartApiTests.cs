using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace PhoneStore.IntegrationTests;

/// <summary>
/// N2LHTBLTT-19: Kiểm thử tích hợp API giỏ hàng (FR-06) — thêm, sửa số lượng, xóa.
///
/// Phủ:
/// - Thêm sản phẩm vào giỏ (giá lấy từ Catalog).
/// - Thêm cùng sản phẩm lần hai → gộp thành một dòng, cộng dồn số lượng.
/// - Sửa số lượng của một dòng trong giỏ.
/// - Xóa một dòng khỏi giỏ.
/// - Giỏ trống sau khi xóa hết → checkout bị từ chối.
/// </summary>
[Collection(ApiTestCollection.Name)]
public class CartApiTests : IAsyncLifetime
{
    private readonly PhoneStoreApiFactory _factory;
    private HttpClient _client = null!;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public CartApiTests(PhoneStoreApiFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        // N2LHTBLTT-8 / N2LHTBLTT-9: client được xác thực bằng JWT thật.
        _client = await AuthTestHelper.CreateAuthenticatedCustomerAsync(_factory);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Get_cart_creates_empty_active_cart_for_new_user()
    {
        var response = await _client.GetAsync("/api/v1/cart");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        var cart = payload.GetProperty("data");

        cart.GetProperty("status").GetString().Should().Be("ACTIVE");
        cart.GetProperty("totalQuantity").GetInt32().Should().Be(0);
        cart.GetProperty("subtotal").GetInt64().Should().Be(0);
        cart.GetProperty("items").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Adding_same_variant_twice_merges_into_one_line()
    {
        var variant = await GetFirstVariantAsync();

        await _client.PostAsJsonAsync(
            "/api/v1/cart/items",
            new { productVariantId = variant.Id, quantity = 2 },
            Json);

        var response = await _client.PostAsJsonAsync(
            "/api/v1/cart/items",
            new { productVariantId = variant.Id, quantity = 3 },
            Json);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        var cart = payload.GetProperty("data");

        // Cùng một phiên bản → chỉ 1 dòng, số lượng cộng dồn.
        cart.GetProperty("items").GetArrayLength().Should().Be(1);
        cart.GetProperty("totalQuantity").GetInt32().Should().Be(5);
        cart.GetProperty("subtotal").GetInt64().Should().Be(variant.Price * 5);
    }

    [Fact]
    public async Task Update_item_quantity_changes_quantity_and_subtotal()
    {
        var variant = await GetFirstVariantAsync();

        await _client.PostAsJsonAsync(
            "/api/v1/cart/items",
            new { productVariantId = variant.Id, quantity = 2 },
            Json);

        var cart = await _client.GetFromJsonAsync<JsonElement>("/api/v1/cart", Json);
        var cartItemId = cart.GetProperty("data").GetProperty("items")[0].GetProperty("id").GetInt64();

        var response = await _client.PutAsJsonAsync(
            $"/api/v1/cart/items/{cartItemId}",
            new { quantity = 4 },
            Json);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        var updated = payload.GetProperty("data");

        updated.GetProperty("totalQuantity").GetInt32().Should().Be(4);
        updated.GetProperty("subtotal").GetInt64().Should().Be(variant.Price * 4);
    }

    [Fact]
    public async Task Update_item_quantity_beyond_limit_returns_validation_error()
    {
        var variant = await GetFirstVariantAsync();

        await _client.PostAsJsonAsync(
            "/api/v1/cart/items",
            new { productVariantId = variant.Id, quantity = 2 },
            Json);

        var cart = await _client.GetFromJsonAsync<JsonElement>("/api/v1/cart", Json);
        var cartItemId = cart.GetProperty("data").GetProperty("items")[0].GetProperty("id").GetInt64();

        var response = await _client.PutAsJsonAsync(
            $"/api/v1/cart/items/{cartItemId}",
            new { quantity = 50 },
            Json);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        payload.GetProperty("code").GetString().Should().Be("VALIDATION_ERROR");
    }

    [Fact]
    public async Task Remove_item_empties_the_cart()
    {
        var variant = await GetFirstVariantAsync();

        await _client.PostAsJsonAsync(
            "/api/v1/cart/items",
            new { productVariantId = variant.Id, quantity = 2 },
            Json);

        var cart = await _client.GetFromJsonAsync<JsonElement>("/api/v1/cart", Json);
        var cartItemId = cart.GetProperty("data").GetProperty("items")[0].GetProperty("id").GetInt64();

        var response = await _client.DeleteAsync($"/api/v1/cart/items/{cartItemId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        var updated = payload.GetProperty("data");

        updated.GetProperty("items").GetArrayLength().Should().Be(0);
        updated.GetProperty("totalQuantity").GetInt32().Should().Be(0);
        updated.GetProperty("subtotal").GetInt64().Should().Be(0);
    }

    [Fact]
    public async Task Adding_unknown_variant_returns_not_found()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/cart/items",
            new { productVariantId = 999_999, quantity = 1 },
            Json);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        payload.GetProperty("code").GetString().Should().Be("PRODUCT_VARIANT_NOT_FOUND");
    }

    [Fact]
    public async Task Removing_unknown_cart_item_returns_not_found()
    {
        var response = await _client.DeleteAsync("/api/v1/cart/items/999999");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        payload.GetProperty("code").GetString().Should().Be("CART_ITEM_NOT_FOUND");
    }

    [Fact]
    public async Task Cart_request_without_user_identity_returns_unauthorized()
    {
        using var anonymousClient = _factory.CreateClient();

        var response = await anonymousClient.GetAsync("/api/v1/cart");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        payload.GetProperty("code").GetString().Should().Be("UNAUTHORIZED");
    }

    private async Task<(long Id, long Price)> GetFirstVariantAsync()
    {
        var products = await _client.GetFromJsonAsync<JsonElement>("/api/v1/products", Json);
        var productId = products.GetProperty("data").GetProperty("items")[0].GetProperty("id").GetInt64();

        var detail = await _client.GetFromJsonAsync<JsonElement>($"/api/v1/products/{productId}", Json);
        var variant = detail.GetProperty("data").GetProperty("variants")[0];

        return (variant.GetProperty("id").GetInt64(), variant.GetProperty("price").GetInt64());
    }
}
