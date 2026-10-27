using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;

namespace PhoneStore.IntegrationTests;

/// <summary>
/// N2LHTBLTT-24: Kiểm thử tích hợp luồng tạo đơn hàng end-to-end qua HTTP API.
///
/// Phủ các yêu cầu nghiệp vụ quan trọng:
/// - F-01: Server tự lấy giá từ Catalog, không tin giá client gửi lên.
/// - FR-06 / FR-07: Thêm giỏ hàng rồi checkout COD tạo đơn và tính tổng tiền.
/// - Validation trả về đúng định dạng ErrorResponse chuẩn.
/// </summary>
[Collection(ApiTestCollection.Name)]
public class CheckoutFlowTests : IAsyncLifetime
{
    private readonly PhoneStoreApiFactory _factory;
    private HttpClient _client = null!;

    private static int _userIdCounter = 100;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public CheckoutFlowTests(PhoneStoreApiFactory factory)
    {
        _factory = factory;
    }
    public async Task InitializeAsync()
    {
        // Mỗi test dùng một tài khoản riêng để giỏ hàng không bị chia sẻ giữa các test.
        // N2LHTBLTT-8 / N2LHTBLTT-9: xac thuc bang JWT that.
        _client = await AuthTestHelper.CreateAuthenticatedCustomerAsync(_factory);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Health_endpoint_returns_healthy()
    {
        var response = await _client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        payload.GetProperty("status").GetString().Should().Be("Healthy");
        payload.GetProperty("architecture").GetString().Should().Contain("Modular Monolith");
    }

    [Fact]
    public async Task Products_endpoint_returns_seeded_catalog()
    {
        var response = await _client.GetAsync("/api/v1/products");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        var items = payload.GetProperty("data").GetProperty("items");

        items.GetArrayLength().Should().BeGreaterThan(0);

        // FR-04: mỗi sản phẩm phải có giá min > 0 (dữ liệu seed).
        foreach (var item in items.EnumerateArray())
        {
            item.GetProperty("minPrice").GetInt64().Should().BeGreaterThan(0);
        }
    }

    [Fact]
    public async Task Add_to_cart_uses_catalog_price_and_ignores_client_supplied_price()
    {
        // Lấy variant id từ catalog để đảm bảo tồn tồn tại.
        var products = await _client.GetFromJsonAsync<JsonElement>("/api/v1/products", Json);
        var firstProduct = products.GetProperty("data").GetProperty("items")[0];
        var productId = firstProduct.GetProperty("id").GetInt64();

        var detail = await _client.GetFromJsonAsync<JsonElement>($"/api/v1/products/{productId}", Json);
        var variant = detail.GetProperty("data").GetProperty("variants")[0];
        var variantId = variant.GetProperty("id").GetInt64();
        var expectedPrice = variant.GetProperty("price").GetInt64();

        // Client cố gửi thêm field giá (giá "rẻ" hơn) — server phải bỏ qua.
        var request = new
        {
            productVariantId = variantId,
            quantity = 2,
            price = 1,
            unitPrice = 1
        };

        var response = await _client.PostAsJsonAsync("/api/v1/cart/items", request, Json);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        var data = payload.GetProperty("data");

        // F-01: giá đơn vị phải lấy từ Catalog, không phải giá client gửi lên.
        data.GetProperty("items")[0].GetProperty("unitPrice").GetInt64().Should().Be(expectedPrice);
        data.GetProperty("subtotal").GetInt64().Should().Be(expectedPrice * 2);
        data.GetProperty("totalQuantity").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task Add_to_cart_with_invalid_quantity_returns_standard_validation_error()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/cart/items",
            new { productVariantId = 1, quantity = 99 },
            Json);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        payload.GetProperty("code").GetString().Should().Be("VALIDATION_ERROR");
        payload.GetProperty("message").GetString().Should().NotBeNullOrWhiteSpace();
        payload.GetProperty("fieldErrors").TryGetProperty("Quantity", out var quantityErrors).Should().BeTrue();
        quantityErrors.GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Checkout_creates_order_with_cod_total_from_cart()
    {
        // Chuẩn bị giỏ hàng.
        // N2LHTBLTT-23: dung variant seed "iPhone 15" de co du ton kho lon, tranh
        // viec chon variant cua san pham vua test quan tri tao (ton thap).
        var (variantId, expectedPrice, _) = await ApiTestHelper.GetFirstVariantAsync(_client);

        await _client.PostAsJsonAsync(
            "/api/v1/cart/items",
            new { productVariantId = variantId, quantity = 2 },
            Json);

        // FR-07: checkout COD.
        var checkoutResponse = await _client.PostAsJsonAsync(
            "/api/v1/orders/checkout",
            new
            {
                receiverName = "Nguyễn Văn Khách",
                receiverPhone = "0912345678",
                shippingAddress = "123 Nguyễn Huệ, Quận 1, TP.HCM",
                note = "Giao sau 18h"
            },
            Json);

        checkoutResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var checkoutPayload = await checkoutResponse.Content.ReadFromJsonAsync<JsonElement>(Json);
        var order = checkoutPayload.GetProperty("data");

        // FR-07: tổng tiền = tổng tiền giỏ hàng.
        order.GetProperty("totalAmount").GetInt64().Should().Be(expectedPrice * 2);
        order.GetProperty("status").GetString().Should().Be("PENDING");
        order.GetProperty("paymentMethod").GetString().Should().Be("COD");
        order.GetProperty("orderNumber").GetString().Should().NotBeNullOrWhiteSpace();
        order.GetProperty("items").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task Checkout_with_invalid_phone_returns_validation_error()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/orders/checkout",
            new
            {
                receiverName = "Nguyễn Văn Khách",
                receiverPhone = "abc",
                shippingAddress = "123 Nguyễn Huệ"
            },
            Json);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        payload.GetProperty("code").GetString().Should().Be("VALIDATION_ERROR");
    }

    [Fact]
    public async Task Swagger_document_is_available_in_development()
    {
        var response = await _client.GetAsync("/swagger/v1/swagger.json");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var document = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        document.GetProperty("info").GetProperty("title").GetString().Should().Be("PhoneStore API");
        document.GetProperty("paths").EnumerateObject().Should().NotBeEmpty();
    }
}
