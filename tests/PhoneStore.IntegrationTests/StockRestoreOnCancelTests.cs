using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace PhoneStore.IntegrationTests;

/// <summary>
/// N2LHTBLTT-26: Kiểm thử tích hợp hoàn tồn kho khi hủy đơn (FR-09).
///
/// Luồng kiểm tra: tạo đơn → hủy đơn → kiểm tra `inventory.product_stocks`
/// của các phiên bản sản phẩm trong đơn đã được hoàn lại đúng số lượng.
/// </summary>
[Collection(ApiTestCollection.Name)]
public class StockRestoreOnCancelTests : IAsyncLifetime
{
    private readonly PhoneStoreApiFactory _factory;
    private HttpClient _client = null!;

    private static int _userIdCounter = 900;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public StockRestoreOnCancelTests(PhoneStoreApiFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        // N2LHTBLTT-8 / N2LHTBLTT-9: xac thuc bang JWT that.
        _client = await AuthTestHelper.CreateAuthenticatedCustomerAsync(_factory);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Cancelling_order_restores_stock_for_ordered_variants()
    {
        // 1. Lấy một variant hợp lệ từ catalog.
        var products = await _client.GetFromJsonAsync<JsonElement>("/api/v1/products", Json);
        var productId = products.GetProperty("data").GetProperty("items")[0].GetProperty("id").GetInt64();

        var detail = await _client.GetFromJsonAsync<JsonElement>($"/api/v1/products/{productId}", Json);
        var variantId = detail.GetProperty("data").GetProperty("variants")[0].GetProperty("id").GetInt64();

        // 2. Đọc tồn kho trước khi đặt hàng.
        var stockBefore = await GetStockAsync(variantId);

        // 3. Đặt 2 sản phẩm vào giỏ rồi checkout.
        await _client.PostAsJsonAsync(
            "/api/v1/cart/items",
            new { productVariantId = variantId, quantity = 2 },
            Json);

        var checkoutResponse = await _client.PostAsJsonAsync(
            "/api/v1/orders/checkout",
            new
            {
                receiverName = "Nguyễn Văn Khách",
                receiverPhone = "0912345678",
                shippingAddress = "123 Nguyễn Huệ, Quận 1, TP.HCM"
            },
            Json);

        checkoutResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.Created);

        var checkoutPayload = await checkoutResponse.Content.ReadFromJsonAsync<JsonElement>(Json);
        var orderId = checkoutPayload.GetProperty("data").GetProperty("id").GetInt64();

        // 4. Hủy đơn → Order phát OrderCancelled → Inventory hoàn tồn.
        var cancelResponse = await _client.PostAsJsonAsync(
            $"/api/v1/orders/{orderId}/cancel",
            new { reason = "Kiểm thử hoàn tồn." },
            Json);

        cancelResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);

        // 5. Tồn kho phải được hoàn lại đúng 2 đơn vị.
        var stockAfter = await GetStockAsync(variantId);

        stockAfter.Should().Be(stockBefore + 2);
    }

    private async Task<int> GetStockAsync(long productVariantId)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<PhoneStore.Modules.Inventory.Infrastructure.Persistence.InventoryDbContext>();

        var stock = await dbContext.ProductStocks
            .AsNoTracking()
            .Where(item => item.ProductVariantId == productVariantId)
            .Select(item => item.StockQuantity)
            .FirstOrDefaultAsync();

        return stock;
    }
}
